using Microsoft.EntityFrameworkCore;
using ProDoc.Application.SaaS;
using ProDoc.Domain.Billing;
using ProDoc.Domain.Enterprise;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Providers;
using ProDoc.Domain.SaaS;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.SaaS;

public sealed class SaaSStore(ProDocDbContext db) : ISaaSStore
{
    public async Task<IReadOnlyList<SaaSPlan>> ListPlansAsync(bool publicOnly, CancellationToken cancellationToken)
    {
        var query = db.SaaSPlans.AsNoTracking().Include(item => item.Entitlements).AsQueryable();
        if (publicOnly)
        {
            query = query.Where(item => item.IsPublic && item.IsActive);
        }

        return await query.OrderBy(item => item.SortOrder).ThenBy(item => item.Name).ToListAsync(cancellationToken);
    }

    public Task<SaaSPlan?> FindPlanByCodeAsync(string code, bool tracking, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var query = tracking
            ? db.SaaSPlans.Include(item => item.Entitlements)
            : db.SaaSPlans.AsNoTracking().Include(item => item.Entitlements);
        return query.SingleOrDefaultAsync(item => item.Code == normalized, cancellationToken);
    }

    public Task<SaaSPlan?> FindPlanByIdAsync(Guid planId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking
            ? db.SaaSPlans.Include(item => item.Entitlements)
            : db.SaaSPlans.AsNoTracking().Include(item => item.Entitlements);
        return query.SingleOrDefaultAsync(item => item.Id == planId, cancellationToken);
    }

    public void AddPlan(SaaSPlan plan) => db.SaaSPlans.Add(plan);

    public Task<Organization?> FindOrganizationAsync(Guid organizationId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.Organizations.AsQueryable() : db.Organizations.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == organizationId, cancellationToken);
    }

    public async Task<IReadOnlyList<Organization>> ListOrganizationsAsync(CancellationToken cancellationToken) =>
        await db.Organizations.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken);

    public Task<OrganizationMembership?> FindMembershipAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        db.OrganizationMemberships.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OrganizationId == organizationId && item.UserId == userId, cancellationToken);

    public Task<OrganizationSubscription?> FindSubscriptionAsync(Guid organizationId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.OrganizationSubscriptions.AsQueryable() : db.OrganizationSubscriptions.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.OrganizationId == organizationId, cancellationToken);
    }

    public void AddSubscription(OrganizationSubscription subscription) => db.OrganizationSubscriptions.Add(subscription);

    public Task<OrganizationBranding?> FindBrandingAsync(Guid organizationId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.OrganizationBrandings.AsQueryable() : db.OrganizationBrandings.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.OrganizationId == organizationId, cancellationToken);
    }

    public Task<OrganizationBranding?> FindBrandingByHostAsync(string host, CancellationToken cancellationToken)
    {
        var value = host.Trim().ToLowerInvariant();
        return db.OrganizationBrandings.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CustomDomain == value || item.SubdomainSlug == value,
                cancellationToken);
    }

    public void AddBranding(OrganizationBranding branding) => db.OrganizationBrandings.Add(branding);

    public async Task<IReadOnlyList<OrganizationFeatureOverride>> ListOverridesAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await db.OrganizationFeatureOverrides.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

    public Task<OrganizationFeatureOverride?> FindOverrideAsync(Guid organizationId, string featureCode, bool tracking, CancellationToken cancellationToken)
    {
        var code = featureCode.Trim().ToLowerInvariant();
        var query = tracking ? db.OrganizationFeatureOverrides.AsQueryable() : db.OrganizationFeatureOverrides.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.OrganizationId == organizationId && item.FeatureCode == code, cancellationToken);
    }

    public void AddOverride(OrganizationFeatureOverride item) => db.OrganizationFeatureOverrides.Add(item);

    public Task<int> CountClinicsAsync(Guid organizationId, CancellationToken cancellationToken) =>
        db.Clinics.AsNoTracking().CountAsync(item => item.OrganizationId == organizationId, cancellationToken);

    public Task<int> CountBranchesAsync(Guid organizationId, CancellationToken cancellationToken) =>
        db.ClinicBranches.AsNoTracking()
            .Where(branch => branch.IsActive)
            .Join(
                db.Clinics.AsNoTracking().Where(clinic => clinic.OrganizationId == organizationId),
                branch => branch.ClinicId,
                clinic => clinic.Id,
                (branch, _) => branch)
            .CountAsync(cancellationToken);

    public async Task<int> CountDoctorsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var clinicIds = await db.Clinics.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        if (clinicIds.Count == 0)
        {
            return 0;
        }

        return await db.DoctorClinicAffiliations.AsNoTracking()
            .Where(item => clinicIds.Contains(item.ClinicId))
            .Select(item => item.DoctorId)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    public Task<int> CountStaffAsync(Guid organizationId, CancellationToken cancellationToken) =>
        db.OrganizationMemberships.AsNoTracking()
            .CountAsync(item => item.OrganizationId == organizationId && item.IsActive, cancellationToken);

    public Task<SaaSSubscriptionCharge?> FindChargeByIdempotencyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        db.SaaSSubscriptionCharges.AsNoTracking()
            .SingleOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);

    public Task<SaaSSubscriptionCharge?> FindChargeAsync(Guid chargeId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.SaaSSubscriptionCharges.AsQueryable() : db.SaaSSubscriptionCharges.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == chargeId, cancellationToken);
    }

    public async Task<IReadOnlyList<SaaSSubscriptionCharge>> ListChargesAsync(Guid organizationId, int take, CancellationToken cancellationToken) =>
        await db.SaaSSubscriptionCharges.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

    public void AddCharge(SaaSSubscriptionCharge charge) => db.SaaSSubscriptionCharges.Add(charge);

    public void AddInvoice(Invoice invoice) => db.Invoices.Add(invoice);

    public async Task MarkInvoicePaidAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.SingleAsync(item => item.Id == invoiceId, cancellationToken);
        if (!string.Equals(invoice.Status, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            invoice.RecordPayment(invoice.Outstanding, DateTimeOffset.UtcNow);
        }
    }

    public async Task<string> NextSaaSInvoiceNumberAsync(CancellationToken cancellationToken)
    {
        var count = await db.Invoices.CountAsync(item => item.Kind == InvoiceKind.SaaSSubscription, cancellationToken);
        return $"SAAS-{clockDate():yyyyMMdd}-{count + 1:D5}";

        static DateTimeOffset clockDate() => DateTimeOffset.UtcNow;
    }

    public async Task<decimal> SumSaaSRevenueAsync(CancellationToken cancellationToken) =>
        await db.SaaSSubscriptionCharges.AsNoTracking()
            .Where(item => item.Status == SaaSChargeStatus.Paid)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

    public async Task<decimal> SumCommissionRevenueAsync(CancellationToken cancellationToken) =>
        await db.CommissionSnapshots.AsNoTracking()
            .SumAsync(item => (decimal?)item.CommissionAmount, cancellationToken) ?? 0m;

    public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(audit);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public async Task SavePlanSeedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Seed shares a long-lived scope with other seeders; discard polluted tracker state and skip
            // re-applying identical catalog rows on the next process start (EnsurePlanAsync is idempotent).
            foreach (var entry in db.ChangeTracker.Entries().ToList())
            {
                entry.State = EntityState.Detached;
            }
        }
    }
}
