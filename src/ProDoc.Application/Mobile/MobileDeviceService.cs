using ProDoc.Application.Abstractions;
using ProDoc.Application.Providers;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Mobile;

namespace ProDoc.Application.Mobile;

public sealed class MobileDeviceService(IMobileStore store, IPushNotificationSender push, IClock clock)
{
    public async Task<PushDeviceDetails> RegisterAsync(
        Guid userId,
        string platform,
        string token,
        string? appVersion,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var existing = await store.FindByTokenAsync(token.Trim(), true, cancellationToken);
        if (existing is null)
        {
            existing = PushDevice.Register(userId, platform, token, appVersion ?? "1.0.0", clock.UtcNow);
            store.Add(existing);
        }
        else if (existing.UserId != userId)
        {
            existing.Deactivate(clock.UtcNow);
            existing = PushDevice.Register(userId, platform, token, appVersion ?? "1.0.0", clock.UtcNow);
            store.Add(existing);
        }
        else
        {
            existing.Refresh(appVersion ?? existing.AppVersion, clock.UtcNow);
        }

        await store.AddAuditAsync(
            AuditLog.Record(userId, AuditActions.PushDeviceRegistered, clock.UtcNow, metadata.IpAddress, metadata.UserAgent),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return Map(existing);
    }

    public async Task UnregisterAsync(Guid userId, RequestMetadata metadata, CancellationToken cancellationToken)
    {
        var devices = await store.ListActiveForUserAsync(userId, cancellationToken);
        foreach (var device in devices)
        {
            var tracked = await store.FindByTokenAsync(device.Token, true, cancellationToken);
            tracked?.Deactivate(clock.UtcNow);
        }

        await store.AddAuditAsync(
            AuditLog.Record(userId, AuditActions.PushDeviceUnregistered, clock.UtcNow, metadata.IpAddress, metadata.UserAgent),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task DispatchUserPushAsync(
        Guid userId,
        string kind,
        Guid? subjectId,
        string title,
        CancellationToken cancellationToken)
    {
        var devices = await store.ListActiveForUserAsync(userId, cancellationToken);
        if (devices.Count == 0)
        {
            return;
        }

        var (safeTitle, safeBody) = NotificationDeepLinks.SanitizeForPush(kind, title);
        var path = NotificationDeepLinks.Resolve(kind, subjectId);
        var data = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["kind"] = kind,
            ["path"] = path
        };
        if (subjectId is Guid id)
        {
            data["subjectId"] = id.ToString("D");
        }

        await push.SendAsync(devices.Select(item => item.Token).ToArray(), safeTitle, safeBody, data, cancellationToken);
    }

    private static PushDeviceDetails Map(PushDevice device) =>
        new(device.Id, device.Platform, device.AppVersion, device.IsActive, device.UpdatedAtUtc);
}
