using SolicitudesTechGov.Application.Catalogs.Dtos;
using SolicitudesTechGov.Application.Requests.Dtos;

namespace SolicitudesTechGov.Application.Abstractions;

public interface IAppUserAdminRepository
{
    Task<IReadOnlyList<AppRoleOptionDto>> ListRolesAsync(CancellationToken cancellationToken);

    /// <summary>Roles distintos asignados al usuario (orden por SortOrder).</summary>
    Task<IReadOnlyList<AppRoleOptionDto>> ListRolesForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<PagedResult<AppUserListItemDto>> ListUsersAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<AppUserDetailDto?> GetUserDetailAsync(Guid userId, CancellationToken cancellationToken);

    Task<Guid> CreateUserAsync(string email, string displayName, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string email, Guid? exceptUserId, CancellationToken cancellationToken);

    /// <summary>Reemplaza todas las filas de <c>UserRole</c> del usuario.</summary>
    Task ReplaceUserRolesAsync(
        Guid userId,
        IReadOnlyList<(byte RoleId, int? OrganizationalUnitId)> assignments,
        CancellationToken cancellationToken);
}
