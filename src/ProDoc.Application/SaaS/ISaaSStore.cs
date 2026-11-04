using ProDoc.Domain.Billing;
using ProDoc.Domain.Enterprise;
using ProDoc.Domain.Governance;
using ProDoc.Domain.SaaS;

namespace ProDoc.Application.SaaS;

public interface ISaaSStore
{
    Task<IReadOnlyList<SaaSPlan>> ListPlansAsync(bool publicOnly, CancellationToken cancellationToken);

    Task<SaaSPlan?> FindPlanByCodeAsync(string code, bool tracking, CancellationToken cancellationToken);

    Task<SaaSPlan?> FindPlanByIdAsync(Guid planId, bool tracking, CancellationToken cancellationToken);

    void AddPlan(SaaSPlan plan);

    Task<Organization?> FindOrganizationAsync(Guid organizationId, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<Organization>> ListOrganizationsAsync(CancellationToken cancellationToken);

    Task<OrganizationMembership?> FindMembershipAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);

    Task<OrganizationSubscription?> FindSubscriptionAsync(Guid organizationId, bool tracking, CancellationToken cancellationToken);

    void AddSubscription(OrganizationSubscription subscription);

    Task<OrganizationBranding?> FindBrandingAsync(Guid organizationId, bool tracking, CancellationToken cancellationToken);

    Task<OrganizationBranding?> FindBrandingByHostAsync(string host, CancellationToken cancellationToken);

    void AddBranding(OrganizationBranding branding);

    Task<IReadOnlyList<OrganizationFeatureOverride>> ListOverridesAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<OrganizationFeatureOverride?> FindOverrideAsync(Guid organizationId, string featureCode, bool tracking, CancellationToken cancellationToken);

    void AddOverride(OrganizationFeatureOverride item);

    Task<int> CountClinicsAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<int> CountBranchesAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<int> CountDoctorsAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<int> CountStaffAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<SaaSSubscriptionCharge?> FindChargeByIdempotencyAsync(string idempotencyKey, CancellationToken cancellationToken);

    Task<SaaSSubscriptionCharge?> FindChargeAsync(Guid chargeId, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<SaaSSubscriptionCharge>> ListChargesAsync(Guid organizationId, int take, CancellationToken cancellationToken);

    void AddCharge(SaaSSubscriptionCharge charge);

    void AddInvoice(Invoice invoice);

    Task MarkInvoicePaidAsync(Guid invoiceId, CancellationToken cancellationToken);

    Task<string> NextSaaSInvoiceNumberAsync(CancellationToken cancellationToken);

    Task<decimal> SumSaaSRevenueAsync(CancellationToken cancellationToken);

    Task<decimal> SumCommissionRevenueAsync(CancellationToken cancellationToken);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Persists plan seed changes with one concurrency retry (detached tracker pollution / stale rows).</summary>
    Task SavePlanSeedAsync(CancellationToken cancellationToken);
}

public interface ITenantEntitlementGuard
{
    Task EnsureTenantOperationalAsync(Guid organizationId, CancellationToken cancellationToken);

    Task EnsureCanAddClinicAsync(Guid organizationId, CancellationToken cancellationToken);

    Task EnsureCanAddStaffAsync(Guid organizationId, CancellationToken cancellationToken);

    Task EnsureFeatureAsync(Guid organizationId, string featureCode, CancellationToken cancellationToken);

    Task<TenantUsageDetails> GetUsageAsync(Guid organizationId, CancellationToken cancellationToken);
}
