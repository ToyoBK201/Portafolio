using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SolicitudesTechGov.Application.Abstractions;
using SolicitudesTechGov.Application.Requests.Dtos;
using SolicitudesTechGov.Domain;
using SolicitudesTechGov.Domain.Entities;
using SolicitudesTechGov.Infrastructure.Attachments;
using SolicitudesTechGov.Infrastructure.Persistence;
using Xunit;

namespace SolicitudesTechGov.Api.Tests;

public sealed class TransitionIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    /// <summary>
    /// Cumple <see cref="SolicitudesTechGov.Application.Requests.Validation.SubmitRequestValidator"/> para RequestType Software (2).
    /// </summary>
    private const string ValidSoftwarePayloadJson =
        """{"productName":"Office","licenseModel":"Subscription","seatOrUserCount":10,"environment":"Production"}""";

    private readonly TestWebApplicationFactory _factory;

    public TransitionIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Submit_Returns204_ForDraft_AndRequesterRole()
    {
        var client = _factory.CreateClient();

        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var token = await GetDevTokenAsync(client, userId, "Requester");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createResponse = await client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "Solicitud de licencias Office",
            description = "Se requiere licencia para personal de nueva contratación en la unidad.",
            businessJustification = "Garantizar continuidad operativa y cumplimiento institucional.",
            requestType = 2,
            priority = 2,
            requestingUnitId = 3,
            requesterUserId = userId,
            desiredDate = (string?)null,
            specificPayloadJson = ValidSoftwarePayloadJson
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedRequestStub>();
        Assert.NotNull(created);

        var transitionResponse = await client.PostAsJsonAsync(
            $"/api/v1/requests/{created!.RequestId}/transitions",
            new { transition = "Submit", reason = (string?)null });

        Assert.Equal(HttpStatusCode.NoContent, transitionResponse.StatusCode);
    }

    [Fact]
    public async Task InvalidTransition_Returns400()
    {
        var client = _factory.CreateClient();

        var userId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var token = await GetDevTokenAsync(client, userId, "Requester");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createResponse = await client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "Solicitud de licencias Office",
            description = "Se requiere licencia para personal de nueva contratación en la unidad.",
            businessJustification = "Garantizar continuidad operativa y cumplimiento institucional.",
            requestType = 2,
            priority = 2,
            requestingUnitId = 3,
            requesterUserId = userId,
            desiredDate = (string?)null,
            specificPayloadJson = (string?)null
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedRequestStub>();
        Assert.NotNull(created);

        var transitionResponse = await client.PostAsJsonAsync(
            $"/api/v1/requests/{created!.RequestId}/transitions",
            new { transition = "NotARealTransition", reason = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, transitionResponse.StatusCode);
    }

    [Fact]
    public async Task CancelByRequester_WithoutReason_Returns400()
    {
        var client = _factory.CreateClient();

        var userId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var token = await GetDevTokenAsync(client, userId, "Requester");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createResponse = await client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "Solicitud de licencias Office",
            description = "Se requiere licencia para personal de nueva contratación en la unidad.",
            businessJustification = "Garantizar continuidad operativa y cumplimiento institucional.",
            requestType = 2,
            priority = 2,
            requestingUnitId = 3,
            requesterUserId = userId,
            desiredDate = (string?)null,
            specificPayloadJson = ValidSoftwarePayloadJson
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedRequestStub>();
        Assert.NotNull(created);

        var submitResponse = await client.PostAsJsonAsync(
            $"/api/v1/requests/{created!.RequestId}/transitions",
            new { transition = "Submit", reason = (string?)null });
        Assert.Equal(HttpStatusCode.NoContent, submitResponse.StatusCode);

        var cancelResponse = await client.PostAsJsonAsync(
            $"/api/v1/requests/{created.RequestId}/transitions",
            new { transition = "CancelByRequester", reason = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, cancelResponse.StatusCode);
    }

    [Fact]
    public async Task Submit_WithWrongRole_Returns403()
    {
        var client = _factory.CreateClient();

        var userId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var token = await GetDevTokenAsync(client, userId, "TicAnalyst");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createResponse = await client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "Solicitud de licencias Office",
            description = "Se requiere licencia para personal de nueva contratación en la unidad.",
            businessJustification = "Garantizar continuidad operativa y cumplimiento institucional.",
            requestType = 2,
            priority = 2,
            requestingUnitId = 3,
            requesterUserId = userId,
            desiredDate = (string?)null,
            specificPayloadJson = (string?)null
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedRequestStub>();
        Assert.NotNull(created);

        var transitionResponse = await client.PostAsJsonAsync(
            $"/api/v1/requests/{created!.RequestId}/transitions",
            new { transition = "Submit", reason = (string?)null });

        Assert.Equal(HttpStatusCode.Forbidden, transitionResponse.StatusCode);
    }

    [Fact]
    public async Task Transition_Returns409_WhenRepositoryReportsConcurrencyConflict()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                ServiceCollectionTestExtensions.RemoveServiceDescriptors(services, typeof(DbContextOptions<SolicitudesTechGovDbContext>));
                ServiceCollectionTestExtensions.RemoveServiceDescriptors(services, typeof(SolicitudesTechGovDbContext));

                ServiceCollectionTestExtensions.RemoveServiceDescriptors(services, typeof(IRequestRepository));
                ServiceCollectionTestExtensions.RemoveServiceDescriptors(services, typeof(IAuditLogRepository));
                ServiceCollectionTestExtensions.RemoveServiceDescriptors(services, typeof(IRequestAttachmentRepository));
                ServiceCollectionTestExtensions.RemoveServiceDescriptors(services, typeof(LocalAttachmentFileStore));
                ServiceCollectionTestExtensions.RemoveServiceDescriptors(services, typeof(IRequestCommentRepository));
                ServiceCollectionTestExtensions.RemoveServiceDescriptors(services, typeof(IUserNotificationRepository));
                ServiceCollectionTestExtensions.RemoveServiceDescriptors(services, typeof(IUnitOfWork));
                ServiceCollectionTestExtensions.RemoveServiceDescriptors(services, typeof(IOrganizationalUnitCatalogRepository));
                ServiceCollectionTestExtensions.RemoveServiceDescriptors(services, typeof(IAppUserAdminRepository));
                ServiceCollectionTestExtensions.RemoveServiceDescriptors(services, typeof(IUserAuthenticationService));

                services.AddSingleton<IRequestRepository, ConcurrencyConflictRequestRepository>();
                services.AddSingleton<IAuditLogRepository, NoOpAuditForConflict>();
                services.AddSingleton<IRequestAttachmentRepository, NoOpAttachmentRepositoryStub>();
                services.AddSingleton<IRequestCommentRepository, NoOpRequestCommentRepositoryStub>();
                services.AddSingleton<IUserNotificationRepository, NoOpUserNotificationRepositoryStub>();
                services.AddSingleton<IUnitOfWork, NoOpUnitOfWorkConflict>();
                services.AddSingleton<IOrganizationalUnitCatalogRepository, InMemoryOrganizationalUnitCatalogRepository>();
                services.AddSingleton<IAppUserAdminRepository, InMemoryAppUserAdminRepository>();
                services.AddSingleton<IUserAuthenticationService, TransitionNoOpUserAuthenticationService>();
            });
        });

        var client = factory.CreateClient();

        var userId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var token = await GetDevTokenAsync(client, userId, "Requester");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createResponse = await client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "Solicitud de licencias Office",
            description = "Se requiere licencia para personal de nueva contratación en la unidad.",
            businessJustification = "Garantizar continuidad operativa y cumplimiento institucional.",
            requestType = 2,
            priority = 2,
            requestingUnitId = 3,
            requesterUserId = userId,
            desiredDate = (string?)null,
            specificPayloadJson = (string?)null
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedRequestStub>();
        Assert.NotNull(created);

        var transitionResponse = await client.PostAsJsonAsync(
            $"/api/v1/requests/{created!.RequestId}/transitions",
            new { transition = "Submit", reason = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, transitionResponse.StatusCode);
    }

    private static async Task<string> GetDevTokenAsync(HttpClient client, Guid userId, string role)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/dev-token", new { userId, role });
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("accessToken").GetString() ?? throw new InvalidOperationException();
    }

    private sealed record CreatedRequestStub([property: JsonPropertyName("requestId")] Guid RequestId);

    private sealed class ConcurrencyConflictRequestRepository : IRequestRepository
    {
        public Task AddAsync(Request request, CancellationToken cancellationToken)
        {
            _ = request;
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task<RequestDto?> GetByIdAsync(Guid requestId, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return Task.FromResult<RequestDto?>(new RequestDto(
                requestId,
                "Solicitud de licencias Office",
                "Se requiere licencia para personal de nueva contratación en la unidad.",
                "Garantizar continuidad operativa y cumplimiento institucional.",
                2,
                2,
                3,
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                RequestStatus.Draft.ToString(),
                null,
                DateTime.UtcNow,
                """{"productName":"Office","licenseModel":"Subscription","seatOrUserCount":10,"environment":"Production"}"""));
        }

        public Task<bool> TryUpdateStatusAsync(
            Guid requestId,
            string expectedCurrentStatus,
            string nextStatus,
            DateTime updatedAtUtc,
            DateTime? submittedAtUtc,
            CancellationToken cancellationToken)
        {
            _ = requestId;
            _ = expectedCurrentStatus;
            _ = nextStatus;
            _ = updatedAtUtc;
            _ = submittedAtUtc;
            _ = cancellationToken;
            return Task.FromResult(false);
        }

        public Task<PagedResult<RequestDto>> ListAsync(
            string? status,
            Guid? requesterUserId,
            DateTime? createdFromUtc,
            DateTime? createdToUtc,
            int page,
            int pageSize,
            string? sortBy,
            string? sortDirection,
            CancellationToken cancellationToken)
        {
            _ = status;
            _ = requesterUserId;
            _ = createdFromUtc;
            _ = createdToUtc;
            _ = page;
            _ = pageSize;
            _ = sortBy;
            _ = sortDirection;
            _ = cancellationToken;
            return Task.FromResult(new PagedResult<RequestDto>(Array.Empty<RequestDto>(), 0, 1, 10));
        }

        public Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(
            Guid? requesterUserIdScopeOnly,
            CancellationToken cancellationToken)
        {
            _ = requesterUserIdScopeOnly;
            _ = cancellationToken;
            return Task.FromResult<IReadOnlyDictionary<string, int>>(
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
        }

        public Task<bool> TryUpdateDraftAsync(
            Guid requestId,
            string title,
            string description,
            string businessJustification,
            byte requestTypeId,
            byte priorityId,
            int requestingUnitId,
            DateOnly? desiredDate,
            string? specificPayloadJson,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken)
        {
            _ = requestId;
            _ = title;
            _ = description;
            _ = businessJustification;
            _ = requestTypeId;
            _ = priorityId;
            _ = requestingUnitId;
            _ = desiredDate;
            _ = specificPayloadJson;
            _ = updatedAtUtc;
            _ = cancellationToken;
            return Task.FromResult(false);
        }

        public Task<RequestNotificationInfo?> GetNotificationInfoAsync(Guid requestId, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return Task.FromResult<RequestNotificationInfo?>(
                new RequestNotificationInfo(
                    requestId,
                    "Solicitud de licencias Office",
                    Guid.Parse("55555555-5555-5555-5555-555555555555"),
                    null,
                    null));
        }
    }

    private sealed class NoOpAuditForConflict : IAuditLogRepository
    {
        public Task<PagedResult<AdminAuditLogEntryDto>> ListGlobalAsync(
            int page,
            int pageSize,
            string? entityType,
            string? action,
            CancellationToken cancellationToken)
        {
            _ = entityType;
            _ = action;
            _ = cancellationToken;
            return Task.FromResult(new PagedResult<AdminAuditLogEntryDto>(
                Array.Empty<AdminAuditLogEntryDto>(),
                0,
                page,
                pageSize));
        }

        public Task<IReadOnlyList<RequestAuditEntryDto>> ListForRequestAsync(
            Guid requestId,
            CancellationToken cancellationToken)
        {
            _ = requestId;
            _ = cancellationToken;
            return Task.FromResult<IReadOnlyList<RequestAuditEntryDto>>(Array.Empty<RequestAuditEntryDto>());
        }

        public Task AddRequestCreatedAsync(Guid requestId, Guid actorUserId, byte toStatusId, CancellationToken cancellationToken)
        {
            _ = requestId;
            _ = actorUserId;
            _ = toStatusId;
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task AddRequestTransitionAsync(
            Guid requestId,
            Guid actorUserId,
            string actorRole,
            string transition,
            byte fromStatusId,
            byte toStatusId,
            string? reason,
            CancellationToken cancellationToken)
        {
            _ = requestId;
            _ = actorUserId;
            _ = actorRole;
            _ = transition;
            _ = fromStatusId;
            _ = toStatusId;
            _ = reason;
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task AddAdminCatalogEventAsync(
            Guid actorUserId,
            string actorRole,
            string action,
            string entityType,
            string entityId,
            string? payloadSummary,
            CancellationToken cancellationToken)
        {
            _ = actorUserId;
            _ = actorRole;
            _ = action;
            _ = entityType;
            _ = entityId;
            _ = payloadSummary;
            _ = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpUnitOfWorkConflict : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TransitionNoOpUserAuthenticationService : IUserAuthenticationService
    {
        public Task<LoginOutcome> TryLoginAsync(string email, string password, CancellationToken cancellationToken)
        {
            _ = email;
            _ = password;
            _ = cancellationToken;
            return Task.FromResult<LoginOutcome>(new LoginFailed(LoginFailureReason.InvalidCredentials));
        }
    }
}
