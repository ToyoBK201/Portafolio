namespace SolicitudesTechGov.Application.Requests.Commands;

public sealed record UpdateDraftRequestCommand(
    Guid RequestId,
    string Title,
    string Description,
    string BusinessJustification,
    byte RequestType,
    byte Priority,
    int RequestingUnitId,
    DateOnly? DesiredDate,
    string? SpecificPayloadJson);
