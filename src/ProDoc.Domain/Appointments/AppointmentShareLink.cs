using System.Security.Cryptography;
using System.Text;
using ProDoc.Domain.Common;

namespace ProDoc.Domain.Appointments;

/// <summary>
/// Patient-created, read-only appointment sharing link. Stores only a hash of the raw token.
/// </summary>
public sealed class AppointmentShareLink
{
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(7);
    public static readonly TimeSpan MinLifetime = TimeSpan.FromHours(1);
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromDays(30);

    private AppointmentShareLink()
    {
    }

    public Guid Id { get; private set; }

    public Guid AppointmentId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public DateTimeOffset? LastAccessedAtUtc { get; private set; }

    public int AccessCount { get; private set; }

    public bool IsActive(DateTimeOffset now) =>
        RevokedAtUtc is null && ExpiresAtUtc > now;

    public static AppointmentShareLink Create(
        Guid appointmentId,
        Guid createdByUserId,
        string tokenHash,
        DateTimeOffset now,
        TimeSpan? lifetime = null)
    {
        if (appointmentId == Guid.Empty || createdByUserId == Guid.Empty)
        {
            throw DomainException.Validation("share_invalid", "Choose a valid appointment to share.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash) || tokenHash.Length is < 32 or > 128)
        {
            throw DomainException.Validation("share_token_invalid", "A secure share token is required.");
        }

        var duration = lifetime ?? DefaultLifetime;
        if (duration < MinLifetime || duration > MaxLifetime)
        {
            throw DomainException.Validation(
                "share_expiry_invalid",
                $"Choose a sharing link lifetime between {MinLifetime.TotalHours:0} hour and {MaxLifetime.TotalDays:0} days.");
        }

        return new AppointmentShareLink
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointmentId,
            CreatedByUserId = createdByUserId,
            TokenHash = tokenHash.Trim().ToUpperInvariant(),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(duration)
        };
    }

    public void Revoke(DateTimeOffset now) => RevokedAtUtc ??= now;

    public void RecordAccess(DateTimeOffset now)
    {
        if (!IsActive(now))
        {
            throw DomainException.Conflict("share_link_unavailable", "That sharing link is no longer available.");
        }

        LastAccessedAtUtc = now;
        AccessCount += 1;
    }

    public static string HashToken(string rawToken)
    {
        var normalized = (rawToken ?? string.Empty).Trim();
        if (normalized.Length is < 24 or > 128)
        {
            throw DomainException.Validation("share_token_invalid", "That sharing link is not valid.");
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }

    public static string CreateRawToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
}
