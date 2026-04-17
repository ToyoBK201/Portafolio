using SolicitudesTechGov.Application.Requests.Dtos;

namespace SolicitudesTechGov.Application.Abstractions;

public interface IUserNotificationRepository
{
    Task AddAsync(
        Guid recipientUserId,
        Guid? requestId,
        string title,
        string message,
        string category,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<UserNotificationDto>> ListForUserAsync(
        Guid forUserId,
        int take,
        bool unreadOnly,
        CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(Guid forUserId, CancellationToken cancellationToken);

    Task<bool> TryMarkAsReadAsync(Guid forUserId, Guid notificationId, CancellationToken cancellationToken);

    Task<int> MarkAllAsReadAsync(Guid forUserId, CancellationToken cancellationToken);
}
