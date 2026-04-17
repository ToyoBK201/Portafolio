namespace SolicitudesTechGov.Application.Requests.Commands;

public sealed record CreateDraftRequestCommand(
    string Title,
    string Description,
    string BusinessJustification,
    byte RequestType,
    byte Priority,
    int RequestingUnitId,
    Guid RequesterUserId,
    DateOnly? DesiredDate,
    string? SpecificPayloadJson);
