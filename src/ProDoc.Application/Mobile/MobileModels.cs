namespace ProDoc.Application.Mobile;

public sealed record PushDeviceDetails(Guid Id, string Platform, string AppVersion, bool IsActive, DateTimeOffset UpdatedAtUtc);

public sealed record RegisterPushDeviceRequest(string Platform, string Token, string? AppVersion);

public static class NotificationDeepLinks
{
    public static string Resolve(string kind, Guid? subjectId) =>
        kind switch
        {
            var value when value.StartsWith("appointment", StringComparison.OrdinalIgnoreCase) => "/visits",
            var value when value.StartsWith("follow_up", StringComparison.OrdinalIgnoreCase) => "/visits",
            var value when value.StartsWith("queue", StringComparison.OrdinalIgnoreCase) => "/queue",
            var value when value.Equals("check_in", StringComparison.OrdinalIgnoreCase) => "/queue",
            var value when value.StartsWith("prescription", StringComparison.OrdinalIgnoreCase) =>
                subjectId is Guid id ? $"/prescription/{id:D}" : "/health-records",
            var value when value.StartsWith("payment", StringComparison.OrdinalIgnoreCase) => "/visits",
            var value when value.StartsWith("campaign", StringComparison.OrdinalIgnoreCase) => "/home",
            _ => "/home"
        };

    /// <summary>
    /// Lock-screen / banner copy must never include diagnoses, Rx lines, or amounts.
    /// </summary>
    public static (string Title, string Body) SanitizeForPush(string kind, string title)
    {
        var safeTitle = title.Length <= 48 ? title : "ProDoc";
        var body = kind.ToLowerInvariant() switch
        {
            var value when value.Contains("appointment") => "Your visit was updated. Open ProDoc for details.",
            var value when value.Contains("follow_up") => "You have a follow-up update. Open ProDoc for details.",
            var value when value.Contains("queue") => "Your queue status changed. Open ProDoc for details.",
            var value when value.Contains("prescription") => "A prescription is available in ProDoc.",
            var value when value.Contains("payment") => "A payment update is available in ProDoc.",
            _ => "Open ProDoc for a secure update. Details are not shown here."
        };
        return (safeTitle, body);
    }
}
