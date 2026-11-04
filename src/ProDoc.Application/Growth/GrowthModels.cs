namespace ProDoc.Application.Growth;

public sealed record ReferralDashboard(
    string Code,
    string ShareUrl,
    string WhatsAppShareText,
    int RegisteredCount,
    int ConvertedCount,
    int RewardedCount,
    int PointsBalance,
    decimal CreditBalanceInr,
    IReadOnlyList<ReferralItem> RecentReferrals);

public sealed record ReferralItem(
    Guid Id,
    string Status,
    DateTimeOffset RegisteredAtUtc,
    DateTimeOffset? ConvertedAtUtc,
    DateTimeOffset? RewardedAtUtc);

public sealed record RewardWalletDetails(
    int PointsBalance,
    decimal CreditBalanceInr,
    IReadOnlyList<RewardLedgerItem> History);

public sealed record RewardLedgerItem(
    Guid Id,
    string EntryType,
    int PointsDelta,
    decimal CreditDeltaInr,
    string Reason,
    string? ReferenceType,
    Guid? ReferenceId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc);

public sealed record CouponDetails(
    Guid Id,
    string Code,
    string Name,
    string DiscountType,
    decimal DiscountValue,
    decimal? MinBookingAmount,
    decimal? MaxDiscountAmount,
    bool FirstBookingOnly,
    bool NewUserOnly,
    Guid? ClinicId,
    Guid? DoctorId,
    int? MaxRedemptions,
    int? MaxRedemptionsPerUser,
    int RedemptionCount,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    bool IsActive);

public sealed record CouponPreview(
    string Code,
    decimal BookingAmount,
    decimal DiscountAmount,
    decimal PayableAmount,
    string Message);

public sealed record CouponRedemptionResult(
    Guid RedemptionId,
    string Code,
    decimal DiscountAmount,
    Guid AppointmentId);

public sealed record MarketingConsentDetails(bool MarketingOptIn, DateTimeOffset UpdatedAtUtc);

public sealed record CampaignDetails(
    Guid Id,
    string Name,
    string Kind,
    string Title,
    string Body,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int DeliveryCount);

public sealed record GrowthDashboard(
    int NewRegistrations30d,
    int ReferralRegistrations,
    int ReferralConversions,
    int ReferralRewardsPaid,
    decimal ReferralConversionRate,
    int CouponsRedeemed30d,
    decimal CouponDiscountGiven30d,
    int OutstandingRewardPoints,
    decimal OutstandingRewardCreditsInr,
    int RepeatBookers30d,
    int ReturningPatients90d,
    decimal PatientRetentionRate90d,
    int CampaignDeliveries30d,
    IReadOnlyList<GrowthSeriesPoint> RegistrationTrend,
    IReadOnlyList<GrowthSeriesPoint> ReferralConversionTrend,
    IReadOnlyList<CouponUsageRow> TopCoupons);

public sealed record GrowthSeriesPoint(string Period, int Count, decimal Amount);

public sealed record CouponUsageRow(string Code, string Name, int Redemptions, decimal DiscountTotal);
