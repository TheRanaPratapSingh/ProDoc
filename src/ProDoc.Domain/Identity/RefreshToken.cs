namespace ProDoc.Domain.Identity;

public sealed class RefreshToken
{
    public const int MaxDeviceNameLength = 80;

    private RefreshToken()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid FamilyId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public string? DeviceName { get; private set; }

    public bool WasRotatedOrRevoked => RevokedAtUtc is not null || ReplacedByTokenId is not null;

    public static RefreshToken Issue(
        Guid userId,
        Guid familyId,
        string tokenHash,
        DateTimeOffset now,
        DateTimeOffset expiresAtUtc,
        string? deviceName)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A user is required.", nameof(userId));
        }

        if (familyId == Guid.Empty)
        {
            throw new ArgumentException("A token family is required.", nameof(familyId));
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("A token hash is required.", nameof(tokenHash));
        }

        if (expiresAtUtc <= now)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAtUtc));
        }

        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FamilyId = familyId,
            TokenHash = tokenHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = expiresAtUtc,
            DeviceName = NormalizeDeviceName(deviceName)
        };
    }

    public bool IsActive(DateTimeOffset now) =>
        RevokedAtUtc is null && ReplacedByTokenId is null && ExpiresAtUtc > now;

    public void Revoke(DateTimeOffset now, Guid? replacedByTokenId)
    {
        RevokedAtUtc ??= now;
        if (replacedByTokenId is not null)
        {
            ReplacedByTokenId ??= replacedByTokenId;
        }
    }

    private static string? NormalizeDeviceName(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return null;
        }

        var trimmed = deviceName.Trim();
        if (trimmed.Length > MaxDeviceNameLength)
        {
            throw new ArgumentException($"Device name must be {MaxDeviceNameLength} characters or fewer.", nameof(deviceName));
        }

        return trimmed;
    }
}
