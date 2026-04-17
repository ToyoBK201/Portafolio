using System.Collections.Concurrent;
using SolicitudesTechGov.Application.Abstractions;
using SolicitudesTechGov.Application.Catalogs.Dtos;

namespace SolicitudesTechGov.Infrastructure.Persistence;

/// <summary>Catálogo de unidades en memoria (pruebas API sin SQL).</summary>
public sealed class InMemoryOrganizationalUnitCatalogRepository : IOrganizationalUnitCatalogRepository
{
    private readonly ConcurrentDictionary<int, OrganizationalUnitDto> _units = new();
    private int _nextId = 10;

    public InMemoryOrganizationalUnitCatalogRepository()
    {
        _units[1] = new OrganizationalUnitDto(1, "U-001", "Unidad de prueba", true, DateTime.UtcNow);
        _units[2] = new OrganizationalUnitDto(2, "U-002", "Otra unidad", true, DateTime.UtcNow);
        _nextId = 3;
    }

    public Task<IReadOnlyList<OrganizationalUnitDto>> ListAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        IEnumerable<OrganizationalUnitDto> q = _units.Values.OrderBy(x => x.Code);
        if (activeOnly)
        {
            q = q.Where(x => x.IsActive);
        }

        return Task.FromResult<IReadOnlyList<OrganizationalUnitDto>>(q.ToList());
    }

    public Task<int> CreateAsync(string code, string name, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var id = Interlocked.Increment(ref _nextId);
        var dto = new OrganizationalUnitDto(id, code.Trim(), name.Trim(), true, DateTime.UtcNow);
        _units[id] = dto;
        return Task.FromResult(id);
    }

    public Task<bool> TryUpdateAsync(
        int unitId,
        string? code,
        string? name,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (!_units.TryGetValue(unitId, out var current))
        {
            return Task.FromResult(false);
        }

        _units[unitId] = new OrganizationalUnitDto(
            unitId,
            code is not null ? code.Trim() : current.Code,
            name is not null ? name.Trim() : current.Name,
            isActive ?? current.IsActive,
            current.CreatedAtUtc);
        return Task.FromResult(true);
    }

    public Task<bool> HasRequestsReferencingUnitAsync(int unitId, CancellationToken cancellationToken)
    {
        _ = unitId;
        _ = cancellationToken;
        return Task.FromResult(false);
    }
}
