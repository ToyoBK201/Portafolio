namespace SolicitudesTechGov.Application.Catalogs.Dtos;

public sealed record RoleAssignmentDto(
    byte RoleId,
    string RoleCode,
    string RoleLabelEs,
    int? OrganizationalUnitId);
