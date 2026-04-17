using SolicitudesTechGov.Application.Catalogs.Dtos;

namespace SolicitudesTechGov.Application.Abstractions;

public interface IOrganizationalUnitCatalogRepository
{
    Task<IReadOnlyList<OrganizationalUnitDto>> ListAsync(bool activeOnly, CancellationToken cancellationToken);

    Task<int> CreateAsync(string code, string name, CancellationToken cancellationToken);

    Task<bool> TryUpdateAsync(int unitId, string? code, string? name, bool? isActive, CancellationToken cancellationToken);

    Task<bool> HasRequestsReferencingUnitAsync(int unitId, CancellationToken cancellationToken);
}
