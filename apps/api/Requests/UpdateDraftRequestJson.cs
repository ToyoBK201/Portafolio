namespace SolicitudesTechGov.Api.Requests;

public sealed record UpdateDraftRequestJson(
    string Title,
    string Description,
    string BusinessJustification,
    byte RequestType,
    byte Priority,
    int RequestingUnitId,
    DateOnly? DesiredDate,
    string? SpecificPayloadJson);
