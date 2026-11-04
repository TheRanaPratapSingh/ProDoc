using ProDoc.Application.Abstractions;
using ProDoc.Application.Billing;
using ProDoc.Application.Providers;
using ProDoc.Domain.Billing;
using ProDoc.Domain.Common;
using ProDoc.Domain.Enterprise;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.SaaS;

namespace ProDoc.Application.SaaS;

public sealed class SaaSService(
    ISaaSStore store,
    IDocumentStorage documents,
    IUploadScanner scanner,
    IPaymentGateway payments,
    IClock clock) : ISaaSService, ITenantEntitlementGuard
{
    public async Task SeedDefaultsAsync(CancellationToken cancellationToken)
    {
        await EnsurePlanAsync(
            "STARTER",
            "Starter",
            "Small clinics getting started on ProDoc.",
            1999m,
            19990m,
            trialDays: 14,
            grace: 3,
            refundDays: 7,
            isPublic: true,
            isCustom: false,
            sort: 10,
            [
                (SaaSFeatureCodes.MaxClinics, true, 1),
                (SaaSFeatureCodes.MaxBranches, true, 2),
                (SaaSFeatureCodes.MaxDoctors, true, 5),
                (SaaSFeatureCodes.MaxStaff, true, 10),
                (SaaSFeatureCodes.MaxStorageMb, true, 512),
                (SaaSFeatureCodes.WhiteLabel, false, null),
                (SaaSFeatureCodes.CustomDomain, false, null),
                (SaaSFeatureCodes.AdvancedAnalytics, false, null),
                (SaaSFeatureCodes.Integrations, false, null),
                (SaaSFeatureCodes.AiAssistant, true, null)
            ],
            cancellationToken);

        await EnsurePlanAsync(
            "PROFESSIONAL",
            "Professional",
            "Growing multi-branch practices.",
            4999m,
            49990m,
            trialDays: 14,
            grace: 5,
            refundDays: 7,
            isPublic: true,
            isCustom: false,
            sort: 20,
            [
                (SaaSFeatureCodes.MaxClinics, true, 5),
                (SaaSFeatureCodes.MaxBranches, true, 15),
                (SaaSFeatureCodes.MaxDoctors, true, 40),
                (SaaSFeatureCodes.MaxStaff, true, 100),
                (SaaSFeatureCodes.MaxStorageMb, true, 5120),
                (SaaSFeatureCodes.WhiteLabel, true, null),
                (SaaSFeatureCodes.CustomDomain, false, null),
                (SaaSFeatureCodes.AdvancedAnalytics, true, null),
                (SaaSFeatureCodes.Integrations, true, null),
                (SaaSFeatureCodes.AiAssistant, true, null)
            ],
            cancellationToken);

        await EnsurePlanAsync(
            "ENTERPRISE",
            "Enterprise",
            "Hospital groups with advanced controls.",
            14999m,
            149990m,
            trialDays: 30,
            grace: 7,
            refundDays: 14,
            isPublic: true,
            isCustom: false,
            sort: 30,
            [
                (SaaSFeatureCodes.MaxClinics, true, 50),
                (SaaSFeatureCodes.MaxBranches, true, 200),
                (SaaSFeatureCodes.MaxDoctors, true, 500),
                (SaaSFeatureCodes.MaxStaff, true, 2000),
                (SaaSFeatureCodes.MaxStorageMb, true, 51200),
                (SaaSFeatureCodes.WhiteLabel, true, null),
                (SaaSFeatureCodes.CustomDomain, true, null),
                (SaaSFeatureCodes.AdvancedAnalytics, true, null),
                (SaaSFeatureCodes.Integrations, true, null),
                (SaaSFeatureCodes.AiAssistant, true, null)
            ],
            cancellationToken);

        await EnsurePlanAsync(
            "CUSTOM",
            "Custom",
            "Negotiated enterprise contract with platform-configured entitlements.",
            0m,
            0m,
            trialDays: 0,
            grace: 14,
            refundDays: 0,
            isPublic: false,
            isCustom: true,
            sort: 100,
            [
                (SaaSFeatureCodes.MaxClinics, true, 1000),
                (SaaSFeatureCodes.MaxBranches, true, 5000),
                (SaaSFeatureCodes.MaxDoctors, true, 10000),
                (SaaSFeatureCodes.MaxStaff, true, 50000),
                (SaaSFeatureCodes.MaxStorageMb, true, 512000),
                (SaaSFeatureCodes.WhiteLabel, true, null),
                (SaaSFeatureCodes.CustomDomain, true, null),
                (SaaSFeatureCodes.AdvancedAnalytics, true, null),
                (SaaSFeatureCodes.Integrations, true, null),
                (SaaSFeatureCodes.AiAssistant, true, null)
            ],
            cancellationToken);
    }

    public async Task EnsureTenantBootstrapAsync(Guid organizationId, string organizationName, CancellationToken cancellationToken)
    {
        if (await store.FindBrandingAsync(organizationId, false, cancellationToken) is null)
        {
            store.AddBranding(OrganizationBranding.CreateDefault(organizationId, organizationName, clock.UtcNow));
        }

        var subscription = await store.FindSubscriptionAsync(organizationId, true, cancellationToken);
        var starter = await store.FindPlanByCodeAsync("STARTER", false, cancellationToken);
        if (subscription is not null && starter is not null && subscription.PlanId is null)
        {
            var seats = starter.LimitFor(SaaSFeatureCodes.MaxStaff) ?? subscription.SeatLimit;
            subscription.AssignPlan(starter.Id, starter.Code, seats, 1, clock.UtcNow);
        }

        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SaaSPlanDetails>> ListPlansAsync(bool publicOnly, CancellationToken cancellationToken)
    {
        var plans = await store.ListPlansAsync(publicOnly, cancellationToken);
        return plans.Select(MapPlan).ToArray();
    }

    public async Task<SaaSPlanDetails> UpsertPlanAsync(
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
        CancellationToken cancellationToken)
    {
        var existing = await store.FindPlanByCodeAsync(code, true, cancellationToken);
        if (existing is null)
        {
            existing = SaaSPlan.Create(
                code,
                name,
                description,
                monthlyPrice,
                annualPrice,
                trialDays,
                gracePeriodDays,
                refundPolicyDays,
                isPublic,
                isCustom,
                sortOrder,
                clock.UtcNow);
            store.AddPlan(existing);
        }
        else
        {
            existing.Update(
                name,
                description,
                monthlyPrice,
                annualPrice,
                trialDays,
                gracePeriodDays,
                refundPolicyDays,
                isPublic,
                isActive,
                sortOrder,
                clock.UtcNow);
        }

        existing.ReplaceEntitlements(
            entitlements.Select(item => (item.FeatureCode, item.IsEnabled, item.LimitValue)),
            clock.UtcNow);
        await store.AddAuditAsync(Audit(actorUserId, AuditActions.SaaSPlanConfigured, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapPlan(existing);
    }

    public async Task<OrganizationBrandingDetails> GetBrandingAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        await EnsureOrgManageAsync(userId, organizationId, cancellationToken);
        return await MapBrandingAsync(organizationId, cancellationToken);
    }

    public async Task<OrganizationBrandingDetails> UpdateBrandingAsync(
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
        CancellationToken cancellationToken)
    {
        await EnsureOrgManageAsync(userId, organizationId, cancellationToken);
        await EnsureTenantOperationalAsync(organizationId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(customDomain))
        {
            await EnsureFeatureAsync(organizationId, SaaSFeatureCodes.CustomDomain, cancellationToken);
        }

        await EnsureFeatureAsync(organizationId, SaaSFeatureCodes.WhiteLabel, cancellationToken);
        var branding = await store.FindBrandingAsync(organizationId, true, cancellationToken)
            ?? throw DomainException.NotFound("branding_not_found", "Branding was not initialized for this organization.");
        branding.Update(
            primaryColorHex,
            accentColorHex,
            loginHeadline,
            loginSubheadline,
            emailFromName,
            emailFooterText,
            notificationAccentHex,
            customDomain,
            subdomainSlug,
            clock.UtcNow);
        await store.AddAuditAsync(Audit(userId, AuditActions.TenantBrandingUpdated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return await MapBrandingAsync(organizationId, cancellationToken);
    }

    public async Task<OrganizationBrandingDetails> UploadLogoAsync(
        Guid userId,
        Guid organizationId,
        Stream content,
        string fileName,
        string contentType,
        long contentLength,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await EnsureOrgManageAsync(userId, organizationId, cancellationToken);
        await EnsureFeatureAsync(organizationId, SaaSFeatureCodes.WhiteLabel, cancellationToken);
        if (contentLength <= 0 || contentLength > 2 * 1024 * 1024)
        {
            throw DomainException.Validation("logo_too_large", "Logo must be between 1 byte and 2 MB.");
        }

        var scanned = await scanner.ScanAsync(content, fileName, contentType, imagesOnly: true, cancellationToken);
        if (!scanned.Allowed)
        {
            throw DomainException.Validation("logo_rejected", scanned.Detail);
        }

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        var stored = await documents.SaveAsync(content, Path.GetExtension(fileName), cancellationToken);
        var branding = await store.FindBrandingAsync(organizationId, true, cancellationToken)
            ?? throw DomainException.NotFound("branding_not_found", "Branding was not initialized for this organization.");
        branding.SetLogo(stored.StorageKey, clock.UtcNow);
        await store.AddAuditAsync(Audit(userId, AuditActions.TenantBrandingUpdated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return await MapBrandingAsync(organizationId, cancellationToken);
    }

    public async Task<PublicTenantBranding?> ResolvePublicBrandingAsync(string? host, string? slug, CancellationToken cancellationToken)
    {
        OrganizationBranding? branding = null;
        var resolvedBy = "none";
        if (!string.IsNullOrWhiteSpace(host))
        {
            var normalized = host.Split(':')[0].Trim().ToLowerInvariant();
            branding = await store.FindBrandingByHostAsync(normalized, cancellationToken);
            if (branding is not null)
            {
                resolvedBy = "custom_domain";
            }
            else
            {
                var parts = normalized.Split('.');
                if (parts.Length >= 3)
                {
                    branding = await store.FindBrandingByHostAsync(parts[0], cancellationToken);
                    if (branding?.SubdomainSlug == parts[0])
                    {
                        resolvedBy = "subdomain";
                    }
                    else
                    {
                        branding = null;
                    }
                }
            }
        }

        if (branding is null && !string.IsNullOrWhiteSpace(slug))
        {
            branding = await store.FindBrandingByHostAsync(slug.Trim().ToLowerInvariant(), cancellationToken);
            if (branding is not null)
            {
                resolvedBy = "slug";
            }
        }

        if (branding is null)
        {
            return null;
        }

        var org = await store.FindOrganizationAsync(branding.OrganizationId, false, cancellationToken);
        if (org is not { IsActive: true })
        {
            return null;
        }

        var usage = await GetUsageInternalAsync(org.Id, cancellationToken);
        if (!usage.EffectiveEntitlements.Any(item => item.FeatureCode == SaaSFeatureCodes.WhiteLabel && item.IsEnabled))
        {
            return null;
        }

        return new PublicTenantBranding(
            org.Id,
            org.Name,
            branding.LogoStorageKey is null ? null : $"/api/v1/public/tenant-assets/{branding.LogoStorageKey}",
            branding.PrimaryColorHex,
            branding.AccentColorHex,
            branding.LoginHeadline,
            branding.LoginSubheadline,
            branding.NotificationAccentHex,
            resolvedBy);
    }

    public async Task<TenantUsageDetails> GetUsageAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        await EnsureOrgAccessAsync(userId, organizationId, cancellationToken);
        return await GetUsageInternalAsync(organizationId, cancellationToken);
    }

    public async Task EnsureTenantOperationalAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var org = await store.FindOrganizationAsync(organizationId, false, cancellationToken)
            ?? throw DomainException.NotFound("organization_not_found", "That organization was not found.");
        if (!org.IsActive)
        {
            throw DomainException.Validation("tenant_suspended", "This organization is suspended by ProDoc.");
        }

        var subscription = await store.FindSubscriptionAsync(organizationId, false, cancellationToken);
        if (subscription is null || !subscription.IsOperationallyActive(clock.UtcNow))
        {
            throw DomainException.Validation("subscription_inactive", "The organization subscription is not active.");
        }
    }

    public async Task EnsureCanAddClinicAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        await EnsureTenantOperationalAsync(organizationId, cancellationToken);
        var usage = await GetUsageInternalAsync(organizationId, cancellationToken);
        if (usage.ClinicsLimit is int limit && usage.ClinicsUsed >= limit)
        {
            throw DomainException.Validation("saas_clinic_limit", $"Clinic limit reached for plan {usage.PlanCode} ({limit}).");
        }
    }

    public async Task EnsureCanAddStaffAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        await EnsureTenantOperationalAsync(organizationId, cancellationToken);
        var usage = await GetUsageInternalAsync(organizationId, cancellationToken);
        if (usage.StaffLimit is int limit && usage.StaffUsed >= limit)
        {
            throw DomainException.Validation("saas_staff_limit", $"Staff seat limit reached for plan {usage.PlanCode} ({limit}).");
        }
    }

    public async Task EnsureFeatureAsync(Guid organizationId, string featureCode, CancellationToken cancellationToken)
    {
        await EnsureTenantOperationalAsync(organizationId, cancellationToken);
        var usage = await GetUsageInternalAsync(organizationId, cancellationToken);
        if (!usage.EffectiveEntitlements.Any(item => item.FeatureCode == featureCode && item.IsEnabled))
        {
            throw DomainException.Validation("saas_feature_disabled", $"Feature '{featureCode}' is not enabled for this plan.");
        }
    }

    Task<TenantUsageDetails> ITenantEntitlementGuard.GetUsageAsync(Guid organizationId, CancellationToken cancellationToken) =>
        GetUsageInternalAsync(organizationId, cancellationToken);

    public async Task<TenantSubscriptionDetails> GetSubscriptionAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        await EnsureBillingAccessAsync(userId, organizationId, cancellationToken);
        var subscription = await store.FindSubscriptionAsync(organizationId, false, cancellationToken)
            ?? throw DomainException.NotFound("subscription_not_found", "No subscription exists.");
        return MapSubscription(subscription);
    }

    public async Task<TenantSubscriptionDetails> ChangePlanAsync(
        Guid userId,
        Guid organizationId,
        string planCode,
        string billingInterval,
        bool applyImmediately,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await EnsureBillingAccessAsync(userId, organizationId, cancellationToken);
        await EnsureTenantOperationalAsync(organizationId, cancellationToken);
        var plan = await store.FindPlanByCodeAsync(planCode, false, cancellationToken)
            ?? throw DomainException.NotFound("saas_plan_not_found", "That subscription plan was not found.");
        if (!plan.IsActive)
        {
            throw DomainException.Validation("saas_plan_inactive", "That plan is not available.");
        }

        var interval = ParseInterval(billingInterval);
        var subscription = await store.FindSubscriptionAsync(organizationId, true, cancellationToken)
            ?? throw DomainException.NotFound("subscription_not_found", "No subscription exists.");
        var seats = plan.LimitFor(SaaSFeatureCodes.MaxStaff) ?? subscription.SeatLimit;

        if (!applyImmediately && subscription.Status == OrganizationSubscriptionStatus.Active)
        {
            subscription.SchedulePlanChange(plan.Code, clock.UtcNow);
        }
        else
        {
            subscription.AssignPlan(plan.Id, plan.Code, seats, (int)interval, clock.UtcNow);
            if (subscription.Status == OrganizationSubscriptionStatus.Trial && plan.TrialDays == 0)
            {
                subscription.MarkActivePeriod(clock.UtcNow, clock.UtcNow.AddMonths(interval == SaaSBillingInterval.Annual ? 12 : 1), clock.UtcNow);
            }
        }

        await store.AddAuditAsync(Audit(userId, AuditActions.OrganizationSubscriptionUpdated, metadata), cancellationToken);
        try
        {
            await store.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex.GetType().Name.Contains("DbUpdateConcurrency", StringComparison.Ordinal))
        {
            throw DomainException.Conflict("subscription_concurrency", "Another subscription change was saved first. Refresh and try again.");
        }

        return MapSubscription(subscription);
    }

    public async Task<TenantSubscriptionDetails> CancelSubscriptionAsync(
        Guid userId,
        Guid organizationId,
        bool atPeriodEnd,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await EnsureBillingAccessAsync(userId, organizationId, cancellationToken);
        var subscription = await store.FindSubscriptionAsync(organizationId, true, cancellationToken)
            ?? throw DomainException.NotFound("subscription_not_found", "No subscription exists.");
        if (atPeriodEnd)
        {
            subscription.RequestCancelAtPeriodEnd(clock.UtcNow);
        }
        else
        {
            subscription.MarkCancelled(clock.UtcNow);
        }

        await store.AddAuditAsync(Audit(userId, AuditActions.OrganizationSubscriptionUpdated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapSubscription(subscription);
    }

    public async Task<SaaSChargeDetails> StartRenewalPaymentAsync(
        Guid userId,
        Guid organizationId,
        string? idempotencyKey,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await EnsureBillingAccessAsync(userId, organizationId, cancellationToken);
        var org = await store.FindOrganizationAsync(organizationId, false, cancellationToken)
            ?? throw DomainException.NotFound("organization_not_found", "That organization was not found.");
        var subscription = await store.FindSubscriptionAsync(organizationId, true, cancellationToken)
            ?? throw DomainException.NotFound("subscription_not_found", "No subscription exists.");
        var plan = await store.FindPlanByCodeAsync(subscription.PendingPlanCode ?? subscription.PlanCode, false, cancellationToken)
            ?? throw DomainException.NotFound("saas_plan_not_found", "Subscription plan configuration is missing.");

        var key = string.IsNullOrWhiteSpace(idempotencyKey)
            ? $"saas:{organizationId:N}:{subscription.CurrentPeriodEndUtc:yyyyMMddHHmmss}"
            : idempotencyKey.Trim();
        var existing = await store.FindChargeByIdempotencyAsync(key, cancellationToken);
        if (existing is not null)
        {
            return MapCharge(existing);
        }

        var interval = (SaaSBillingInterval)subscription.BillingInterval;
        var amount = interval == SaaSBillingInterval.Annual ? plan.AnnualPrice : plan.MonthlyPrice;
        if (amount <= 0)
        {
            throw DomainException.Validation("saas_amount_zero", "This plan has no self-serve price. Contact ProDoc for custom billing.");
        }

        var periodStart = subscription.CurrentPeriodEndUtc is DateTimeOffset end && end > clock.UtcNow ? end : clock.UtcNow;
        var periodEnd = interval == SaaSBillingInterval.Annual ? periodStart.AddMonths(12) : periodStart.AddMonths(1);
        var invoiceNumber = await store.NextSaaSInvoiceNumberAsync(cancellationToken);
        var invoice = Invoice.Issue(
            invoiceNumber,
            InvoiceKind.SaaSSubscription,
            org.Name,
            subscription.BillingEmail,
            $"ProDoc SaaS {plan.Code} ({interval}) {periodStart:yyyy-MM-dd} → {periodEnd:yyyy-MM-dd}",
            amount,
            0m,
            0m,
            plan.Currency,
            null,
            0m,
            clock.UtcNow,
            accountUserId: userId);
        store.AddInvoice(invoice);

        var charge = SaaSSubscriptionCharge.Create(
            organizationId,
            invoice.Id,
            key,
            interval,
            plan.Code,
            amount,
            plan.Currency,
            payments.ProviderName,
            periodStart,
            periodEnd,
            clock.UtcNow);
        var order = await payments.CreateOrderAsync(
            new GatewayOrderRequest(invoiceNumber, amount, plan.Currency, $"saas:{organizationId}", key),
            cancellationToken);
        charge.AttachProviderOrder(order.OrderId, order.CheckoutPayloadJson, clock.UtcNow);
        store.AddCharge(charge);
        await store.AddAuditAsync(Audit(userId, AuditActions.SaaSChargeCreated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapCharge(charge);
    }

    public async Task<SaaSChargeDetails> ConfirmRenewalPaymentAsync(
        Guid userId,
        Guid organizationId,
        Guid chargeId,
        string? providerPaymentId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await EnsureBillingAccessAsync(userId, organizationId, cancellationToken);
        var charge = await store.FindChargeAsync(chargeId, true, cancellationToken)
            ?? throw DomainException.NotFound("saas_charge_not_found", "That subscription charge was not found.");
        if (charge.OrganizationId != organizationId)
        {
            throw DomainException.NotFound("saas_charge_not_found", "That subscription charge was not found.");
        }

        if (charge.Status == SaaSChargeStatus.Paid)
        {
            return MapCharge(charge);
        }

        if (charge.ProviderOrderId is null)
        {
            throw DomainException.Validation("saas_charge_incomplete", "Checkout was not started for this charge.");
        }

        var verification = await payments.VerifyPaymentAsync(charge.ProviderOrderId, providerPaymentId, cancellationToken);
        if (!verification.Paid)
        {
            charge.MarkFailed(verification.FailureReason ?? "Payment not captured.", clock.UtcNow);
            var subscriptionFailed = await store.FindSubscriptionAsync(organizationId, true, cancellationToken);
            if (subscriptionFailed is not null)
            {
                var plan = await store.FindPlanByCodeAsync(subscriptionFailed.PlanCode, false, cancellationToken);
                var graceDays = plan?.GracePeriodDays ?? 3;
                subscriptionFailed.EnterPastDue(clock.UtcNow.AddDays(graceDays), clock.UtcNow);
            }

            await store.SaveChangesAsync(cancellationToken);
            throw DomainException.Validation("saas_payment_failed", verification.FailureReason ?? "Subscription payment failed.");
        }

        charge.MarkPaid(verification.ProviderPaymentId ?? providerPaymentId, clock.UtcNow);
        await store.MarkInvoicePaidAsync(charge.InvoiceId, cancellationToken);
        var subscription = await store.FindSubscriptionAsync(organizationId, true, cancellationToken)
            ?? throw DomainException.NotFound("subscription_not_found", "No subscription exists.");
        if (!string.IsNullOrWhiteSpace(subscription.PendingPlanCode))
        {
            var pending = await store.FindPlanByCodeAsync(subscription.PendingPlanCode, false, cancellationToken);
            if (pending is not null)
            {
                subscription.AssignPlan(
                    pending.Id,
                    pending.Code,
                    pending.LimitFor(SaaSFeatureCodes.MaxStaff) ?? subscription.SeatLimit,
                    (int)charge.Interval,
                    clock.UtcNow);
            }
        }

        subscription.MarkActivePeriod(charge.PeriodStartUtc, charge.PeriodEndUtc, clock.UtcNow);
        subscription.ClearCancellation(clock.UtcNow);
        await store.AddAuditAsync(Audit(userId, AuditActions.SaaSChargePaid, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapCharge(charge);
    }

    public async Task<IReadOnlyList<SaaSChargeDetails>> ListBillingHistoryAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        await EnsureBillingAccessAsync(userId, organizationId, cancellationToken);
        var rows = await store.ListChargesAsync(organizationId, 50, cancellationToken);
        return rows.Select(MapCharge).ToArray();
    }

    public async Task<PlatformSaaSMetrics> PlatformMetricsAsync(CancellationToken cancellationToken)
    {
        var orgs = await store.ListOrganizationsAsync(cancellationToken);
        var tenants = new List<PlatformTenantSummary>();
        var trial = 0;
        var pastDue = 0;
        var suspended = 0;
        var active = 0;
        foreach (var org in orgs)
        {
            var subscription = await store.FindSubscriptionAsync(org.Id, false, cancellationToken);
            var clinics = await store.CountClinicsAsync(org.Id, cancellationToken);
            var members = await store.CountStaffAsync(org.Id, cancellationToken);
            var status = subscription?.Status.ToString() ?? "None";
            switch (subscription?.Status)
            {
                case OrganizationSubscriptionStatus.Trial: trial++; break;
                case OrganizationSubscriptionStatus.PastDue: pastDue++; break;
                case OrganizationSubscriptionStatus.Suspended: suspended++; break;
                case OrganizationSubscriptionStatus.Active: active++; break;
            }

            tenants.Add(new PlatformTenantSummary(
                org.Id,
                org.Name,
                org.Kind.ToString(),
                org.IsActive,
                subscription?.PlanCode ?? "NONE",
                status,
                subscription?.CurrentPeriodEndUtc,
                clinics,
                members,
                0m));
        }

        return new PlatformSaaSMetrics(
            active,
            trial,
            pastDue,
            suspended,
            await store.SumSaaSRevenueAsync(cancellationToken),
            await store.SumCommissionRevenueAsync(cancellationToken),
            tenants.OrderBy(item => item.Name).ToArray());
    }

    public async Task<PlatformTenantSummary> PlatformSuspendTenantAsync(
        Guid actorUserId,
        Guid organizationId,
        bool suspend,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var org = await store.FindOrganizationAsync(organizationId, true, cancellationToken)
            ?? throw DomainException.NotFound("organization_not_found", "That organization was not found.");
        org.SetActive(!suspend, clock.UtcNow);
        var subscription = await store.FindSubscriptionAsync(organizationId, true, cancellationToken);
        if (subscription is not null)
        {
            if (suspend)
            {
                subscription.Suspend(clock.UtcNow);
            }
            else if (subscription.Status == OrganizationSubscriptionStatus.Suspended)
            {
                subscription.Update(subscription.PlanCode, OrganizationSubscriptionStatus.Active, subscription.SeatLimit, subscription.BillingEmail, subscription.CurrentPeriodEndUtc, clock.UtcNow);
            }
        }

        await store.AddAuditAsync(
            Audit(actorUserId, suspend ? AuditActions.TenantSuspended : AuditActions.TenantReactivated, metadata),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        var metrics = await PlatformMetricsAsync(cancellationToken);
        return metrics.Tenants.Single(item => item.OrganizationId == organizationId);
    }

    public async Task<TenantUsageDetails> PlatformSetEntitlementAsync(
        Guid actorUserId,
        Guid organizationId,
        string featureCode,
        bool enabled,
        int? limitValue,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        _ = await store.FindOrganizationAsync(organizationId, false, cancellationToken)
            ?? throw DomainException.NotFound("organization_not_found", "That organization was not found.");
        var existing = await store.FindOverrideAsync(organizationId, featureCode, true, cancellationToken);
        if (existing is null)
        {
            store.AddOverride(OrganizationFeatureOverride.Create(organizationId, featureCode, enabled, limitValue, clock.UtcNow));
        }
        else
        {
            existing.Update(enabled, limitValue, clock.UtcNow);
        }

        await store.AddAuditAsync(Audit(actorUserId, AuditActions.TenantEntitlementChanged, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return await GetUsageInternalAsync(organizationId, cancellationToken);
    }

    private async Task EnsurePlanAsync(
        string code,
        string name,
        string description,
        decimal monthly,
        decimal annual,
        int trialDays,
        int grace,
        int refundDays,
        bool isPublic,
        bool isCustom,
        int sort,
        (string Feature, bool Enabled, int? Limit)[] entitlements,
        CancellationToken cancellationToken)
    {
        var rows = entitlements.Select(item => (item.Feature, item.Enabled, item.Limit)).ToArray();
        var existing = await store.FindPlanByCodeAsync(code, true, cancellationToken);
        if (existing is null)
        {
            existing = SaaSPlan.Create(code, name, description, monthly, annual, trialDays, grace, refundDays, isPublic, isCustom, sort, clock.UtcNow);
            store.AddPlan(existing);
            existing.SyncEntitlements(rows, clock.UtcNow);
            await store.SaveChangesAsync(cancellationToken);
            return;
        }

        var metadataChanged =
            !string.Equals(existing.Name, name.Trim(), StringComparison.Ordinal)
            || !string.Equals(existing.Description, description.Trim(), StringComparison.Ordinal)
            || existing.MonthlyPrice != decimal.Round(monthly, 2)
            || existing.AnnualPrice != decimal.Round(annual, 2)
            || existing.TrialDays != Math.Clamp(trialDays, 0, 365)
            || existing.GracePeriodDays != Math.Clamp(grace, 0, 60)
            || existing.RefundPolicyDays != Math.Clamp(refundDays, 0, 90)
            || existing.IsPublic != isPublic
            || existing.IsCustom != isCustom
            || existing.SortOrder != sort
            || !existing.IsActive;

        if (metadataChanged)
        {
            existing.Update(name, description, monthly, annual, trialDays, grace, refundDays, isPublic, isActive: true, sort, clock.UtcNow);
        }

        var entitlementsChanged = existing.SyncEntitlements(rows, clock.UtcNow);
        if (!metadataChanged && !entitlementsChanged)
        {
            return;
        }

        await store.SavePlanSeedAsync(cancellationToken);
    }

    private async Task<TenantUsageDetails> GetUsageInternalAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var subscription = await store.FindSubscriptionAsync(organizationId, false, cancellationToken)
            ?? throw DomainException.NotFound("subscription_not_found", "No subscription exists.");
        var plan = subscription.PlanId is Guid planId
            ? await store.FindPlanByIdAsync(planId, false, cancellationToken)
            : await store.FindPlanByCodeAsync(subscription.PlanCode, false, cancellationToken);
        var overrides = await store.ListOverridesAsync(organizationId, cancellationToken);
        var effective = MergeEntitlements(plan, overrides);
        return new TenantUsageDetails(
            organizationId,
            await store.CountClinicsAsync(organizationId, cancellationToken),
            Limit(effective, SaaSFeatureCodes.MaxClinics),
            await store.CountBranchesAsync(organizationId, cancellationToken),
            Limit(effective, SaaSFeatureCodes.MaxBranches),
            await store.CountDoctorsAsync(organizationId, cancellationToken),
            Limit(effective, SaaSFeatureCodes.MaxDoctors),
            await store.CountStaffAsync(organizationId, cancellationToken),
            Limit(effective, SaaSFeatureCodes.MaxStaff),
            0,
            Limit(effective, SaaSFeatureCodes.MaxStorageMb),
            subscription.Status.ToString(),
            subscription.PlanCode,
            subscription.CurrentPeriodEndUtc,
            subscription.GracePeriodEndsUtc,
            subscription.CancelAtPeriodEnd,
            effective);
    }

    private static IReadOnlyList<SaaSEntitlementDetails> MergeEntitlements(
        SaaSPlan? plan,
        IReadOnlyList<OrganizationFeatureOverride> overrides)
    {
        var map = new Dictionary<string, SaaSEntitlementDetails>(StringComparer.OrdinalIgnoreCase);
        if (plan is not null)
        {
            foreach (var item in plan.Entitlements)
            {
                map[item.FeatureCode] = new SaaSEntitlementDetails(item.FeatureCode, item.IsEnabled, item.LimitValue);
            }
        }

        foreach (var item in overrides)
        {
            map[item.FeatureCode] = new SaaSEntitlementDetails(item.FeatureCode, item.IsEnabled, item.LimitValue);
        }

        return map.Values.OrderBy(item => item.FeatureCode).ToArray();
    }

    private static int? Limit(IReadOnlyList<SaaSEntitlementDetails> entitlements, string code) =>
        entitlements.FirstOrDefault(item => item.FeatureCode == code && item.IsEnabled)?.LimitValue;

    private async Task<OrganizationBrandingDetails> MapBrandingAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var org = await store.FindOrganizationAsync(organizationId, false, cancellationToken)
            ?? throw DomainException.NotFound("organization_not_found", "That organization was not found.");
        var branding = await store.FindBrandingAsync(organizationId, false, cancellationToken)
            ?? OrganizationBranding.CreateDefault(organizationId, org.Name, clock.UtcNow);
        var usage = await GetUsageInternalAsync(organizationId, cancellationToken);
        return new OrganizationBrandingDetails(
            organizationId,
            org.Name,
            branding.LogoStorageKey,
            branding.PrimaryColorHex,
            branding.AccentColorHex,
            branding.LoginHeadline,
            branding.LoginSubheadline,
            branding.EmailFromName,
            branding.EmailFooterText,
            branding.NotificationAccentHex,
            branding.CustomDomain,
            branding.SubdomainSlug,
            usage.EffectiveEntitlements.Any(item => item.FeatureCode == SaaSFeatureCodes.WhiteLabel && item.IsEnabled),
            usage.EffectiveEntitlements.Any(item => item.FeatureCode == SaaSFeatureCodes.CustomDomain && item.IsEnabled));
    }

    private async Task EnsureOrgAccessAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        var membership = await store.FindMembershipAsync(organizationId, userId, cancellationToken);
        if (membership is not { IsActive: true })
        {
            throw DomainException.NotFound("organization_not_found", "That organization was not found.");
        }
    }

    private async Task EnsureOrgManageAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        var membership = await store.FindMembershipAsync(organizationId, userId, cancellationToken);
        if (membership is not { IsActive: true } || !OrganizationRoles.CanManageOrganization(membership.Role))
        {
            throw DomainException.NotFound("organization_not_found", "That organization was not found.");
        }
    }

    private async Task EnsureBillingAccessAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        var membership = await store.FindMembershipAsync(organizationId, userId, cancellationToken);
        if (membership is not { IsActive: true } || !OrganizationRoles.CanManageBilling(membership.Role))
        {
            throw DomainException.NotFound("organization_not_found", "That organization was not found.");
        }
    }

    private static SaaSBillingInterval ParseInterval(string value) =>
        Enum.TryParse<SaaSBillingInterval>(value, true, out var parsed)
            ? parsed
            : throw DomainException.Validation("billing_interval_invalid", "Choose Monthly or Annual.");

    private static SaaSPlanDetails MapPlan(SaaSPlan plan) =>
        new(
            plan.Id,
            plan.Code,
            plan.Name,
            plan.Description,
            plan.MonthlyPrice,
            plan.AnnualPrice,
            plan.Currency,
            plan.TrialDays,
            plan.GracePeriodDays,
            plan.RefundPolicyDays,
            plan.IsPublic,
            plan.IsActive,
            plan.IsCustom,
            plan.SortOrder,
            plan.Entitlements.Select(item => new SaaSEntitlementDetails(item.FeatureCode, item.IsEnabled, item.LimitValue)).ToArray());

    private static TenantSubscriptionDetails MapSubscription(OrganizationSubscription subscription) =>
        new(
            subscription.OrganizationId,
            subscription.PlanCode,
            subscription.PlanId,
            subscription.Status.ToString(),
            subscription.SeatLimit,
            ((SaaSBillingInterval)subscription.BillingInterval).ToString(),
            subscription.BillingEmail,
            subscription.CurrentPeriodStartUtc,
            subscription.CurrentPeriodEndUtc,
            subscription.TrialEndsUtc,
            subscription.GracePeriodEndsUtc,
            subscription.CancelAtPeriodEnd,
            subscription.PendingPlanCode,
            "SaaS subscription revenue is invoiced separately from appointment commission settlements.");

    private static SaaSChargeDetails MapCharge(SaaSSubscriptionCharge charge) =>
        new(
            charge.Id,
            charge.OrganizationId,
            charge.InvoiceId,
            charge.Status.ToString(),
            charge.Interval.ToString(),
            charge.PlanCodeSnapshot,
            charge.Amount,
            charge.Currency,
            charge.ProviderOrderId,
            charge.CheckoutPayload,
            charge.PeriodStartUtc,
            charge.PeriodEndUtc,
            charge.CreatedAtUtc,
            charge.PaidAtUtc);

    private AuditLog Audit(Guid userId, string action, RequestMetadata metadata) =>
        AuditLog.Record(userId, action, clock.UtcNow, metadata.IpAddress, metadata.UserAgent);
}
