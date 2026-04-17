namespace SolicitudesTechGov.Application.Requests.Dtos;

public sealed record RequestDto(
    Guid RequestId,
    string Title,
    string Description,
    string BusinessJustification,
    byte RequestType,
    byte Priority,
    int RequestingUnitId,
    Guid RequesterUserId,
    string Status,
    DateOnly? DesiredDate,
    DateTime CreatedAtUtc,
    string? SpecificPayloadJson);
