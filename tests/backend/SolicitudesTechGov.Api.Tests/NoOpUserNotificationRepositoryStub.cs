using SolicitudesTechGov.Application.Abstractions;
using SolicitudesTechGov.Application.Requests.Dtos;

namespace SolicitudesTechGov.Api.Tests;

internal sealed class NoOpUserNotificationRepositoryStub : IUserNotificationRepository
{
    public Task AddAsync(
        Guid recipientUserId,
        Guid? requestId,
        string title,
        string message,
        string category,
        CancellationToken cancellationToken)
    {
        _ = recipientUserId;
        _ = requestId;
        _ = title;
        _ = message;
        _ = category;
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<UserNotificationDto>> ListForUserAsync(
        Guid forUserId,
        int take,
        bool unreadOnly,
        CancellationToken cancellationToken)
    {
        _ = forUserId;
        _ = take;
        _ = unreadOnly;
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<UserNotificationDto>>(Array.Empty<UserNotificationDto>());
    }

    public Task<int> CountUnreadAsync(Guid forUserId, CancellationToken cancellationToken)
    {
        _ = forUserId;
        _ = cancellationToken;
        return Task.FromResult(0);
    }

    public Task<bool> TryMarkAsReadAsync(Guid forUserId, Guid notificationId, CancellationToken cancellationToken)
    {
        _ = forUserId;
        _ = notificationId;
        _ = cancellationToken;
        return Task.FromResult(false);
    }

    public Task<int> MarkAllAsReadAsync(Guid forUserId, CancellationToken cancellationToken)
    {
        _ = forUserId;
        _ = cancellationToken;
        return Task.FromResult(0);
    }
}
