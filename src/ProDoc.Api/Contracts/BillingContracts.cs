namespace ProDoc.Api.Contracts;

public sealed record UpdateClinicPaymentSettingsRequest(string Policy, decimal AdvancePercent, bool ChargeCommissionOnWalkIns);

public sealed record CreatePaymentRequest(string? PaymentMethod, string IdempotencyKey, string? CouponCode = null, bool UseRewardCredit = false);

public sealed record VerifyPaymentRequest(string? ProviderPaymentId);

public sealed record RefundPaymentRequest(decimal? Amount, string Reason);

public sealed record CreateCommissionRuleRequest(
    string Name,
    string Basis,
    decimal Rate,
    decimal? MinimumAmount,
    decimal? MaximumAmount,
    Guid? ClinicId,
    Guid? DoctorId,
    bool AppliesToPlatformBookingsOnly,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    int Priority);

public sealed record CreateSettlementRequest(Guid ClinicId, DateOnly PeriodFrom, DateOnly PeriodTo);
