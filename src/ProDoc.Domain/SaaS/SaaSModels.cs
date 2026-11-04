using ProDoc.Domain.Common;

namespace ProDoc.Domain.SaaS;

public enum SaaSBillingInterval
{
    Monthly = 1,
    Annual = 2
}

public enum SaaSChargeStatus
{
    Created = 1,
    Pending = 2,
    Paid = 3,
    Failed = 4,
    Refunded = 5,
    Cancelled = 6
}

public sealed class SaaSPlan
{
    private readonly List<SaaSPlanEntitlement> _entitlements = [];

    private SaaSPlan()
    {
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public decimal MonthlyPrice { get; private set; }

    public decimal AnnualPrice { get; private set; }

    public string Currency { get; private set; } = "INR";

    public int TrialDays { get; private set; }

    public int GracePeriodDays { get; private set; }

    public int RefundPolicyDays { get; private set; }

    public bool IsPublic { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsCustom { get; private set; }

    public int SortOrder { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<SaaSPlanEntitlement> Entitlements => _entitlements;

    public static SaaSPlan Create(
        string code,
        string name,
        string description,
        decimal monthlyPrice,
        decimal annualPrice,
        int trialDays,
        int gracePeriodDays,
        int refundPolicyDays,
        bool isPublic,
        bool isCustom,
        int sortOrder,
        DateTimeOffset now)
    {
        var plan = new SaaSPlan
        {
            Id = Guid.NewGuid(),
            Code = NormalizeCode(code),
            Name = name.Trim(),
            Description = description.Trim(),
            MonthlyPrice = decimal.Round(monthlyPrice, 2),
            AnnualPrice = decimal.Round(annualPrice, 2),
            TrialDays = Math.Clamp(trialDays, 0, 365),
            GracePeriodDays = Math.Clamp(gracePeriodDays, 0, 60),
            RefundPolicyDays = Math.Clamp(refundPolicyDays, 0, 90),
            IsPublic = isPublic,
            IsActive = true,
            IsCustom = isCustom,
            SortOrder = sortOrder,
            UpdatedAtUtc = now
        };
        if (plan.Name.Length is < 2 or > 80)
        {
            throw DomainException.Validation("saas_plan_name_invalid", "Plan name must be 2–80 characters.");
        }

        if (monthlyPrice < 0 || annualPrice < 0)
        {
            throw DomainException.Validation("saas_plan_price_invalid", "Plan prices cannot be negative.");
        }

        return plan;
    }

    public void Update(
        string name,
        string description,
        decimal monthlyPrice,
        decimal annualPrice,
        int trialDays,
        int gracePeriodDays,
        int refundPolicyDays,
        bool isPublic,
        bool isActive,
        int sortOrder,
        DateTimeOffset now)
    {
        Name = name.Trim();
        Description = description.Trim();
        MonthlyPrice = decimal.Round(monthlyPrice, 2);
        AnnualPrice = decimal.Round(annualPrice, 2);
        TrialDays = Math.Clamp(trialDays, 0, 365);
        GracePeriodDays = Math.Clamp(gracePeriodDays, 0, 60);
        RefundPolicyDays = Math.Clamp(refundPolicyDays, 0, 90);
        IsPublic = isPublic;
        IsActive = isActive;
        SortOrder = sortOrder;
        UpdatedAtUtc = now;
    }

    public void ReplaceEntitlements(IEnumerable<(string FeatureCode, bool Enabled, int? LimitValue)> rows, DateTimeOffset now)
    {
        _entitlements.Clear();
        foreach (var row in rows)
        {
            _entitlements.Add(SaaSPlanEntitlement.Create(Id, row.FeatureCode, row.Enabled, row.LimitValue));
        }

        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Idempotent entitlement sync used by seeders — updates/adds/removes in place to avoid
    /// delete-all + insert-all concurrency collisions on restart.
    /// </summary>
    public bool SyncEntitlements(IEnumerable<(string FeatureCode, bool Enabled, int? LimitValue)> rows, DateTimeOffset now)
    {
        var desired = rows
            .Select(row => (
                FeatureCode: row.FeatureCode.Trim().ToLowerInvariant(),
                row.Enabled,
                row.LimitValue))
            .GroupBy(row => row.FeatureCode, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToArray();

        var changed = false;
        for (var index = _entitlements.Count - 1; index >= 0; index--)
        {
            var current = _entitlements[index];
            if (desired.All(row => row.FeatureCode != current.FeatureCode))
            {
                _entitlements.RemoveAt(index);
                changed = true;
            }
        }

        foreach (var row in desired)
        {
            var existing = _entitlements.FirstOrDefault(item => item.FeatureCode == row.FeatureCode);
            if (existing is null)
            {
                _entitlements.Add(SaaSPlanEntitlement.Create(Id, row.FeatureCode, row.Enabled, row.LimitValue));
                changed = true;
                continue;
            }

            if (existing.IsEnabled != row.Enabled || existing.LimitValue != row.LimitValue)
            {
                existing.Update(row.Enabled, row.LimitValue);
                changed = true;
            }
        }

        if (changed)
        {
            UpdatedAtUtc = now;
        }

        return changed;
    }

    public int? LimitFor(string featureCode) =>
        _entitlements.FirstOrDefault(item => item.FeatureCode == featureCode && item.IsEnabled)?.LimitValue;

    public bool HasFeature(string featureCode) =>
        _entitlements.Any(item => item.FeatureCode == featureCode && item.IsEnabled);

    private static string NormalizeCode(string code)
    {
        var value = code.Trim().ToUpperInvariant();
        if (value.Length is < 2 or > 40)
        {
            throw DomainException.Validation("saas_plan_code_invalid", "Plan code must be 2–40 characters.");
        }

        return value;
    }
}

public sealed class SaaSPlanEntitlement
{
    private SaaSPlanEntitlement()
    {
    }

    public Guid Id { get; private set; }

    public Guid PlanId { get; private set; }

    public string FeatureCode { get; private set; } = string.Empty;

    public bool IsEnabled { get; private set; }

    public int? LimitValue { get; private set; }

    internal static SaaSPlanEntitlement Create(Guid planId, string featureCode, bool enabled, int? limitValue)
    {
        var code = featureCode.Trim().ToLowerInvariant();
        if (code.Length is < 3 or > 80)
        {
            throw DomainException.Validation("saas_feature_invalid", "Feature code is invalid.");
        }

        if (limitValue is < 0)
        {
            throw DomainException.Validation("saas_limit_invalid", "Feature limits cannot be negative.");
        }

        return new SaaSPlanEntitlement
        {
            Id = Guid.NewGuid(),
            PlanId = planId,
            FeatureCode = code,
            IsEnabled = enabled,
            LimitValue = limitValue
        };
    }

    internal void Update(bool enabled, int? limitValue)
    {
        if (limitValue is < 0)
        {
            throw DomainException.Validation("saas_limit_invalid", "Feature limits cannot be negative.");
        }

        IsEnabled = enabled;
        LimitValue = limitValue;
    }
}

public sealed class OrganizationBranding
{
    private OrganizationBranding()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string? LogoStorageKey { get; private set; }

    public string PrimaryColorHex { get; private set; } = "#0F766E";

    public string AccentColorHex { get; private set; } = "#134E4A";

    public string? LoginHeadline { get; private set; }

    public string? LoginSubheadline { get; private set; }

    public string? EmailFromName { get; private set; }

    public string? EmailFooterText { get; private set; }

    public string? NotificationAccentHex { get; private set; }

    public string? CustomDomain { get; private set; }

    public string? SubdomainSlug { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static OrganizationBranding CreateDefault(Guid organizationId, string organizationName, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            LoginHeadline = organizationName,
            LoginSubheadline = "Care, in person.",
            EmailFromName = organizationName,
            EmailFooterText = $"© {organizationName}. Powered by ProDoc.",
            UpdatedAtUtc = now
        };

    public void Update(
        string primaryColorHex,
        string accentColorHex,
        string? loginHeadline,
        string? loginSubheadline,
        string? emailFromName,
        string? emailFooterText,
        string? notificationAccentHex,
        string? customDomain,
        string? subdomainSlug,
        DateTimeOffset now)
    {
        PrimaryColorHex = NormalizeHex(primaryColorHex, "primary");
        AccentColorHex = NormalizeHex(accentColorHex, "accent");
        LoginHeadline = TrimOrNull(loginHeadline, 120);
        LoginSubheadline = TrimOrNull(loginSubheadline, 200);
        EmailFromName = TrimOrNull(emailFromName, 120);
        EmailFooterText = TrimOrNull(emailFooterText, 500);
        NotificationAccentHex = string.IsNullOrWhiteSpace(notificationAccentHex)
            ? null
            : NormalizeHex(notificationAccentHex, "notification");
        CustomDomain = NormalizeDomain(customDomain);
        SubdomainSlug = NormalizeSlug(subdomainSlug);
        UpdatedAtUtc = now;
    }

    public void SetLogo(string? storageKey, DateTimeOffset now)
    {
        LogoStorageKey = string.IsNullOrWhiteSpace(storageKey) ? null : storageKey.Trim();
        UpdatedAtUtc = now;
    }

    private static string NormalizeHex(string value, string label)
    {
        var text = value.Trim();
        if (text.Length == 7 && text[0] == '#' && text[1..].All(Uri.IsHexDigit))
        {
            return text.ToUpperInvariant();
        }

        throw DomainException.Validation("branding_color_invalid", $"Enter a valid #{label} hex color (e.g. #0F766E).");
    }

    private static string? NormalizeDomain(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var host = value.Trim().ToLowerInvariant();
        if (host.StartsWith("http://", StringComparison.Ordinal) || host.StartsWith("https://", StringComparison.Ordinal))
        {
            throw DomainException.Validation("custom_domain_invalid", "Enter a hostname only (no http/https).");
        }

        if (host.Length is < 3 or > 200 || host.Contains('/') || host.Contains(' '))
        {
            throw DomainException.Validation("custom_domain_invalid", "Enter a valid custom domain hostname.");
        }

        return host;
    }

    private static string? NormalizeSlug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var slug = value.Trim().ToLowerInvariant();
        if (slug.Length is < 2 or > 63 || !slug.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '-'))
        {
            throw DomainException.Validation("subdomain_invalid", "Subdomain slug may use letters, digits, and hyphens (2–63).");
        }

        return slug;
    }

    private static string? TrimOrNull(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        return text.Length <= max ? text : text[..max];
    }
}

public sealed class OrganizationFeatureOverride
{
    private OrganizationFeatureOverride()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string FeatureCode { get; private set; } = string.Empty;

    public bool IsEnabled { get; private set; }

    public int? LimitValue { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static OrganizationFeatureOverride Create(
        Guid organizationId,
        string featureCode,
        bool enabled,
        int? limitValue,
        DateTimeOffset now)
    {
        var code = featureCode.Trim().ToLowerInvariant();
        return new OrganizationFeatureOverride
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            FeatureCode = code,
            IsEnabled = enabled,
            LimitValue = limitValue,
            UpdatedAtUtc = now
        };
    }

    public void Update(bool enabled, int? limitValue, DateTimeOffset now)
    {
        IsEnabled = enabled;
        LimitValue = limitValue;
        UpdatedAtUtc = now;
    }
}

public sealed class SaaSSubscriptionCharge
{
    private SaaSSubscriptionCharge()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid InvoiceId { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public SaaSChargeStatus Status { get; private set; }

    public SaaSBillingInterval Interval { get; private set; }

    public string PlanCodeSnapshot { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = "INR";

    public string Provider { get; private set; } = string.Empty;

    public string? ProviderOrderId { get; private set; }

    public string? ProviderPaymentId { get; private set; }

    public string? CheckoutPayload { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset PeriodStartUtc { get; private set; }

    public DateTimeOffset PeriodEndUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? PaidAtUtc { get; private set; }

    public static SaaSSubscriptionCharge Create(
        Guid organizationId,
        Guid invoiceId,
        string idempotencyKey,
        SaaSBillingInterval interval,
        string planCode,
        decimal amount,
        string currency,
        string provider,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        DateTimeOffset now)
    {
        if (amount <= 0)
        {
            throw DomainException.Validation("saas_charge_amount_invalid", "Subscription charge must be greater than zero.");
        }

        return new SaaSSubscriptionCharge
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            InvoiceId = invoiceId,
            IdempotencyKey = idempotencyKey.Trim(),
            Status = SaaSChargeStatus.Created,
            Interval = interval,
            PlanCodeSnapshot = planCode.Trim().ToUpperInvariant(),
            Amount = decimal.Round(amount, 2),
            Currency = currency,
            Provider = provider,
            PeriodStartUtc = periodStart,
            PeriodEndUtc = periodEnd,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void AttachProviderOrder(string orderId, string? checkoutPayload, DateTimeOffset now)
    {
        ProviderOrderId = orderId;
        CheckoutPayload = checkoutPayload;
        Status = SaaSChargeStatus.Pending;
        UpdatedAtUtc = now;
    }

    public void MarkPaid(string? providerPaymentId, DateTimeOffset now)
    {
        Status = SaaSChargeStatus.Paid;
        ProviderPaymentId = providerPaymentId;
        PaidAtUtc = now;
        UpdatedAtUtc = now;
    }

    public void MarkFailed(string reason, DateTimeOffset now)
    {
        Status = SaaSChargeStatus.Failed;
        FailureReason = reason.Trim();
        UpdatedAtUtc = now;
    }

    public void MarkRefunded(DateTimeOffset now)
    {
        Status = SaaSChargeStatus.Refunded;
        UpdatedAtUtc = now;
    }
}
