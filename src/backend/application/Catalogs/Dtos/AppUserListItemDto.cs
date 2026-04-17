namespace SolicitudesTechGov.Application.Catalogs.Dtos;

public sealed record AppUserListItemDto(
    Guid UserId,
    string Email,
    string DisplayName,
    bool IsActive,
    DateTime CreatedAtUtc);
