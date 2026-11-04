using Microsoft.Extensions.Logging;
using ProDoc.Application.Mobile;

namespace ProDoc.Infrastructure.Mobile;

/// <summary>
/// Development/sandbox push sink. Does not claim Expo or FCM production delivery.
/// </summary>
public sealed class DevelopmentPushSender(ILogger<DevelopmentPushSender> logger) : IPushNotificationSender
{
    public Task SendAsync(
        IReadOnlyList<string> tokens,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Sandbox push prepared for {Count} device(s). Title={Title} Path={Path} Kind={Kind}. No production push gateway is configured.",
            tokens.Count,
            title,
            data.GetValueOrDefault("path"),
            data.GetValueOrDefault("kind"));
        return Task.CompletedTask;
    }
}
