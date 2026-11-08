using ProDoc.Domain.Common;

namespace ProDoc.Domain.Identity;

public sealed class OtpChallenge
{
    private OtpChallenge()
    {
    }

    public Guid Id { get; private set; }

    public string PhoneE164 { get; private set; } = string.Empty;

    public string CodeHash { get; private set; } = string.Empty;

    public OtpPurpose Purpose { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public int AttemptCount { get; private set; }

    public int MaxAttempts { get; private set; }

    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    public static OtpChallenge Issue(
        string phoneE164,
        string codeHash,
        OtpPurpose purpose,
        DateTimeOffset now,
        TimeSpan lifetime,
        int maxAttempts)
    {
        if (string.IsNullOrWhiteSpace(phoneE164))
        {
            throw new ArgumentException("A phone number is required.", nameof(phoneE164));
        }

        if (string.IsNullOrWhiteSpace(codeHash))
        {
            throw new ArgumentException("A code hash is required.", nameof(codeHash));
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        }

        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        }

        return new OtpChallenge
        {
            Id = Guid.NewGuid(),
            PhoneE164 = phoneE164,
            CodeHash = codeHash,
            Purpose = purpose,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(lifetime),
            MaxAttempts = maxAttempts
        };
    }

    public bool IsActive(DateTimeOffset now) =>
        ConsumedAtUtc is null && AttemptCount < MaxAttempts && ExpiresAtUtc > now;

    public void RegisterFailedAttempt() => AttemptCount++;

    public void Consume(DateTimeOffset now)
    {
        if (!IsActive(now))
        {
            throw DomainException.Validation("otp_invalid", "Incorrect OTP. Please try again.");
        }

        ConsumedAtUtc = now;
    }
}
