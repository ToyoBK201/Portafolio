using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SolicitudesTechGov.Api;
using SolicitudesTechGov.Api.Auth;
using SolicitudesTechGov.Api.Correlation;
using SolicitudesTechGov.Api.Admin;
using SolicitudesTechGov.Api.Export;
using SolicitudesTechGov.Api.Requests;
using SolicitudesTechGov.Application.Abstractions;
using SolicitudesTechGov.Application.Requests.Commands;
using SolicitudesTechGov.Application.Requests.Dtos;
using SolicitudesTechGov.Domain;
using SolicitudesTechGov.Infrastructure;
using SolicitudesTechGov.Infrastructure.Attachments;
using SolicitudesTechGov.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 12 * 1024 * 1024;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 12 * 1024 * 1024;
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

var connectionString = builder.Configuration.GetConnectionString("SqlServer")
    ?? throw new InvalidOperationException("Connection string 'SqlServer' is required.");

builder.Services.AddInfrastructure(connectionString, builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICorrelationIdAccessor, HttpCorrelationIdAccessor>();
builder.Services.AddScoped<CreateDraftRequestHandler>();
builder.Services.AddScoped<UpdateDraftRequestHandler>();
builder.Services.AddScoped<TransitionRequestHandler>();
builder.Services.AddSingleton<JwtTokenIssuer>();

var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is required.");
var jwtIssuer = jwtSection["Issuer"] ?? "SolicitudesTechGov";
var jwtAudience = jwtSection["Audience"] ?? "SolicitudesTechGov";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var loginRatePermit = builder.Configuration.GetValue("Authentication:LoginRateLimit:PermitLimit", 20);
var loginRateWindowSeconds = builder.Configuration.GetValue("Authentication:LoginRateLimit:WindowSeconds", 60);
var loginRateWindow = TimeSpan.FromSeconds(Math.Max(1, loginRateWindowSeconds));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            ip,
            _ => new FixedWindowRateLimiterOptions
            {
                Window = loginRateWindow,
                PermitLimit = Math.Max(1, loginRatePermit),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            ApiProblemBody.For(
                context.HttpContext,
                429,
                "Demasiados intentos",
                "Espere unos segundos e intente de nuevo."));
    };
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

app.UseCors();

app.UseRateLimiter();

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var correlationId = context.Items.TryGetValue(CorrelationIdConstants.ItemsKey, out var cid) && cid is Guid g
            ? g.ToString()
            : context.TraceIdentifier;

        var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        if (ex is DomainValidationException dve)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                title = "Error de validación",
                status = 400,
                detail = dve.Message,
                correlationId
            });
            return;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            title = "Error interno del servidor",
            status = 500,
            detail = app.Environment.IsDevelopment() ? ex?.Message : "Se produjo un error inesperado.",
            correlationId
        });
    });
});

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/api/v1/health", () => Results.Ok(new
{
    service = "SolicitudesTechGov.Api",
    status = "ok",
    timestampUtc = DateTime.UtcNow
})).AllowAnonymous();

var devTokenLifetime = TimeSpan.FromHours(8);
if (app.Environment.IsDevelopment()
    || builder.Configuration.GetValue("Authentication:AllowDevTokenEndpoint", false))
{
    app.MapPost(
        "/api/v1/auth/dev-token",
        (DevTokenRequest request, JwtTokenIssuer issuer) =>
        {
            if (request.Role is not (
                "Requester" or
                "AreaCoordinator" or
                "TicAnalyst" or
                "InstitutionalApprover" or
                "Implementer" or
                "SystemAdministrator" or
                "Auditor"))
            {
                return Results.BadRequest(new
                {
                    title = "Invalid role",
                    status = 400
                });
            }

            var accessToken = issuer.IssueToken(request.UserId, request.Role, devTokenLifetime, JwtAuthMethodClaims.Dev);
            return Results.Ok(new
            {
                accessToken,
                tokenType = "Bearer",
                expiresIn = (int)devTokenLifetime.TotalSeconds
            });
        }).AllowAnonymous();
}

app.MapGet(
    "/api/v1/me/roles",
    async (HttpContext httpContext, IAppUserAdminRepository repo, CancellationToken cancellationToken) =>
    {
        var userId = httpContext.User.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var items = await repo.ListRolesForUserAsync(userId.Value, cancellationToken);
        return Results.Ok(new { items });
    }).RequireAuthorization();

app.MapPost(
    "/api/v1/auth/switch-role",
    async (
        SwitchRoleRequest body,
        HttpContext httpContext,
        IAppUserAdminRepository repo,
        JwtTokenIssuer issuer,
        CancellationToken cancellationToken) =>
    {
        var userId = httpContext.User.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        if (!string.Equals(httpContext.User.GetAuthMethod(), JwtAuthMethodClaims.Password, StringComparison.Ordinal))
        {
            return Results.Json(
                ApiProblemBody.For(
                    httpContext,
                    400,
                    "Sesión no compatible",
                    "Use el selector «Rol activo (dev)» o inicie sesión con contraseña para cambiar rol según la base de datos."),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var rc = body.RoleCode?.Trim() ?? "";
        if (rc.Length < 2)
        {
            return Results.BadRequest(
                ApiProblemBody.For(httpContext, 400, "Datos inválidos", "Indique un rol válido."));
        }

        var roles = await repo.ListRolesForUserAsync(userId.Value, cancellationToken);
        var match = roles.FirstOrDefault(r => string.Equals(r.Code, rc, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return Results.Json(
                ApiProblemBody.For(httpContext, 403, "Rol no asignado", "No tiene asignado ese rol."),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var accessToken = issuer.IssueToken(userId.Value, match.Code, devTokenLifetime, JwtAuthMethodClaims.Password);
        return Results.Ok(new
        {
            accessToken,
            tokenType = "Bearer",
            expiresIn = (int)devTokenLifetime.TotalSeconds
        });
    }).RequireAuthorization();

app.MapPost(
    "/api/v1/auth/login",
    async (
        LoginRequestJson body,
        HttpContext httpContext,
        IUserAuthenticationService auth,
        JwtTokenIssuer issuer,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken) =>
    {
        var log = loggerFactory.CreateLogger("Auth.Login");
        var email = body.Email?.Trim() ?? "";
        var password = body.Password ?? "";
        if (email.Length < 3 || password.Length < 1)
        {
            return Results.BadRequest(
                ApiProblemBody.For(httpContext, 400, "Datos inválidos", "Email y contraseña son obligatorios."));
        }

        var outcome = await auth.TryLoginAsync(email, password, cancellationToken);
        switch (outcome)
        {
            case LoginSucceeded s:
                var accessToken = issuer.IssueToken(s.UserId, s.RoleCode, devTokenLifetime);
                return Results.Ok(new
                {
                    accessToken,
                    tokenType = "Bearer",
                    expiresIn = (int)devTokenLifetime.TotalSeconds
                });
            case LoginFailed f:
                log.LogWarning(
                    "Login failed: {Reason} for {EmailMasked}",
                    f.Reason,
                    MaskEmailForLog(email));
                return f.Reason switch
                {
                    LoginFailureReason.InvalidCredentials => Results.Json(
                        ApiProblemBody.For(
                            httpContext,
                            401,
                            "Credenciales incorrectas",
                            "Revise el email y la contraseña."),
                        statusCode: StatusCodes.Status401Unauthorized),
                    LoginFailureReason.AccountInactive => Results.Json(
                        ApiProblemBody.For(
                            httpContext,
                            401,
                            "Cuenta desactivada",
                            "Su usuario está desactivado."),
                        statusCode: StatusCodes.Status401Unauthorized),
                    LoginFailureReason.PasswordNotConfigured => Results.Json(
                        ApiProblemBody.For(
                            httpContext,
                            401,
                            "Credenciales no configuradas",
                            "Esta cuenta no tiene contraseña local. Use el proveedor institucional o contacte al administrador."),
                        statusCode: StatusCodes.Status401Unauthorized),
                    LoginFailureReason.NoRolesAssigned => Results.Json(
                        ApiProblemBody.For(
                            httpContext,
                            403,
                            "Sin roles asignados",
                            "Contacte al administrador del sistema para que le asignen permisos en la aplicación."),
                        statusCode: StatusCodes.Status403Forbidden),
                    _ => Results.Json(
                        ApiProblemBody.For(httpContext, 401, "No se pudo iniciar sesión", null),
                        statusCode: StatusCodes.Status401Unauthorized)
                };
            default:
                return Results.Json(
                    ApiProblemBody.For(httpContext, 500, "Error de autenticación", null),
                    statusCode: StatusCodes.Status500InternalServerError);
        }
    }).AllowAnonymous().RequireRateLimiting("login");

app.MapPost(
    "/api/v1/requests",
    async (
        CreateDraftRequestCommand command,
        HttpContext httpContext,
        CreateDraftRequestHandler handler,
        CancellationToken cancellationToken) =>
    {
        var user = httpContext.User;
        var userId = user.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        if (!user.IsSystemAdministrator() && command.RequesterUserId != userId)
        {
            return Results.Forbid();
        }

        var created = await handler.HandleAsync(command, cancellationToken);
        return Results.Created($"/api/v1/requests/{created.RequestId}", created);
    }).RequireAuthorization();

app.MapPatch(
    "/api/v1/requests/{id:guid}",
    async (
        Guid id,
        UpdateDraftRequestJson body,
        HttpContext httpContext,
        IRequestRepository requestRepository,
        UpdateDraftRequestHandler handler,
        CancellationToken cancellationToken) =>
    {
        var user = httpContext.User;
        var userId = user.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var existing = await requestRepository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return Results.NotFound();
        }

        if (!user.CanEditDraftRequest(existing.RequesterUserId))
        {
            return Results.Forbid();
        }

        var command = new UpdateDraftRequestCommand(
            id,
            body.Title,
            body.Description,
            body.BusinessJustification,
            body.RequestType,
            body.Priority,
            body.RequestingUnitId,
            body.DesiredDate,
            body.SpecificPayloadJson);

        var correlationId = httpContext.Items.TryGetValue(CorrelationIdConstants.ItemsKey, out var cid) && cid is Guid g
            ? g.ToString()
            : httpContext.TraceIdentifier;

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.Kind switch
        {
            0 => Results.Ok(result.Request),
            1 => Results.NotFound(),
            2 => Results.Conflict(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                title = "La solicitud no está en borrador o cambió de estado.",
                status = 409,
                detail = "Solo se pueden editar solicitudes en estado Draft.",
                correlationId
            }),
            _ => Results.BadRequest(new { title = "No se pudo actualizar el borrador.", status = 400, correlationId })
        };
    }).RequireAuthorization();

app.MapGet(
    "/api/v1/requests/{id:guid}",
    async (
        Guid id,
        HttpContext httpContext,
        IRequestRepository requestRepository,
        CancellationToken cancellationToken) =>
    {
        var user = httpContext.User;
        var userId = user.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var request = await requestRepository.GetByIdAsync(id, cancellationToken);
        if (request is null)
        {
            return Results.NotFound();
        }

        if (!user.CanViewRequestContent(request.RequesterUserId))
        {
            return Results.Forbid();
        }

        return Results.Ok(request);
    }).RequireAuthorization();

app.MapGet(
    "/api/v1/requests/{id:guid}/audit",
    async (
        Guid id,
        HttpContext httpContext,
        IRequestRepository requestRepository,
        IAuditLogRepository auditLogRepository,
        CancellationToken cancellationToken) =>
    {
        var user = httpContext.User;
        var userId = user.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var request = await requestRepository.GetByIdAsync(id, cancellationToken);
        if (request is null)
        {
            return Results.NotFound();
        }

        if (!user.CanViewRequestContent(request.RequesterUserId))
        {
            return Results.Forbid();
        }

        var items = await auditLogRepository.ListForRequestAsync(id, cancellationToken);
        return Results.Ok(new { items });
    }).RequireAuthorization();

app.MapGet(
    "/api/v1/requests/{id:guid}/attachments",
    async (
        Guid id,
        HttpContext httpContext,
        IRequestRepository requestRepository,
        IRequestAttachmentRepository attachmentRepository,
        CancellationToken cancellationToken) =>
    {
        var user = httpContext.User;
        var userId = user.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var request = await requestRepository.GetByIdAsync(id, cancellationToken);
        if (request is null)
        {
            return Results.NotFound();
        }

        if (!user.CanViewRequestContent(request.RequesterUserId))
        {
            return Results.Forbid();
        }

        var items = await attachmentRepository.ListActiveAsync(id, cancellationToken);
        return Results.Ok(new { items });
    }).RequireAuthorization();

app.MapPost(
    "/api/v1/requests/{id:guid}/attachments",
    async (
        Guid id,
        HttpContext httpContext,
        IRequestRepository requestRepository,
        IRequestAttachmentRepository attachmentRepository,
        CancellationToken cancellationToken) =>
    {
        var user = httpContext.User;
        var userId = user.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var request = await requestRepository.GetByIdAsync(id, cancellationToken);
        if (request is null)
        {
            return Results.NotFound();
        }

        if (!user.CanViewRequestContent(request.RequesterUserId))
        {
            return Results.Forbid();
        }

        if (!httpContext.Request.HasFormContentType)
        {
            return Results.BadRequest(new { title = "Se requiere multipart/form-data con un campo file.", status = 400 });
        }

        var form = await httpContext.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        if (file is null)
        {
            return Results.BadRequest(new { title = "Falta el archivo (campo file).", status = 400 });
        }

        await using var stream = file.OpenReadStream();
        var result = await attachmentRepository.TryAddAndSaveAsync(
            id,
            userId.Value,
            file.FileName,
            file.ContentType ?? "application/octet-stream",
            stream,
            file.Length,
            cancellationToken);

        if (result.Item is null)
        {
            return Results.BadRequest(new { title = result.ErrorMessage ?? "No se pudo guardar el adjunto.", status = 400 });
        }

        return Results.Created($"/api/v1/requests/{id}/attachments/{result.Item.AttachmentId}/file", result.Item);
    }).RequireAuthorization().DisableAntiforgery();

app.MapGet(
    "/api/v1/requests/{id:guid}/attachments/{attachmentId:guid}/file",
    async (
        Guid id,
        Guid attachmentId,
        HttpContext httpContext,
        IRequestRepository requestRepository,
        IRequestAttachmentRepository attachmentRepository,
        [FromServices] LocalAttachmentFileStore fileStore,
        CancellationToken cancellationToken) =>
    {
        var user = httpContext.User;
        var userId = user.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var request = await requestRepository.GetByIdAsync(id, cancellationToken);
        if (request is null)
        {
            return Results.NotFound();
        }

        if (!user.CanViewRequestContent(request.RequesterUserId))
        {
            return Results.Forbid();
        }

        var info = await attachmentRepository.GetActiveByIdAsync(id, attachmentId, cancellationToken);
        if (info is null)
        {
            return Results.NotFound();
        }

        var stream = fileStore.OpenRead(info.RelativeStoragePath);
        return Results.File(stream, info.ContentType, info.FileName);
    }).RequireAuthorization();

app.MapGet(
    "/api/v1/requests/{id:guid}/comments",
    async (
        Guid id,
        HttpContext httpContext,
        IRequestRepository requestRepository,
        IRequestCommentRepository commentRepository,
        CancellationToken cancellationToken) =>
    {
        var user = httpContext.User;
        var userId = user.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var request = await requestRepository.GetByIdAsync(id, cancellationToken);
        if (request is null)
        {
            return Results.NotFound();
        }

        if (!user.CanViewRequestContent(request.RequesterUserId))
        {
            return Results.Forbid();
        }

        var includeInternal = !user.ExcludeInternalCommentsFromResponse();
        var items = await commentRepository.ListForRequestAsync(id, includeInternal, cancellationToken);
        return Results.Ok(new { items });
    }).RequireAuthorization();

app.MapPost(
    "/api/v1/requests/{id:guid}/comments",
    async (
        Guid id,
        CommentPostBody body,
        HttpContext httpContext,
        IRequestRepository requestRepository,
        IRequestCommentRepository commentRepository,
        CancellationToken cancellationToken) =>
    {
        var user = httpContext.User;
        var userId = user.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var request = await requestRepository.GetByIdAsync(id, cancellationToken);
        if (request is null)
        {
            return Results.NotFound();
        }

        if (!user.CanViewRequestContent(request.RequesterUserId))
        {
            return Results.Forbid();
        }

        if (body.IsInternal)
        {
            if (!user.CanPostInternalComment())
            {
                return Results.Forbid();
            }
        }
        else if (!user.CanPostPublicComment())
        {
            return Results.Forbid();
        }

        var result = await commentRepository.TryAddAndSaveAsync(
            id,
            userId.Value,
            body.Body,
            body.IsInternal,
            cancellationToken);

        if (result.Item is null)
        {
            return Results.BadRequest(new { title = result.ErrorMessage ?? "No se pudo guardar el comentario.", status = 400 });
        }

        return Results.Created($"/api/v1/requests/{id}/comments", result.Item);
    }).RequireAuthorization();

app.MapGet(
    "/api/v1/me/notifications/summary",
    async (HttpContext httpContext, IUserNotificationRepository notificationRepository, CancellationToken cancellationToken) =>
    {
        var userId = httpContext.User.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var unreadCount = await notificationRepository.CountUnreadAsync(userId.Value, cancellationToken);
        return Results.Ok(new { unreadCount });
    }).RequireAuthorization();

app.MapGet(
    "/api/v1/me/notifications",
    async (
        HttpContext httpContext,
        IUserNotificationRepository notificationRepository,
        int? take,
        bool? unreadOnly,
        CancellationToken cancellationToken) =>
    {
        var userId = httpContext.User.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var items = await notificationRepository.ListForUserAsync(
            userId.Value,
            take ?? 30,
            unreadOnly ?? false,
            cancellationToken);
        return Results.Ok(new { items });
    }).RequireAuthorization();

app.MapPost(
    "/api/v1/me/notifications/{notificationId:guid}/read",
    async (
        Guid notificationId,
        HttpContext httpContext,
        IUserNotificationRepository notificationRepository,
        CancellationToken cancellationToken) =>
    {
        var userId = httpContext.User.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var ok = await notificationRepository.TryMarkAsReadAsync(userId.Value, notificationId, cancellationToken);
        return ok ? Results.NoContent() : Results.NotFound();
    }).RequireAuthorization();

app.MapPost(
    "/api/v1/me/notifications/read-all",
    async (HttpContext httpContext, IUserNotificationRepository notificationRepository, CancellationToken cancellationToken) =>
    {
        var userId = httpContext.User.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var marked = await notificationRepository.MarkAllAsReadAsync(userId.Value, cancellationToken);
        return Results.Ok(new { marked });
    }).RequireAuthorization();

app.MapGet(
    "/api/v1/me/work-queue-summary",
    async (HttpContext httpContext, IRequestRepository requestRepository, CancellationToken cancellationToken) =>
    {
        var user = httpContext.User;
        var userId = user.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        Guid? scopeForCounts = string.Equals(user.GetPrimaryRole(), "Requester", StringComparison.OrdinalIgnoreCase)
            ? userId
            : null;

        var countsByStatus = await requestRepository.CountByStatusAsync(scopeForCounts, cancellationToken);
        return Results.Ok(new { countsByStatus });
    }).RequireAuthorization();

// --- Catálogos (lectura para formularios) — docs/02 §2.5, D1 ---
app.MapGet(
    "/api/v1/catalogs/organizational-units",
    async (bool? activeOnly, IOrganizationalUnitCatalogRepository repo, CancellationToken cancellationToken) =>
    {
        var onlyActive = activeOnly ?? false;
        var items = await repo.ListAsync(onlyActive, cancellationToken);
        return Results.Ok(new { items });
    }).RequireAuthorization();

app.MapGet(
    "/api/v1/catalogs/app-roles",
    async (IAppUserAdminRepository repo, CancellationToken cancellationToken) =>
    {
        var items = await repo.ListRolesAsync(cancellationToken);
        return Results.Ok(new { items });
    }).RequireAuthorization();

// --- Administración catálogos / usuarios (D1, D2) — solo SystemAdministrator ---
app.MapPost(
    "/api/v1/admin/organizational-units",
    async (
        CreateOrganizationalUnitJson body,
        HttpContext httpContext,
        IOrganizationalUnitCatalogRepository repo,
        IAuditLogRepository auditLog,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken) =>
    {
        if (!httpContext.User.IsSystemAdministrator())
        {
            return Results.Forbid();
        }

        var actorUserId = httpContext.User.GetUserId();
        if (actorUserId is null)
        {
            return Results.Unauthorized();
        }

        var code = body.Code?.Trim() ?? "";
        var name = body.Name?.Trim() ?? "";
        if (code.Length is < 1 or > 32 || name.Length is < 1 or > 200)
        {
            return Results.BadRequest(
                ApiProblemBody.For(
                    httpContext,
                    400,
                    "Datos de unidad inválidos",
                    "Code (1–32) y Name (1–200) son obligatorios."));
        }

        var id = await repo.CreateAsync(code, name, cancellationToken);
        var payloadSummary = JsonSerializer.Serialize(new { code, name });
        await auditLog.AddAdminCatalogEventAsync(
            actorUserId.Value,
            httpContext.User.GetPrimaryRole(),
            "OrganizationalUnitCreated",
            "OrganizationalUnit",
            id.ToString(CultureInfo.InvariantCulture),
            payloadSummary,
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/v1/admin/organizational-units/{id}", new { unitId = id });
    }).RequireAuthorization();

app.MapPatch(
    "/api/v1/admin/organizational-units/{unitId:int}",
    async (
        int unitId,
        PatchOrganizationalUnitJson body,
        HttpContext httpContext,
        IOrganizationalUnitCatalogRepository repo,
        IAuditLogRepository auditLog,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken) =>
    {
        if (!httpContext.User.IsSystemAdministrator())
        {
            return Results.Forbid();
        }

        var actorUserId = httpContext.User.GetUserId();
        if (actorUserId is null)
        {
            return Results.Unauthorized();
        }

        if (body.Code is null && body.Name is null && !body.IsActive.HasValue)
        {
            return Results.BadRequest(
                ApiProblemBody.For(httpContext, 400, "Sin cambios", "Indique code, name o isActive."));
        }

        if (body.IsActive == false)
        {
            var inUse = await repo.HasRequestsReferencingUnitAsync(unitId, cancellationToken);
            if (inUse)
            {
                return Results.Json(
                    ApiProblemBody.For(
                        httpContext,
                        409,
                        "Unidad en uso",
                        "No se puede desactivar: existen solicitudes con esta unidad solicitante."),
                    statusCode: StatusCodes.Status409Conflict);
            }
        }

        var ok = await repo.TryUpdateAsync(unitId, body.Code, body.Name, body.IsActive, cancellationToken);
        if (!ok)
        {
            return Results.NotFound();
        }

        var payloadSummary = JsonSerializer.Serialize(new
        {
            unitId,
            code = body.Code,
            name = body.Name,
            isActive = body.IsActive
        });
        await auditLog.AddAdminCatalogEventAsync(
            actorUserId.Value,
            httpContext.User.GetPrimaryRole(),
            "OrganizationalUnitUpdated",
            "OrganizationalUnit",
            unitId.ToString(CultureInfo.InvariantCulture),
            payloadSummary,
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }).RequireAuthorization();

app.MapGet(
    "/api/v1/admin/users",
    async (
        int? page,
        int? pageSize,
        HttpContext httpContext,
        IAppUserAdminRepository repo,
        CancellationToken cancellationToken) =>
    {
        if (!httpContext.User.IsSystemAdministrator())
        {
            return Results.Forbid();
        }

        var p = page.GetValueOrDefault(1);
        if (p < 1)
        {
            p = 1;
        }

        var ps = pageSize.GetValueOrDefault(20);
        if (ps is < 1 or > 100)
        {
            ps = 20;
        }

        var result = await repo.ListUsersAsync(p, ps, cancellationToken);
        return Results.Ok(result);
    }).RequireAuthorization();

app.MapPost(
    "/api/v1/admin/users",
    async (
        CreateAppUserJson body,
        HttpContext httpContext,
        IAppUserAdminRepository repo,
        IAuditLogRepository auditLog,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken) =>
    {
        if (!httpContext.User.IsSystemAdministrator())
        {
            return Results.Forbid();
        }

        var actorUserId = httpContext.User.GetUserId();
        if (actorUserId is null)
        {
            return Results.Unauthorized();
        }

        var email = body.Email?.Trim() ?? "";
        var display = body.DisplayName?.Trim() ?? "";
        if (email.Length < 5 || display.Length < 2 || !email.Contains('@', StringComparison.Ordinal))
        {
            return Results.BadRequest(
                ApiProblemBody.For(httpContext, 400, "Usuario inválido", "Email y nombre para mostrar son obligatorios."));
        }

        if (await repo.EmailExistsAsync(email, null, cancellationToken))
        {
            return Results.Json(
                ApiProblemBody.For(httpContext, 409, "Email duplicado", "Ya existe un usuario con ese email."),
                statusCode: StatusCodes.Status409Conflict);
        }

        var id = await repo.CreateUserAsync(email, display, cancellationToken);
        var payloadSummary = JsonSerializer.Serialize(new { email, displayName = display });
        await auditLog.AddAdminCatalogEventAsync(
            actorUserId.Value,
            httpContext.User.GetPrimaryRole(),
            "AppUserCreated",
            "AppUser",
            id.ToString(),
            payloadSummary,
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/v1/admin/users/{id}", new { userId = id });
    }).RequireAuthorization();

app.MapGet(
    "/api/v1/admin/users/{userId:guid}",
    async (Guid userId, HttpContext httpContext, IAppUserAdminRepository repo, CancellationToken cancellationToken) =>
    {
        if (!httpContext.User.IsSystemAdministrator())
        {
            return Results.Forbid();
        }

        var u = await repo.GetUserDetailAsync(userId, cancellationToken);
        return u is null ? Results.NotFound() : Results.Ok(u);
    }).RequireAuthorization();

app.MapPut(
    "/api/v1/admin/users/{userId:guid}/roles",
    async (
        Guid userId,
        PutUserRolesJson body,
        HttpContext httpContext,
        IAppUserAdminRepository repo,
        IAuditLogRepository auditLog,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken) =>
    {
        if (!httpContext.User.IsSystemAdministrator())
        {
            return Results.Forbid();
        }

        var actorUserId = httpContext.User.GetUserId();
        if (actorUserId is null)
        {
            return Results.Unauthorized();
        }

        var existing = await repo.GetUserDetailAsync(userId, cancellationToken);
        if (existing is null)
        {
            return Results.NotFound();
        }

        var list = body.Assignments ?? Array.Empty<RoleAssignmentJson>();
        var distinct = list
            .GroupBy(x => (x.RoleId, x.OrganizationalUnitId))
            .Select(g => g.First())
            .Select(x => (x.RoleId, x.OrganizationalUnitId))
            .ToList();

        foreach (var (roleId, _) in distinct)
        {
            if (roleId is < 1 or > 7)
            {
                return Results.BadRequest(
                    ApiProblemBody.For(httpContext, 400, "Rol inválido", "roleId debe existir en catálogo AppRole (1–7)."));
            }
        }

        try
        {
            await repo.ReplaceUserRolesAsync(userId, distinct, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ApiProblemBody.For(httpContext, 400, "Asignación inválida", ex.Message));
        }

        var payloadSummary = JsonSerializer.Serialize(new
        {
            assignmentCount = distinct.Count,
            assignments = distinct.Select(x => new { x.RoleId, x.OrganizationalUnitId }).ToList()
        });
        await auditLog.AddAdminCatalogEventAsync(
            actorUserId.Value,
            httpContext.User.GetPrimaryRole(),
            "UserRolesReplaced",
            "AppUser",
            userId.ToString(),
            payloadSummary,
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }).RequireAuthorization();

app.MapGet(
    "/api/v1/admin/audit-log",
    async (
        int? page,
        int? pageSize,
        string? entityType,
        string? action,
        HttpContext httpContext,
        IAuditLogRepository auditRepo,
        CancellationToken cancellationToken) =>
    {
        if (!httpContext.User.CanViewGlobalAuditLog())
        {
            return Results.Forbid();
        }

        var p = page.GetValueOrDefault(1);
        if (p < 1)
        {
            p = 1;
        }

        var ps = pageSize.GetValueOrDefault(25);
        if (ps < 1)
        {
            ps = 25;
        }
        else if (ps > 200)
        {
            ps = 200;
        }

        var result = await auditRepo.ListGlobalAsync(p, ps, entityType, action, cancellationToken);
        return Results.Ok(new
        {
            items = result.Items,
            totalCount = result.TotalCount,
            page = result.Page,
            pageSize = result.PageSize
        });
    }).RequireAuthorization();

app.MapGet(
    "/api/v1/admin/audit-log/export",
    async (
        string? entityType,
        string? action,
        HttpContext httpContext,
        IAuditLogRepository auditRepo,
        CancellationToken cancellationToken) =>
    {
        if (!httpContext.User.CanViewGlobalAuditLog())
        {
            return Results.Forbid();
        }

        const int maxRows = 5000;
        const int batchSize = 200;
        var all = new List<AdminAuditLogEntryDto>();
        var page = 1;
        while (all.Count < maxRows)
        {
            var batch = await auditRepo.ListGlobalAsync(page, batchSize, entityType, action, cancellationToken);
            if (batch.Items.Count == 0)
            {
                break;
            }

            all.AddRange(batch.Items);
            if (batch.Items.Count < batchSize)
            {
                break;
            }

            page++;
        }

        static string CsvCell(string? s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "";
            }

            var t = s.Replace("\"", "\"\"", StringComparison.Ordinal);
            return t.Contains(',') || t.Contains('"', StringComparison.Ordinal) || t.Contains('\r') || t.Contains('\n')
                ? $"\"{t}\""
                : t;
        }

        var sb = new StringBuilder();
        sb.AppendLine(
            "auditId,occurredAtUtc,correlationId,actorUserId,actorRole,action,entityType,entityId,requestId,fromStatus,toStatus,success,payloadSummary");
        foreach (var e in all)
        {
            sb.AppendLine(string.Join(
                ',',
                CsvCell(e.AuditId.ToString(CultureInfo.InvariantCulture)),
                CsvCell(e.OccurredAtUtc.ToString("o", CultureInfo.InvariantCulture)),
                CsvCell(e.CorrelationId?.ToString()),
                CsvCell(e.ActorUserId.ToString()),
                CsvCell(e.ActorRole),
                CsvCell(e.Action),
                CsvCell(e.EntityType),
                CsvCell(e.EntityId),
                CsvCell(e.RequestId?.ToString()),
                CsvCell(e.FromStatus),
                CsvCell(e.ToStatus),
                CsvCell(e.Success ? "true" : "false"),
                CsvCell(e.PayloadSummary)));
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var bytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);
        var fileName = $"audit-log_{DateTime.UtcNow:yyyy-MM-dd_HHmmss}.csv";
        return Results.File(bytes, "text/csv; charset=utf-8", fileName);
    }).RequireAuthorization();

app.MapGet(
    "/api/v1/requests",
    async (
        string? status,
        Guid? requesterUserId,
        DateTime? createdFromUtc,
        DateTime? createdToUtc,
        int? page,
        int? pageSize,
        string? sortBy,
        string? sortDirection,
        HttpContext httpContext,
        IRequestRepository requestRepository,
        CancellationToken cancellationToken) =>
    {
        var user = httpContext.User;
        var userId = user.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        if (!user.TryResolveListRequesterScope(userId, requesterUserId, out var effectiveRequesterUserId))
        {
            return Results.Forbid();
        }

        var resolvedPage = page.GetValueOrDefault(1);
        if (resolvedPage < 1)
        {
            resolvedPage = 1;
        }

        var resolvedPageSize = pageSize.GetValueOrDefault(10);
        if (resolvedPageSize < 1)
        {
            resolvedPageSize = 10;
        }

        if (resolvedPageSize > 100)
        {
            resolvedPageSize = 100;
        }

        var items = await requestRepository.ListAsync(
            status,
            effectiveRequesterUserId,
            createdFromUtc,
            createdToUtc,
            resolvedPage,
            resolvedPageSize,
            sortBy,
            sortDirection,
            cancellationToken);
        return Results.Ok(items);
    }).RequireAuthorization();

app.MapGet(
    "/api/v1/requests/export",
    async (
        string? format,
        string? status,
        Guid? requesterUserId,
        DateTime? createdFromUtc,
        DateTime? createdToUtc,
        int? maxRows,
        string? sortBy,
        string? sortDirection,
        HttpContext httpContext,
        IRequestRepository requestRepository,
        CancellationToken cancellationToken) =>
    {
        var user = httpContext.User;
        var userId = user.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var fmt = format?.Trim();
        var isCsv = string.Equals(fmt, "csv", StringComparison.OrdinalIgnoreCase);
        var isXlsx = string.Equals(fmt, "xlsx", StringComparison.OrdinalIgnoreCase);
        if (!isCsv && !isXlsx)
        {
            return Results.BadRequest(
                ApiProblemBody.For(
                    httpContext,
                    400,
                    "Parámetro format inválido",
                    "Use format=csv o format=xlsx."));
        }

        if (!user.TryResolveListRequesterScope(userId, requesterUserId, out var effectiveRequesterUserId))
        {
            return Results.Forbid();
        }

        var resolvedMax = maxRows.GetValueOrDefault(5000);
        if (resolvedMax < 1)
        {
            resolvedMax = 1;
        }

        if (resolvedMax > 10_000)
        {
            resolvedMax = 10_000;
        }

        var page = await requestRepository.ListAsync(
            status,
            effectiveRequesterUserId,
            createdFromUtc,
            createdToUtc,
            1,
            resolvedMax,
            sortBy,
            sortDirection,
            cancellationToken);

        if (isCsv)
        {
            var bytes = RequestListCsvFormatter.ToUtf8Bom(page.Items);
            var fileName = $"solicitudes_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            return Results.File(bytes, "text/csv; charset=utf-8", fileName);
        }

        var xlsx = RequestListXlsxFormatter.ToWorkbookBytes(page.Items);
        var xlsxName = $"solicitudes_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
        return Results.File(
            xlsx,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            xlsxName);
    }).RequireAuthorization();

app.MapPost(
    "/api/v1/requests/{id:guid}/transitions",
    async (
        Guid id,
        TransitionRequestBody body,
        HttpContext httpContext,
        TransitionRequestHandler handler,
        CancellationToken cancellationToken) =>
    {
        var user = httpContext.User;
        var userId = user.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(body.Transition))
        {
            return Results.BadRequest(
                ApiProblemBody.For(
                    httpContext,
                    400,
                    "Falta el campo transition",
                    "Indique el nombre de la transición a ejecutar."));
        }

        var command = new TransitionRequestCommand(
            id,
            body.Transition,
            body.Reason,
            userId.Value,
            user.GetPrimaryRole(),
            user.IsSystemAdministrator());

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.StatusCode switch
        {
            204 => Results.NoContent(),
            404 => Results.Json(
                ApiProblemBody.For(httpContext, 404, "Solicitud no encontrada", null),
                statusCode: StatusCodes.Status404NotFound),
            403 => Results.Json(
                ApiProblemBody.For(httpContext, 403, "No autorizado para esta transición", null),
                statusCode: StatusCodes.Status403Forbidden),
            409 => Results.Json(
                ApiProblemBody.For(
                    httpContext,
                    409,
                    "Conflicto de estado",
                    result.Error),
                statusCode: StatusCodes.Status409Conflict),
            _ => Results.BadRequest(
                ApiProblemBody.For(
                    httpContext,
                    400,
                    "Transición no válida",
                    result.Error))
        };
    }).RequireAuthorization();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetService<SolicitudesTechGovDbContext>();
    if (db is not null)
    {
        try
        {
            DemoUserSeeder.EnsureAsync(db).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DemoUserSeeder");
            logger.LogWarning(ex, "No se pudo ejecutar el usuario demo de login local (¿migración 003 aplicada?).");
        }
    }
}

static string MaskEmailForLog(string email)
{
    email = email.Trim();
    var at = email.IndexOf('@');
    if (at <= 0)
    {
        return "***";
    }

    var local = email[..at];
    var domain = email[at..];
    if (local.Length <= 1)
    {
        return $"*{domain}";
    }

    return $"{local[0]}***{domain}";
}

app.Run();

public partial class Program;
