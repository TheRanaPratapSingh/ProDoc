using Microsoft.AspNetCore.SignalR;
using ProDoc.Application.Queue;

namespace ProDoc.Infrastructure.Queue;

public sealed class SignalRQueueRealtime(IHubContext<QueueHub> hubs) : IQueueRealtime
{
    public Task PublishAsync(string group, string eventName, object payload, CancellationToken cancellationToken) =>
        hubs.Clients.Group(group).SendAsync(eventName, payload, cancellationToken);
}
