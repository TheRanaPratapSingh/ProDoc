namespace ProDoc.Application.SaaS;

public sealed record SaaSPlanDetails(
    Guid Id,
    string Code,
    string Name,
    string Description,
    decimal MonthlyPrice,
    decimal AnnualPrice,
    string Currency,
    int TrialDays,
    int GracePeriodDays,
    int RefundPolicyDays,
    bool IsPublic,
    bool IsActive,
    bool IsCustom,
    int SortOrder,
    IReadOnlyList<SaaSEntitlementDetails> Entitlements);

public sealed record SaaSEntitlementDetails(string FeatureCode, bool IsEnabled, int? LimitValue);

public sealed record OrganizationBrandingDetails(
    Guid OrganizationId,
    string OrganizationName,
    string? LogoStorageKey,
    string PrimaryColorHex,
    string AccentColorHex,
    string? LoginHeadline,
    string? LoginSubheadline,
    string? EmailFromName,
    string? EmailFooterText,
    string? NotificationAccentHex,
    string? CustomDomain,
    string? SubdomainSlug,
    bool WhiteLabelEnabled,
    bool CustomDomainEnabled);

public sealed record PublicTenantBranding(
    Guid OrganizationId,
    string OrganizationName,
    string? LogoUrlPath,
    string PrimaryColorHex,
    string AccentColorHex,
    string? LoginHeadline,
    string? LoginSubheadline,
    string? NotificationAccentHex,
    string ResolvedBy);

public sealed record TenantUsageDetails(
    Guid OrganizationId,
    int ClinicsUsed,
    int? ClinicsLimit,
    int BranchesUsed,
    int? BranchesLimit,
    int DoctorsUsed,
    int? DoctorsLimit,
    int StaffUsed,
    int? StaffLimit,
    long StorageBytesUsed,
    int? StorageMbLimit,
    string SubscriptionStatus,
    string PlanCode,
    DateTimeOffset? PeriodEndUtc,
    DateTimeOffset? GracePeriodEndsUtc,
    bool CancelAtPeriodEnd,
    IReadOnlyList<SaaSEntitlementDetails> EffectiveEntitlements);

public sealed record TenantSubscriptionDetails(
    Guid OrganizationId,
    string PlanCode,
    Guid? PlanId,
    string Status,
    int SeatLimit,
    string BillingInterval,
    string? BillingEmail,
    DateTimeOffset? CurrentPeriodStartUtc,
    DateTimeOffset? CurrentPeriodEndUtc,
    DateTimeOffset? TrialEndsUtc,
    DateTimeOffset? GracePeriodEndsUtc,
    bool CancelAtPeriodEnd,
    string? PendingPlanCode,
    string Note);

public sealed record SaaSChargeDetails(
    Guid Id,
    Guid OrganizationId,
    Guid InvoiceId,
    string Status,
    string Interval,
    string PlanCode,
    decimal Amount,
    string Currency,
    string? ProviderOrderId,
    string? CheckoutPayload,
    DateTimeOffset PeriodStartUtc,
    DateTimeOffset PeriodEndUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? PaidAtUtc);

public sealed record PlatformTenantSummary(
    Guid OrganizationId,
    string Name,
    string Kind,
    bool IsActive,
    string PlanCode,
    string SubscriptionStatus,
    DateTimeOffset? PeriodEndUtc,
    int ClinicCount,
    int MemberCount,
    decimal SaaSRevenueCollected);

public sealed record PlatformSaaSMetrics(
    int ActiveTenants,
    int TrialTenants,
    int PastDueTenants,
    int SuspendedTenants,
    decimal SaaSRevenueCollected,
    decimal AppointmentCommissionRevenue,
    IReadOnlyList<PlatformTenantSummary> Tenants);
