using Microsoft.AspNetCore.SignalR;
using ProDoc.Application.Notifications;

namespace ProDoc.Infrastructure.Notifications;

public sealed class SignalRNotificationRealtime(IHubContext<NotificationsHub> hubs) : INotificationRealtime
{
    public Task PublishToUserAsync(Guid userId, string eventName, object payload, CancellationToken cancellationToken) =>
        hubs.Clients.Group(NotificationsHub.UserGroup(userId)).SendAsync(eventName, payload, cancellationToken);
}
