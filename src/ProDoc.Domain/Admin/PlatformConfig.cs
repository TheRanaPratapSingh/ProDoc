using ProDoc.Domain.Common;

namespace ProDoc.Domain.Admin;

public sealed class PlatformSetting
{
    private PlatformSetting()
    {
    }

    public Guid Id { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string Value { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    public static PlatformSetting Create(string key, string value, string? description, Guid? updatedByUserId, DateTimeOffset now)
    {
        var normalized = NormalizeKey(key);
        return new PlatformSetting
        {
            Id = Guid.NewGuid(),
            Key = normalized,
            Value = value.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim()[..Math.Min(description.Trim().Length, 240)],
            UpdatedAtUtc = now,
            UpdatedByUserId = updatedByUserId
        };
    }

    public void Update(string value, Guid? updatedByUserId, DateTimeOffset now)
    {
        Value = value.Trim();
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = now;
    }

    public static string NormalizeKey(string key)
    {
        var text = key.Trim().ToLowerInvariant();
        if (text.Length is < 2 or > 80 || text.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-')))
        {
            throw DomainException.Validation("setting_key_invalid", "Use a setting key with letters, numbers, ., _, or -.");
        }

        return text;
    }
}

public sealed class FeatureFlag
{
    private FeatureFlag()
    {
    }

    public Guid Id { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public bool Enabled { get; private set; }

    public string? Description { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    public static FeatureFlag Create(string key, bool enabled, string? description, Guid? updatedByUserId, DateTimeOffset now)
    {
        return new FeatureFlag
        {
            Id = Guid.NewGuid(),
            Key = PlatformSetting.NormalizeKey(key),
            Enabled = enabled,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim()[..Math.Min(description.Trim().Length, 240)],
            UpdatedAtUtc = now,
            UpdatedByUserId = updatedByUserId
        };
    }

    public void SetEnabled(bool enabled, Guid? updatedByUserId, DateTimeOffset now)
    {
        Enabled = enabled;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = now;
    }
}
