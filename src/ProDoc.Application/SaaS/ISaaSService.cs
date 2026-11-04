using ProDoc.Application.Providers;

namespace ProDoc.Application.SaaS;

public interface ISaaSService
{
    Task SeedDefaultsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SaaSPlanDetails>> ListPlansAsync(bool publicOnly, CancellationToken cancellationToken);

    Task<SaaSPlanDetails> UpsertPlanAsync(
        Guid actorUserId,
        string code,
        string name,
        string description,
        decimal monthlyPrice,
        decimal annualPrice,
        int trialDays,
        int gracePeriodDays,
        int refundPolicyDays,
        bool isPublic,
        bool isActive,
        bool isCustom,
        int sortOrder,
        IReadOnlyList<SaaSEntitlementDetails> entitlements,
        RequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<OrganizationBrandingDetails> GetBrandingAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken);

    Task<OrganizationBrandingDetails> UpdateBrandingAsync(
        Guid userId,
        Guid organizationId,
        string primaryColorHex,
        string accentColorHex,
        string? loginHeadline,
        string? loginSubheadline,
        string? emailFromName,
        string? emailFooterText,
        string? notificationAccentHex,
        string? customDomain,
        string? subdomainSlug,
        RequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<OrganizationBrandingDetails> UploadLogoAsync(
        Guid userId,
        Guid organizationId,
        Stream content,
        string fileName,
        string contentType,
        long contentLength,
        RequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<PublicTenantBranding?> ResolvePublicBrandingAsync(string? host, string? slug, CancellationToken cancellationToken);

    Task<TenantUsageDetails> GetUsageAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken);

    Task<TenantSubscriptionDetails> GetSubscriptionAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken);

    Task<TenantSubscriptionDetails> ChangePlanAsync(
        Guid userId,
        Guid organizationId,
        string planCode,
        string billingInterval,
        bool applyImmediately,
        RequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<TenantSubscriptionDetails> CancelSubscriptionAsync(
        Guid userId,
        Guid organizationId,
        bool atPeriodEnd,
        RequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<SaaSChargeDetails> StartRenewalPaymentAsync(
        Guid userId,
        Guid organizationId,
        string? idempotencyKey,
        RequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<SaaSChargeDetails> ConfirmRenewalPaymentAsync(
        Guid userId,
        Guid organizationId,
        Guid chargeId,
        string? providerPaymentId,
        RequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SaaSChargeDetails>> ListBillingHistoryAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken);

    Task<PlatformSaaSMetrics> PlatformMetricsAsync(CancellationToken cancellationToken);

    Task<PlatformTenantSummary> PlatformSuspendTenantAsync(
        Guid actorUserId,
        Guid organizationId,
        bool suspend,
        RequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<TenantUsageDetails> PlatformSetEntitlementAsync(
        Guid actorUserId,
        Guid organizationId,
        string featureCode,
        bool enabled,
        int? limitValue,
        RequestMetadata metadata,
        CancellationToken cancellationToken);

    Task EnsureTenantBootstrapAsync(Guid organizationId, string organizationName, CancellationToken cancellationToken);
}
