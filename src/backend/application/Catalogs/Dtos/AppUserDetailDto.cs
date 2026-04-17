namespace SolicitudesTechGov.Application.Catalogs.Dtos;

public sealed record AppUserDetailDto(
    Guid UserId,
    string Email,
    string DisplayName,
    bool IsActive,
    DateTime CreatedAtUtc,
    IReadOnlyList<RoleAssignmentDto> Roles);
