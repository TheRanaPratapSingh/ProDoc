using ProDoc.Domain.Common;

namespace ProDoc.Domain.Mobile;

public sealed class PushDevice
{
    private PushDevice()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Platform { get; private set; } = string.Empty;

    public string Token { get; private set; } = string.Empty;

    public string AppVersion { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset RegisteredAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static PushDevice Register(Guid userId, string platform, string token, string appVersion, DateTimeOffset now)
    {
        var normalizedPlatform = platform.Trim().ToLowerInvariant();
        if (normalizedPlatform is not ("ios" or "android" or "web"))
        {
            throw DomainException.Validation("push_platform_invalid", "Choose ios, android, or web.");
        }

        var normalizedToken = token.Trim();
        if (normalizedToken.Length is < 16 or > 512)
        {
            throw DomainException.Validation("push_token_invalid", "Provide a valid push token.");
        }

        return new PushDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Platform = normalizedPlatform,
            Token = normalizedToken,
            AppVersion = string.IsNullOrWhiteSpace(appVersion) ? "unknown" : appVersion.Trim()[..Math.Min(appVersion.Trim().Length, 40)],
            IsActive = true,
            RegisteredAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Refresh(string appVersion, DateTimeOffset now)
    {
        AppVersion = string.IsNullOrWhiteSpace(appVersion) ? AppVersion : appVersion.Trim()[..Math.Min(appVersion.Trim().Length, 40)];
        IsActive = true;
        UpdatedAtUtc = now;
    }

    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        UpdatedAtUtc = now;
    }
}
