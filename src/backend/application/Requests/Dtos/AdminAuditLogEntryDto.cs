namespace SolicitudesTechGov.Application.Requests.Dtos;

/// <summary>Vista global de filas de <c>AuditLog</c> (docs/05, D1).</summary>
public sealed record AdminAuditLogEntryDto(
    long AuditId,
    DateTime OccurredAtUtc,
    Guid? CorrelationId,
    Guid ActorUserId,
    string ActorRole,
    string Action,
    string EntityType,
    string EntityId,
    Guid? RequestId,
    string? FromStatus,
    string? ToStatus,
    string? PayloadSummary,
    bool Success);
