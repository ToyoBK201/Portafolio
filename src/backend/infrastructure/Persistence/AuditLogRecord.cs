namespace SolicitudesTechGov.Infrastructure.Persistence;

public sealed class AuditLogRecord
{
    public long AuditId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public Guid? CorrelationId { get; set; }
    public Guid ActorUserId { get; set; }
    public string ActorRole { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public Guid? RequestId { get; set; }
    public byte? FromStatusId { get; set; }
    public byte? ToStatusId { get; set; }
    public byte[]? ClientIp { get; set; }
    public string? UserAgent { get; set; }
    public string? PayloadSummary { get; set; }
    public string? PayloadDiff { get; set; }
    public bool Success { get; set; }
}
