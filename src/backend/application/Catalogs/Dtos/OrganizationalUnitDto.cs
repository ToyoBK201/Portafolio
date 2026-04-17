namespace SolicitudesTechGov.Application.Catalogs.Dtos;

public sealed record OrganizationalUnitDto(
    int UnitId,
    string Code,
    string Name,
    bool IsActive,
    DateTime CreatedAtUtc);
