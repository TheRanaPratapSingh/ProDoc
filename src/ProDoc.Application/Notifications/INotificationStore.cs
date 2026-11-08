using ProDoc.Domain.Automation;
using ProDoc.Domain.Queue;

namespace ProDoc.Application.Notifications;

public interface INotificationStore
{
    Task<UserNotification?> FindAsync(Guid id, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserNotification>> ListForUserAsync(
        Guid userId,
        bool? unreadOnly,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> HasRecentAsync(Guid userId, string kind, Guid? subjectId, DateTimeOffset since, CancellationToken cancellationToken);

    void Add(UserNotification notification);

    void Remove(UserNotification notification);

    Task<IReadOnlyList<UserNotification>> ListUnreadTrackedAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserNotificationPreference?> FindPreferenceAsync(Guid userId, bool tracking, CancellationToken cancellationToken);

    void AddPreference(UserNotificationPreference preference);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
