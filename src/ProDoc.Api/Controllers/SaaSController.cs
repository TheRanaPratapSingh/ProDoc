using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Providers;
using ProDoc.Application.SaaS;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class SaaSController(ISaaSService saas, IDocumentStorage documents) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("public/branding")]
    public Task<PublicTenantBranding?> ResolveBranding(
        [FromQuery] string? host,
        [FromQuery] string? slug,
        CancellationToken cancellationToken)
    {
        var resolvedHost = host ?? Request.Headers.Host.ToString();
        return saas.ResolvePublicBrandingAsync(resolvedHost, slug, cancellationToken);
    }

    [AllowAnonymous]
    [HttpGet("public/tenant-assets/{storageKey}")]
    public async Task<IActionResult> TenantAsset(string storageKey, CancellationToken cancellationToken)
    {
        var stream = await documents.OpenReadAsync(storageKey, cancellationToken);
        var contentType = storageKey.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png"
            : storageKey.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ? "image/webp"
            : "image/jpeg";
        return File(stream, contentType);
    }

    [AllowAnonymous]
    [HttpGet("saas/plans")]
    public Task<IReadOnlyList<SaaSPlanDetails>> Plans(CancellationToken cancellationToken) =>
        saas.ListPlansAsync(publicOnly: true, cancellationToken);

    [Authorize]
    [HasPermission(PermissionCodes.OrgManage)]
    [HttpGet("organizations/{organizationId:guid}/branding")]
    public Task<OrganizationBrandingDetails> GetBranding(Guid organizationId, CancellationToken cancellationToken) =>
        saas.GetBrandingAsync(User.GetUserId(), organizationId, cancellationToken);

    [Authorize]
    [HasPermission(PermissionCodes.OrgManage)]
    [HttpPut("organizations/{organizationId:guid}/branding")]
    public Task<OrganizationBrandingDetails> PutBranding(
        Guid organizationId,
        UpdateBrandingRequest request,
        CancellationToken cancellationToken) =>
        saas.UpdateBrandingAsync(
            User.GetUserId(),
            organizationId,
            request.PrimaryColorHex,
            request.AccentColorHex,
            request.LoginHeadline,
            request.LoginSubheadline,
            request.EmailFromName,
            request.EmailFooterText,
            request.NotificationAccentHex,
            request.CustomDomain,
            request.SubdomainSlug,
            Meta(),
            cancellationToken);

    [Authorize]
    [HasPermission(PermissionCodes.OrgManage)]
    [HttpPost("organizations/{organizationId:guid}/branding/logo")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<OrganizationBrandingDetails> UploadLogo(Guid organizationId, IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return await saas.UploadLogoAsync(
            User.GetUserId(),
            organizationId,
            stream,
            file.FileName,
            file.ContentType,
            file.Length,
            Meta(),
            cancellationToken);
    }

    [Authorize]
    [HasPermission(PermissionCodes.OrgAccess)]
    [HttpGet("organizations/{organizationId:guid}/usage")]
    public Task<TenantUsageDetails> Usage(Guid organizationId, CancellationToken cancellationToken) =>
        saas.GetUsageAsync(User.GetUserId(), organizationId, cancellationToken);

    [Authorize]
    [HasPermission(PermissionCodes.OrgBilling)]
    [HttpGet("organizations/{organizationId:guid}/saas/subscription")]
    public Task<TenantSubscriptionDetails> Subscription(Guid organizationId, CancellationToken cancellationToken) =>
        saas.GetSubscriptionAsync(User.GetUserId(), organizationId, cancellationToken);

    [Authorize]
    [HasPermission(PermissionCodes.OrgBilling)]
    [HttpPost("organizations/{organizationId:guid}/saas/change-plan")]
    public Task<TenantSubscriptionDetails> ChangePlan(
        Guid organizationId,
        ChangePlanRequest request,
        CancellationToken cancellationToken) =>
        saas.ChangePlanAsync(
            User.GetUserId(),
            organizationId,
            request.PlanCode,
            request.BillingInterval,
            request.ApplyImmediately,
            Meta(),
            cancellationToken);

    [Authorize]
    [HasPermission(PermissionCodes.OrgBilling)]
    [HttpPost("organizations/{organizationId:guid}/saas/cancel")]
    public Task<TenantSubscriptionDetails> Cancel(
        Guid organizationId,
        CancelSubscriptionRequest request,
        CancellationToken cancellationToken) =>
        saas.CancelSubscriptionAsync(User.GetUserId(), organizationId, request.AtPeriodEnd, Meta(), cancellationToken);

    [Authorize]
    [HasPermission(PermissionCodes.OrgBilling)]
    [HttpPost("organizations/{organizationId:guid}/saas/charges")]
    public Task<SaaSChargeDetails> StartCharge(
        Guid organizationId,
        StartSaaSChargeRequest request,
        CancellationToken cancellationToken) =>
        saas.StartRenewalPaymentAsync(User.GetUserId(), organizationId, request.IdempotencyKey, Meta(), cancellationToken);

    [Authorize]
    [HasPermission(PermissionCodes.OrgBilling)]
    [HttpPost("organizations/{organizationId:guid}/saas/charges/{chargeId:guid}/confirm")]
    public Task<SaaSChargeDetails> ConfirmCharge(
        Guid organizationId,
        Guid chargeId,
        ConfirmSaaSChargeRequest request,
        CancellationToken cancellationToken) =>
        saas.ConfirmRenewalPaymentAsync(
            User.GetUserId(),
            organizationId,
            chargeId,
            request.ProviderPaymentId,
            Meta(),
            cancellationToken);

    [Authorize]
    [HasPermission(PermissionCodes.OrgBilling)]
    [HttpGet("organizations/{organizationId:guid}/saas/charges")]
    public Task<IReadOnlyList<SaaSChargeDetails>> Charges(Guid organizationId, CancellationToken cancellationToken) =>
        saas.ListBillingHistoryAsync(User.GetUserId(), organizationId, cancellationToken);

    [Authorize]
    [HasPermission(PermissionCodes.TenantsManage)]
    [HttpGet("admin/tenants")]
    public Task<PlatformSaaSMetrics> PlatformTenants(CancellationToken cancellationToken) =>
        saas.PlatformMetricsAsync(cancellationToken);

    [Authorize]
    [HasPermission(PermissionCodes.TenantsManage)]
    [HttpGet("admin/saas/plans")]
    public Task<IReadOnlyList<SaaSPlanDetails>> AdminPlans(CancellationToken cancellationToken) =>
        saas.ListPlansAsync(publicOnly: false, cancellationToken);

    [Authorize]
    [HasPermission(PermissionCodes.TenantsManage)]
    [HttpPut("admin/saas/plans/{code}")]
    public Task<SaaSPlanDetails> UpsertPlan(string code, UpsertSaaSPlanRequest request, CancellationToken cancellationToken) =>
        saas.UpsertPlanAsync(
            User.GetUserId(),
            code,
            request.Name,
            request.Description,
            request.MonthlyPrice,
            request.AnnualPrice,
            request.TrialDays,
            request.GracePeriodDays,
            request.RefundPolicyDays,
            request.IsPublic,
            request.IsActive,
            request.IsCustom,
            request.SortOrder,
            request.Entitlements,
            Meta(),
            cancellationToken);

    [Authorize]
    [HasPermission(PermissionCodes.TenantsManage)]
    [HttpPost("admin/tenants/{organizationId:guid}/suspend")]
    public Task<PlatformTenantSummary> Suspend(Guid organizationId, SuspendTenantRequest request, CancellationToken cancellationToken) =>
        saas.PlatformSuspendTenantAsync(User.GetUserId(), organizationId, request.Suspend, Meta(), cancellationToken);

    [Authorize]
    [HasPermission(PermissionCodes.TenantsManage)]
    [HttpPut("admin/tenants/{organizationId:guid}/entitlements/{featureCode}")]
    public Task<TenantUsageDetails> SetEntitlement(
        Guid organizationId,
        string featureCode,
        SetEntitlementRequest request,
        CancellationToken cancellationToken) =>
        saas.PlatformSetEntitlementAsync(
            User.GetUserId(),
            organizationId,
            featureCode,
            request.Enabled,
            request.LimitValue,
            Meta(),
            cancellationToken);

    private RequestMetadata Meta() => new(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext));
}

public sealed record UpdateBrandingRequest(
    string PrimaryColorHex,
    string AccentColorHex,
    string? LoginHeadline,
    string? LoginSubheadline,
    string? EmailFromName,
    string? EmailFooterText,
    string? NotificationAccentHex,
    string? CustomDomain,
    string? SubdomainSlug);

public sealed record ChangePlanRequest(string PlanCode, string BillingInterval, bool ApplyImmediately = true);

public sealed record CancelSubscriptionRequest(bool AtPeriodEnd = true);

public sealed record StartSaaSChargeRequest(string? IdempotencyKey);

public sealed record ConfirmSaaSChargeRequest(string? ProviderPaymentId);

public sealed record UpsertSaaSPlanRequest(
    string Name,
    string Description,
    decimal MonthlyPrice,
    decimal AnnualPrice,
    int TrialDays,
    int GracePeriodDays,
    int RefundPolicyDays,
    bool IsPublic,
    bool IsActive,
    bool IsCustom,
    int SortOrder,
    IReadOnlyList<SaaSEntitlementDetails> Entitlements);

public sealed record SuspendTenantRequest(bool Suspend = true);

public sealed record SetEntitlementRequest(bool Enabled, int? LimitValue);
