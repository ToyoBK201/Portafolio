namespace SolicitudesTechGov.Infrastructure.Persistence;

public sealed class UserNotificationRecord
{
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
    public Guid? RequestId { get; set; }
    public string Title { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Schema.Column("Message")]
    public string Message { get; set; } = string.Empty;
    public string Category { get; set; } = "RequestTransition";
    public DateTime? ReadAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
