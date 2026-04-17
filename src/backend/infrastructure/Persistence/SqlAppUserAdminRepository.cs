using Microsoft.EntityFrameworkCore;
using SolicitudesTechGov.Application.Abstractions;
using SolicitudesTechGov.Application.Catalogs.Dtos;
using SolicitudesTechGov.Application.Requests.Dtos;

namespace SolicitudesTechGov.Infrastructure.Persistence;

public sealed class SqlAppUserAdminRepository : IAppUserAdminRepository
{
    private readonly SolicitudesTechGovDbContext _dbContext;

    public SqlAppUserAdminRepository(SolicitudesTechGovDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<AppRoleOptionDto>> ListRolesAsync(CancellationToken cancellationToken)
    {
        var list = await _dbContext.AppRoles
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .Select(x => new AppRoleOptionDto(x.RoleId, x.Code, x.LabelEs, x.SortOrder))
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyList<AppRoleOptionDto>> ListRolesForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var roleIds = await _dbContext.UserRoles
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.RoleId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (roleIds.Count == 0)
        {
            return Array.Empty<AppRoleOptionDto>();
        }

        return await _dbContext.AppRoles
            .AsNoTracking()
            .Where(x => roleIds.Contains(x.RoleId))
            .OrderBy(x => x.SortOrder)
            .Select(x => new AppRoleOptionDto(x.RoleId, x.Code, x.LabelEs, x.SortOrder))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<AppUserListItemDto>> ListUsersAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var q = _dbContext.AppUsers.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc);
        var total = await q.CountAsync(cancellationToken);
        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AppUserListItemDto(x.UserId, x.Email, x.DisplayName, x.IsActive, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<AppUserListItemDto>(items, total, page, pageSize);
    }

    public async Task<AppUserDetailDto?> GetUserDetailAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _dbContext.AppUsers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var roles = await (
                from ur in _dbContext.UserRoles.AsNoTracking()
                join ar in _dbContext.AppRoles.AsNoTracking() on ur.RoleId equals ar.RoleId
                where ur.UserId == userId
                orderby ar.SortOrder
                select new RoleAssignmentDto(ur.RoleId, ar.Code, ar.LabelEs, ur.OrganizationalUnitId))
            .ToListAsync(cancellationToken);

        return new AppUserDetailDto(
            user.UserId,
            user.Email,
            user.DisplayName,
            user.IsActive,
            user.CreatedAtUtc,
            roles);
    }

    public async Task<Guid> CreateUserAsync(string email, string displayName, CancellationToken cancellationToken)
    {
        var user = new AppUserRecord
        {
            UserId = Guid.NewGuid(),
            Email = email.Trim(),
            DisplayName = displayName.Trim(),
            ExternalSubjectId = null,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _dbContext.AppUsers.AddAsync(user, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return user.UserId;
    }

    public Task<bool> EmailExistsAsync(string email, Guid? exceptUserId, CancellationToken cancellationToken)
    {
        var normalized = email.Trim();
        var q = _dbContext.AppUsers.AsNoTracking().Where(x => x.Email == normalized);
        if (exceptUserId.HasValue)
        {
            q = q.Where(x => x.UserId != exceptUserId.Value);
        }

        return q.AnyAsync(cancellationToken);
    }

    public async Task ReplaceUserRolesAsync(
        Guid userId,
        IReadOnlyList<(byte RoleId, int? OrganizationalUnitId)> assignments,
        CancellationToken cancellationToken)
    {
        var validRoleIds = await _dbContext.AppRoles.AsNoTracking().Select(x => x.RoleId).ToListAsync(cancellationToken);
        var validSet = validRoleIds.ToHashSet();
        foreach (var (roleId, _) in assignments)
        {
            if (!validSet.Contains(roleId))
            {
                throw new InvalidOperationException($"Unknown role id {roleId}.");
            }
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _dbContext.UserRoles
            .Where(x => x.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var (roleId, orgUnitId) in assignments)
        {
            if (orgUnitId.HasValue)
            {
                var unitOk = await _dbContext.OrganizationalUnits.AsNoTracking()
                    .AnyAsync(x => x.UnitId == orgUnitId.Value && x.IsActive, cancellationToken);
                if (!unitOk)
                {
                    throw new InvalidOperationException($"Organizational unit {orgUnitId} is invalid or inactive.");
                }
            }

            await _dbContext.UserRoles.AddAsync(
                new UserRoleRecord
                {
                    UserId = userId,
                    RoleId = roleId,
                    OrganizationalUnitId = orgUnitId,
                    AssignedAtUtc = now
                },
                cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }
}
