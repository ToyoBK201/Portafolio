using Microsoft.EntityFrameworkCore;
using SolicitudesTechGov.Application.Abstractions;
using SolicitudesTechGov.Application.Requests.Dtos;

namespace SolicitudesTechGov.Infrastructure.Persistence;

public sealed class SqlUserNotificationRepository : IUserNotificationRepository
{
    private readonly SolicitudesTechGovDbContext _db;

    public SqlUserNotificationRepository(SolicitudesTechGovDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(
        Guid recipientUserId,
        Guid? requestId,
        string title,
        string message,
        string category,
        CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await _db.UserNotifications.AddAsync(
            new UserNotificationRecord
            {
                NotificationId = id,
                UserId = recipientUserId,
                RequestId = requestId,
                Title = title,
                Message = message,
                Category = category,
                ReadAtUtc = null,
                CreatedAtUtc = now
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<UserNotificationDto>> ListForUserAsync(
        Guid forUserId,
        int take,
        bool unreadOnly,
        CancellationToken cancellationToken)
    {
        if (take < 1)
        {
            take = 30;
        }

        if (take > 100)
        {
            take = 100;
        }

        var query = _db.UserNotifications.AsNoTracking().Where(x => x.UserId == forUserId);
        if (unreadOnly)
        {
            query = query.Where(x => x.ReadAtUtc == null);
        }

        var rows = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .Select(x => new UserNotificationDto(
                x.NotificationId,
                x.RequestId,
                x.Title,
                x.Message,
                x.Category,
                x.ReadAtUtc != null,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return rows;
    }

    public Task<int> CountUnreadAsync(Guid forUserId, CancellationToken cancellationToken) =>
        _db.UserNotifications.CountAsync(
            x => x.UserId == forUserId && x.ReadAtUtc == null,
            cancellationToken);

    public async Task<bool> TryMarkAsReadAsync(Guid forUserId, Guid notificationId, CancellationToken cancellationToken)
    {
        var affected = await _db.UserNotifications
            .Where(x => x.UserId == forUserId && x.NotificationId == notificationId && x.ReadAtUtc == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.ReadAtUtc, _ => DateTime.UtcNow),
                cancellationToken);
        return affected > 0;
    }

    public async Task<int> MarkAllAsReadAsync(Guid forUserId, CancellationToken cancellationToken) =>
        await _db.UserNotifications
            .Where(x => x.UserId == forUserId && x.ReadAtUtc == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.ReadAtUtc, _ => DateTime.UtcNow),
                cancellationToken);
}
