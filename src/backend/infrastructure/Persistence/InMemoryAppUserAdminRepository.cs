using System.Collections.Concurrent;
using SolicitudesTechGov.Application.Abstractions;
using SolicitudesTechGov.Application.Catalogs.Dtos;
using SolicitudesTechGov.Application.Requests.Dtos;

namespace SolicitudesTechGov.Infrastructure.Persistence;

/// <summary>Usuarios y roles en memoria (pruebas API sin SQL).</summary>
public sealed class InMemoryAppUserAdminRepository : IAppUserAdminRepository
{
    private static readonly IReadOnlyList<AppRoleOptionDto> StaticRoles =
        new List<AppRoleOptionDto>
        {
            new(1, "Requester", "Solicitante", 1),
            new(2, "AreaCoordinator", "Coordinador de área", 2),
            new(3, "TicAnalyst", "Analista TIC", 3),
            new(4, "InstitutionalApprover", "Aprobador institucional", 4),
            new(5, "Implementer", "Implementador", 5),
            new(6, "SystemAdministrator", "Administrador del sistema", 6),
            new(7, "Auditor", "Auditor", 7)
        };

    private readonly ConcurrentDictionary<Guid, (string Email, string DisplayName, bool IsActive, DateTime CreatedAtUtc)> _users = new();
    private readonly ConcurrentDictionary<Guid, List<(byte RoleId, int? OrganizationalUnitId)>> _userRoles = new();

    public InMemoryAppUserAdminRepository()
    {
        var seed = Guid.Parse("11111111-1111-1111-1111-111111111111");
        _users[seed] = ("11111111-1111-1111-1111-111111111111@local.test", "Usuario MVP", true, DateTime.UtcNow);
        _userRoles[seed] = new List<(byte, int?)> { (1, null) };
    }

    public Task<IReadOnlyList<AppRoleOptionDto>> ListRolesAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(StaticRoles);
    }

    public Task<IReadOnlyList<AppRoleOptionDto>> ListRolesForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (!_userRoles.TryGetValue(userId, out var list) || list.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<AppRoleOptionDto>>(Array.Empty<AppRoleOptionDto>());
        }

        var byId = list.Select(x => x.RoleId).Distinct().ToHashSet();
        var result = StaticRoles
            .Where(r => byId.Contains(r.RoleId))
            .OrderBy(r => r.SortOrder)
            .ToList();
        return Task.FromResult<IReadOnlyList<AppRoleOptionDto>>(result);
    }

    public Task<PagedResult<AppUserListItemDto>> ListUsersAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var all = _users
            .OrderByDescending(x => x.Value.CreatedAtUtc)
            .Select(x => new AppUserListItemDto(x.Key, x.Value.Email, x.Value.DisplayName, x.Value.IsActive, x.Value.CreatedAtUtc))
            .ToList();
        var total = all.Count;
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(new PagedResult<AppUserListItemDto>(items, total, page, pageSize));
    }

    public Task<AppUserDetailDto?> GetUserDetailAsync(Guid userId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (!_users.TryGetValue(userId, out var u))
        {
            return Task.FromResult<AppUserDetailDto?>(null);
        }

        var roles = new List<RoleAssignmentDto>();
        if (_userRoles.TryGetValue(userId, out var list))
        {
            foreach (var (roleId, unitId) in list)
            {
                var meta = StaticRoles.First(r => r.RoleId == roleId);
                roles.Add(new RoleAssignmentDto(roleId, meta.Code, meta.LabelEs, unitId));
            }
        }

        return Task.FromResult<AppUserDetailDto?>(
            new AppUserDetailDto(userId, u.Email, u.DisplayName, u.IsActive, u.CreatedAtUtc, roles));
    }

    public Task<Guid> CreateUserAsync(string email, string displayName, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var id = Guid.NewGuid();
        _users[id] = (email.Trim(), displayName.Trim(), true, DateTime.UtcNow);
        _userRoles[id] = new List<(byte, int?)>();
        return Task.FromResult(id);
    }

    public Task<bool> EmailExistsAsync(string email, Guid? exceptUserId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var n = email.Trim();
        foreach (var kv in _users)
        {
            if (exceptUserId.HasValue && kv.Key == exceptUserId.Value)
            {
                continue;
            }

            if (string.Equals(kv.Value.Email, n, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    public Task ReplaceUserRolesAsync(
        Guid userId,
        IReadOnlyList<(byte RoleId, int? OrganizationalUnitId)> assignments,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (!_users.ContainsKey(userId))
        {
            throw new InvalidOperationException("User not found.");
        }

        foreach (var (roleId, _) in assignments)
        {
            if (StaticRoles.All(r => r.RoleId != roleId))
            {
                throw new InvalidOperationException($"Unknown role id {roleId}.");
            }
        }

        _userRoles[userId] = assignments.ToList();
        return Task.CompletedTask;
    }
}
