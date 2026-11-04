namespace ProDoc.Domain.Governance;

public sealed class AuditLog
{
    private AuditLog()
    {
    }

    public Guid Id { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    public static AuditLog Record(
        Guid? actorUserId,
        string action,
        DateTimeOffset now,
        string? ipAddress,
        string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("An action is required.", nameof(action));
        }

        return new AuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            Action = action.Trim(),
            OccurredAtUtc = now,
            IpAddress = Trim(ipAddress, 64),
            UserAgent = Trim(userAgent, 256)
        };
    }

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
