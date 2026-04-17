using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SolicitudesTechGov.Api.Auth;

internal static class ClaimsPrincipalExtensions
{
    private const string SystemAdministratorRole = "SystemAdministrator";

    internal static Guid? GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    internal static bool IsSystemAdministrator(this ClaimsPrincipal user) =>
        user.IsInRole(SystemAdministratorRole);

    internal static bool IsAuditor(this ClaimsPrincipal user) =>
        user.IsInRole("Auditor");

    /// <summary>Listado global de <c>AuditLog</c> (docs/05): administrador o auditor.</summary>
    internal static bool CanViewGlobalAuditLog(this ClaimsPrincipal user) =>
        user.IsSystemAdministrator() || user.IsAuditor();

    /// <summary>
    /// Lectura de solicitud: administrador, solicitante dueño, o cualquier rol distinto de Requester (cola operativa / matriz §2).
    /// </summary>
    internal static bool CanViewRequestContent(this ClaimsPrincipal user, Guid requestOwnerUserId)
    {
        if (user.IsSystemAdministrator())
        {
            return true;
        }

        if (user.GetUserId() == requestOwnerUserId)
        {
            return true;
        }

        return !string.Equals(user.GetPrimaryRole(), "Requester", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Listado/export: solo Requester queda limitado a su propio UserId; el resto puede filtrar opcionalmente o ver todo.
    /// </summary>
    /// <summary>Actualizar borrador: solicitante/coordinador dueño o administrador (docs/02 §1).</summary>
    internal static bool CanEditDraftRequest(this ClaimsPrincipal user, Guid requestOwnerUserId)
    {
        if (user.IsSystemAdministrator())
        {
            return true;
        }

        var role = user.GetPrimaryRole();
        if (!string.Equals(role, "Requester", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(role, "AreaCoordinator", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return user.GetUserId() == requestOwnerUserId;
    }

    internal static bool TryResolveListRequesterScope(
        this ClaimsPrincipal user,
        Guid? userId,
        Guid? requesterUserIdFromQuery,
        out Guid? effectiveRequesterUserId)
    {
        effectiveRequesterUserId = null;

        if (user.IsSystemAdministrator())
        {
            effectiveRequesterUserId = requesterUserIdFromQuery;
            return true;
        }

        if (string.Equals(user.GetPrimaryRole(), "Requester", StringComparison.OrdinalIgnoreCase))
        {
            if (requesterUserIdFromQuery.HasValue && requesterUserIdFromQuery.Value != userId)
            {
                return false;
            }

            effectiveRequesterUserId = userId;
            return true;
        }

        effectiveRequesterUserId = requesterUserIdFromQuery;
        return true;
    }

    internal static string GetPrimaryRole(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.Role) ?? "Requester";
    }

    /// <summary>Origen de la sesión: <see cref="JwtAuthMethodClaims.Password"/> o <see cref="JwtAuthMethodClaims.Dev"/>.</summary>
    internal static string? GetAuthMethod(this ClaimsPrincipal user) =>
        user.FindFirstValue(JwtAuthMethodClaims.ClaimType);

    /// <summary>Comentario visible al solicitante (GET sin filtro de internos).</summary>
    internal static bool CanPostPublicComment(this ClaimsPrincipal user)
    {
        if (user.IsSystemAdministrator())
        {
            return true;
        }

        return user.GetPrimaryRole() switch
        {
            "Requester" or "AreaCoordinator" or "TicAnalyst" or "InstitutionalApprover" or "Implementer" => true,
            _ => false
        };
    }

    /// <summary>Nota interna: Ana, Apr, Imp, Adm (docs/02 §2.4).</summary>
    internal static bool CanPostInternalComment(this ClaimsPrincipal user)
    {
        if (user.IsSystemAdministrator())
        {
            return true;
        }

        return user.GetPrimaryRole() switch
        {
            "TicAnalyst" or "InstitutionalApprover" or "Implementer" => true,
            _ => false
        };
    }

    /// <summary>
    /// El solicitante no debe ver comentarios internos; el resto de roles con acceso al detalle sí.
    /// </summary>
    internal static bool ExcludeInternalCommentsFromResponse(this ClaimsPrincipal user)
    {
        if (user.IsSystemAdministrator())
        {
            return false;
        }

        return string.Equals(user.GetPrimaryRole(), "Requester", StringComparison.OrdinalIgnoreCase);
    }
}
