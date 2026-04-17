namespace SolicitudesTechGov.Infrastructure.Persistence;

public sealed class RequestAttachmentRecord
{
    public Guid AttachmentId { get; set; }
    public Guid RequestId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public Guid? ReplacesAttachmentId { get; set; }
}
