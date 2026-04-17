using Microsoft.EntityFrameworkCore;
using SolicitudesTechGov.Application.Abstractions;
using SolicitudesTechGov.Application.Catalogs.Dtos;

namespace SolicitudesTechGov.Infrastructure.Persistence;

public sealed class SqlOrganizationalUnitCatalogRepository : IOrganizationalUnitCatalogRepository
{
    private readonly SolicitudesTechGovDbContext _dbContext;

    public SqlOrganizationalUnitCatalogRepository(SolicitudesTechGovDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<OrganizationalUnitDto>> ListAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var q = _dbContext.OrganizationalUnits.AsNoTracking().AsQueryable();
        if (activeOnly)
        {
            q = q.Where(x => x.IsActive);
        }

        return await q
            .OrderBy(x => x.Code)
            .Select(x => new OrganizationalUnitDto(x.UnitId, x.Code, x.Name, x.IsActive, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CreateAsync(string code, string name, CancellationToken cancellationToken)
    {
        var record = new OrganizationalUnitRecord
        {
            Code = code.Trim(),
            Name = name.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _dbContext.OrganizationalUnits.AddAsync(record, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return record.UnitId;
    }

    public async Task<bool> TryUpdateAsync(
        int unitId,
        string? code,
        string? name,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        var row = await _dbContext.OrganizationalUnits.FirstOrDefaultAsync(x => x.UnitId == unitId, cancellationToken);
        if (row is null)
        {
            return false;
        }

        if (code is not null)
        {
            row.Code = code.Trim();
        }

        if (name is not null)
        {
            row.Name = name.Trim();
        }

        if (isActive.HasValue)
        {
            row.IsActive = isActive.Value;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<bool> HasRequestsReferencingUnitAsync(int unitId, CancellationToken cancellationToken)
    {
        return _dbContext.Requests.AsNoTracking().AnyAsync(x => x.RequestingUnitId == unitId, cancellationToken);
    }
}
