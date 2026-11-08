using System.Security.Cryptography;
using System.Text;
using ProDoc.Domain.Common;

namespace ProDoc.Domain.Growth;

public enum ReferralStatus
{
    Registered = 1,
    Converted = 2,
    Rewarded = 3,
    Rejected = 4
}

public enum RewardEntryType
{
    Earn = 1,
    Redeem = 2,
    Expire = 3,
    Adjust = 4,
    Reverse = 5
}

public enum CouponDiscountType
{
    Fixed = 1,
    Percent = 2
}

public enum CampaignKind
{
    Onboarding = 1,
    IncompleteBooking = 2,
    AppointmentFollowUp = 3,
    Reengagement = 4,
    Referral = 5,
    Promotion = 6
}

public static class GrowthDefaults
{
    public const int ReferralRewardPoints = 100;
    public const int ReferralRewardCreditInr = 0;
    public const int LoyaltyPointsPerCompletedVisit = 20;
    public const int MaxReferralRewardsPerDay = 20;
    public const int CouponMaxPercent = 50;

    /// <summary>Points required to redeem ₹1 of ProDoc booking credit (configurable via growth.points_per_inr).</summary>
    public const int PointsPerInr = 10;
}

public sealed class ReferralCode
{
    private ReferralCode()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static ReferralCode Create(Guid userId, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Code = GenerateCode(userId),
            CreatedAtUtc = now
        };

    public static string Normalize(string? code)
    {
        var text = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (text.Length is < 4 or > 16 || text.Any(ch => !(char.IsLetterOrDigit(ch))))
        {
            throw DomainException.Validation("referral_code_invalid", "Enter a valid referral code.");
        }

        return text;
    }

    private static string GenerateCode(Guid userId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(userId.ToString("N")));
        var alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var chars = new char[8];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[hash[i] % alphabet.Length];
        }

        return new string(chars);
    }
}

public sealed class ReferralRelationship
{
    private ReferralRelationship()
    {
    }

    public Guid Id { get; private set; }

    public Guid ReferrerUserId { get; private set; }

    public Guid ReferredUserId { get; private set; }

    public string CodeUsed { get; private set; } = string.Empty;

    public ReferralStatus Status { get; private set; }

    public DateTimeOffset RegisteredAtUtc { get; private set; }

    public DateTimeOffset? ConvertedAtUtc { get; private set; }

    public DateTimeOffset? RewardedAtUtc { get; private set; }

    public Guid? ConversionAppointmentId { get; private set; }

    public static ReferralRelationship Create(Guid referrerUserId, Guid referredUserId, string codeUsed, DateTimeOffset now)
    {
        if (referrerUserId == referredUserId)
        {
            throw DomainException.Validation("referral_self", "You cannot refer yourself.");
        }

        return new ReferralRelationship
        {
            Id = Guid.NewGuid(),
            ReferrerUserId = referrerUserId,
            ReferredUserId = referredUserId,
            CodeUsed = ReferralCode.Normalize(codeUsed),
            Status = ReferralStatus.Registered,
            RegisteredAtUtc = now
        };
    }

    public void MarkConverted(Guid appointmentId, DateTimeOffset now)
    {
        if (Status is ReferralStatus.Rejected)
        {
            return;
        }

        if (Status is ReferralStatus.Converted or ReferralStatus.Rewarded)
        {
            return;
        }

        Status = ReferralStatus.Converted;
        ConvertedAtUtc = now;
        ConversionAppointmentId = appointmentId;
    }

    public void MarkRewarded(DateTimeOffset now)
    {
        if (Status != ReferralStatus.Converted)
        {
            throw DomainException.Conflict("referral_not_converted", "Referral must convert before rewards.");
        }

        Status = ReferralStatus.Rewarded;
        RewardedAtUtc = now;
    }

    public void Reject(DateTimeOffset now)
    {
        Status = ReferralStatus.Rejected;
        ConvertedAtUtc ??= now;
    }
}

public sealed class RewardAccount
{
    private RewardAccount()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public int PointsBalance { get; private set; }

    public decimal CreditBalanceInr { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static RewardAccount Create(Guid userId, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UpdatedAtUtc = now
        };

    public void Apply(int pointsDelta, decimal creditDelta, DateTimeOffset now)
    {
        var nextPoints = PointsBalance + pointsDelta;
        var nextCredit = CreditBalanceInr + creditDelta;
        if (nextPoints < 0 || nextCredit < 0)
        {
            throw DomainException.Validation("reward_balance_insufficient", "Insufficient reward balance.");
        }

        PointsBalance = nextPoints;
        CreditBalanceInr = decimal.Round(nextCredit, 2);
        UpdatedAtUtc = now;
    }
}

public sealed class RewardTransaction
{
    private RewardTransaction()
    {
    }

    public Guid Id { get; private set; }

    public Guid RewardAccountId { get; private set; }

    public Guid UserId { get; private set; }

    public RewardEntryType EntryType { get; private set; }

    public int PointsDelta { get; private set; }

    public decimal CreditDeltaInr { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public string? ReferenceType { get; private set; }

    public Guid? ReferenceId { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ExpiresAtUtc { get; private set; }

    public static RewardTransaction Record(
        Guid accountId,
        Guid userId,
        RewardEntryType type,
        int pointsDelta,
        decimal creditDeltaInr,
        string reason,
        string idempotencyKey,
        DateTimeOffset now,
        string? referenceType = null,
        Guid? referenceId = null,
        DateTimeOffset? expiresAtUtc = null)
    {
        var key = idempotencyKey.Trim();
        if (key.Length is < 4 or > 120)
        {
            throw DomainException.Validation("reward_idempotency_invalid", "Provide a reward idempotency key.");
        }

        var text = reason.Trim();
        if (text.Length is < 2 or > 200)
        {
            throw DomainException.Validation("reward_reason_invalid", "Enter a reward reason.");
        }

        return new RewardTransaction
        {
            Id = Guid.NewGuid(),
            RewardAccountId = accountId,
            UserId = userId,
            EntryType = type,
            PointsDelta = pointsDelta,
            CreditDeltaInr = decimal.Round(creditDeltaInr, 2),
            Reason = text,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            IdempotencyKey = key,
            CreatedAtUtc = now,
            ExpiresAtUtc = expiresAtUtc
        };
    }
}

public sealed class Coupon
{
    private Coupon()
    {
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public CouponDiscountType DiscountType { get; private set; }

    public decimal DiscountValue { get; private set; }

    public decimal? MinBookingAmount { get; private set; }

    public decimal? MaxDiscountAmount { get; private set; }

    public bool FirstBookingOnly { get; private set; }

    public bool NewUserOnly { get; private set; }

    public Guid? ClinicId { get; private set; }

    public Guid? DoctorId { get; private set; }

    public int? MaxRedemptions { get; private set; }

    public int? MaxRedemptionsPerUser { get; private set; }

    public int RedemptionCount { get; private set; }

    public DateTimeOffset StartsAtUtc { get; private set; }

    public DateTimeOffset EndsAtUtc { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Coupon Create(
        string code,
        string name,
        CouponDiscountType type,
        decimal value,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        DateTimeOffset now,
        decimal? minBookingAmount = null,
        decimal? maxDiscountAmount = null,
        bool firstBookingOnly = false,
        bool newUserOnly = false,
        Guid? clinicId = null,
        Guid? doctorId = null,
        int? maxRedemptions = null,
        int? maxPerUser = null)
    {
        if (endsAtUtc <= startsAtUtc)
        {
            throw DomainException.Validation("coupon_window_invalid", "Coupon end must be after start.");
        }

        if (type == CouponDiscountType.Percent && (value is <= 0 or > GrowthDefaults.CouponMaxPercent))
        {
            throw DomainException.Validation("coupon_percent_invalid", $"Percent coupons must be between 1 and {GrowthDefaults.CouponMaxPercent}.");
        }

        if (type == CouponDiscountType.Fixed && value <= 0)
        {
            throw DomainException.Validation("coupon_value_invalid", "Fixed coupon value must be greater than zero.");
        }

        return new Coupon
        {
            Id = Guid.NewGuid(),
            Code = NormalizeCode(code),
            Name = name.Trim(),
            DiscountType = type,
            DiscountValue = decimal.Round(value, 2),
            MinBookingAmount = minBookingAmount,
            MaxDiscountAmount = maxDiscountAmount,
            FirstBookingOnly = firstBookingOnly,
            NewUserOnly = newUserOnly,
            ClinicId = clinicId,
            DoctorId = doctorId,
            MaxRedemptions = maxRedemptions,
            MaxRedemptionsPerUser = maxPerUser,
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = endsAtUtc,
            IsActive = true,
            CreatedAtUtc = now
        };
    }

    public decimal CalculateDiscount(decimal bookingAmount)
    {
        if (bookingAmount <= 0)
        {
            return 0m;
        }

        var discount = DiscountType == CouponDiscountType.Fixed
            ? DiscountValue
            : decimal.Round(bookingAmount * DiscountValue / 100m, 2, MidpointRounding.AwayFromZero);
        if (MaxDiscountAmount is decimal max)
        {
            discount = Math.Min(discount, max);
        }

        return Math.Min(discount, bookingAmount);
    }

    public void EnsureRedeemable(DateTimeOffset now)
    {
        if (!IsActive || now < StartsAtUtc || now > EndsAtUtc)
        {
            throw DomainException.Validation("coupon_inactive", "That coupon is not active.");
        }

        if (MaxRedemptions is int max && RedemptionCount >= max)
        {
            throw DomainException.Conflict("coupon_exhausted", "That coupon has reached its usage limit.");
        }
    }

    public void IncrementRedemption() => RedemptionCount += 1;

    public void SetActive(bool active) => IsActive = active;

    public static string NormalizeCode(string? code)
    {
        var text = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (text.Length is < 3 or > 32 || text.Any(ch => !(char.IsLetterOrDigit(ch) || ch == '-')))
        {
            throw DomainException.Validation("coupon_code_invalid", "Enter a valid coupon code.");
        }

        return text;
    }
}

public sealed class CouponRedemption
{
    private CouponRedemption()
    {
    }

    public Guid Id { get; private set; }

    public Guid CouponId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid AppointmentId { get; private set; }

    public Guid? PaymentId { get; private set; }

    public decimal DiscountAmount { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static CouponRedemption Create(
        Guid couponId,
        Guid userId,
        Guid appointmentId,
        decimal discountAmount,
        string idempotencyKey,
        DateTimeOffset now,
        Guid? paymentId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            CouponId = couponId,
            UserId = userId,
            AppointmentId = appointmentId,
            PaymentId = paymentId,
            DiscountAmount = decimal.Round(discountAmount, 2),
            IdempotencyKey = idempotencyKey.Trim(),
            CreatedAtUtc = now
        };

    public void AttachPayment(Guid paymentId) => PaymentId ??= paymentId;
}

public sealed class MarketingConsent
{
    private MarketingConsent()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public bool MarketingOptIn { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static MarketingConsent Create(Guid userId, bool optIn, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MarketingOptIn = optIn,
            UpdatedAtUtc = now
        };

    public void SetOptIn(bool optIn, DateTimeOffset now)
    {
        MarketingOptIn = optIn;
        UpdatedAtUtc = now;
    }
}

public sealed class MarketingCampaign
{
    private MarketingCampaign()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public CampaignKind Kind { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static MarketingCampaign Create(string name, CampaignKind kind, string title, string body, DateTimeOffset now)
    {
        var n = name.Trim();
        var t = title.Trim();
        var b = body.Trim();
        if (n.Length is < 2 or > 120 || t.Length is < 2 or > 120 || b.Length is < 4 or > 280)
        {
            throw DomainException.Validation("campaign_invalid", "Enter a valid campaign name, title, and body.");
        }

        return new MarketingCampaign
        {
            Id = Guid.NewGuid(),
            Name = n,
            Kind = kind,
            Title = t,
            Body = b,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void SetActive(bool active, DateTimeOffset now)
    {
        IsActive = active;
        UpdatedAtUtc = now;
    }
}

public sealed class CampaignDelivery
{
    private CampaignDelivery()
    {
    }

    public Guid Id { get; private set; }

    public Guid CampaignId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid? SubjectId { get; private set; }

    public DateTimeOffset SentAtUtc { get; private set; }

    public static CampaignDelivery Create(Guid campaignId, Guid userId, Guid? subjectId, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            UserId = userId,
            SubjectId = subjectId,
            SentAtUtc = now
        };
}
