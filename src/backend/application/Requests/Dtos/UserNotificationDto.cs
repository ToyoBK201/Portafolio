namespace SolicitudesTechGov.Application.Requests.Dtos;

public sealed record UserNotificationDto(
    Guid NotificationId,
    Guid? RequestId,
    string Title,
    string Message,
    string Category,
    bool IsRead,
    DateTime CreatedAtUtc);
