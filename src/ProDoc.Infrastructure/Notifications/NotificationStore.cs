using Microsoft.EntityFrameworkCore;
using ProDoc.Application.Notifications;
using ProDoc.Domain.Automation;
using ProDoc.Domain.Queue;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.Notifications;

public sealed class NotificationStore(ProDocDbContext db) : INotificationStore
{
    public Task<UserNotification?> FindAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.UserNotifications.AsQueryable() : db.UserNotifications.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<UserNotification>> ListForUserAsync(
        Guid userId,
        bool? unreadOnly,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = db.UserNotifications.AsNoTracking().Where(item => item.UserId == userId);
        if (unreadOnly is true)
        {
            query = query.Where(item => item.ReadAtUtc == null);
        }
        else if (unreadOnly is false)
        {
            query = query.Where(item => item.ReadAtUtc != null);
        }

        return await query
            .OrderByDescending(item => item.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        db.UserNotifications.AsNoTracking().CountAsync(
            item => item.UserId == userId && item.ReadAtUtc == null,
            cancellationToken);

    public Task<bool> HasRecentAsync(Guid userId, string kind, Guid? subjectId, DateTimeOffset since, CancellationToken cancellationToken) =>
        db.UserNotifications.AsNoTracking().AnyAsync(
            item => item.UserId == userId
                && item.Kind == kind
                && item.SubjectId == subjectId
                && item.CreatedAtUtc >= since,
            cancellationToken);

    public void Add(UserNotification notification) => db.UserNotifications.Add(notification);

    public void Remove(UserNotification notification) => db.UserNotifications.Remove(notification);

    public async Task<IReadOnlyList<UserNotification>> ListUnreadTrackedAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.UserNotifications
            .Where(item => item.UserId == userId && item.ReadAtUtc == null)
            .ToListAsync(cancellationToken);

    public Task<UserNotificationPreference?> FindPreferenceAsync(Guid userId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.UserNotificationPreferences.AsQueryable() : db.UserNotificationPreferences.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
    }

    public void AddPreference(UserNotificationPreference preference) => db.UserNotificationPreferences.Add(preference);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
