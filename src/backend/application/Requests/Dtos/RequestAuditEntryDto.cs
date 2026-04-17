namespace SolicitudesTechGov.Application.Requests.Dtos;

public sealed record RequestAuditEntryDto(
    long AuditId,
    DateTime OccurredAtUtc,
    Guid? CorrelationId,
    Guid ActorUserId,
    string ActorRole,
    string Action,
    string? FromStatus,
    string? ToStatus,
    string? PayloadSummary);
