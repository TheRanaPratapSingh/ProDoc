namespace ProDoc.Application.Notifications;

public interface INotificationRealtime
{
    Task PublishToUserAsync(Guid userId, string eventName, object payload, CancellationToken cancellationToken);
}
