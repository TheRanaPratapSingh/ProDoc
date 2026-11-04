using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProDoc.Api.Auth;
using ProDoc.Application.Integrations;
using ProDoc.Application.Providers;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class IntegrationsController(IntegrationService integrations) : ControllerBase
{
    [HasPermission(PermissionCodes.IntegrationsUse)]
    [HttpGet("integrations/status")]
    public Task<IntegrationStatusOverview> Status(CancellationToken cancellationToken) =>
        integrations.GetStatusAsync(User.GetUserId(), cancellationToken);

    [HasPermission(PermissionCodes.IntegrationsUse)]
    [HttpGet("integrations/providers")]
    public Task<IReadOnlyList<IntegrationProviderDetails>> Providers(CancellationToken cancellationToken) =>
        integrations.ListProvidersAsync(cancellationToken);

    [HasPermission(PermissionCodes.IntegrationsManage)]
    [HttpPut("admin/integrations/providers/{providerId:guid}")]
    public Task<IntegrationProviderDetails> ConfigureProvider(Guid providerId, ConfigureIntegrationProviderRequest request, CancellationToken cancellationToken) =>
        integrations.ConfigureProviderAsync(
            User.GetUserId(),
            providerId,
            request.Mode,
            request.IsActive,
            request.BaseUrl,
            request.CredentialReference,
            Meta(),
            cancellationToken);

    [HasPermission(PermissionCodes.IntegrationsUse)]
    [HttpGet("integrations/consents")]
    public Task<IReadOnlyList<IntegrationConsentDetails>> Consents(CancellationToken cancellationToken) =>
        integrations.ListConsentsAsync(User.GetUserId(), cancellationToken);

    [HasPermission(PermissionCodes.IntegrationsUse)]
    [HttpPost("integrations/consents")]
    public Task<IntegrationConsentDetails> GrantConsent(IntegrationConsentRequest request, CancellationToken cancellationToken) =>
        integrations.GrantConsentAsync(User.GetUserId(), request.ProviderKind, request.Purpose, request.ScopeJson, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.IntegrationsUse)]
    [HttpPost("integrations/consents/revoke")]
    public Task<IntegrationConsentDetails> RevokeConsent(RevokeIntegrationConsentRequest request, CancellationToken cancellationToken) =>
        integrations.RevokeConsentAsync(User.GetUserId(), request.ProviderKind, request.Purpose, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.IntegrationsUse)]
    [HttpPost("integrations/fhir/export")]
    public Task<FhirBundleExport> ExportFhir(FhirExportRequest request, CancellationToken cancellationToken) =>
        integrations.ExportFhirAsync(User.GetUserId(), request.AppointmentId, request.PrescriptionId, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.IntegrationsUse)]
    [HttpGet("integrations/abdm/readiness")]
    public Task<AbdmReadinessResult> AbdmReadiness(CancellationToken cancellationToken) =>
        integrations.GetAbdmReadinessAsync(cancellationToken);

    [HasPermission(PermissionCodes.IntegrationsUse)]
    [HttpGet("integrations/abha")]
    public Task<AbhaLinkDetails> Abha(CancellationToken cancellationToken) =>
        integrations.GetAbhaAsync(User.GetUserId(), cancellationToken);

    [HasPermission(PermissionCodes.IntegrationsUse)]
    [HttpPost("integrations/abha/sandbox-link")]
    public Task<AbhaLinkDetails> SimulateAbha(AbhaSandboxLinkRequest request, CancellationToken cancellationToken) =>
        integrations.SimulateAbhaSandboxAsync(User.GetUserId(), request.PreferredHandle, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.IntegrationsUse)]
    [HttpPost("integrations/abha/clear")]
    public Task<AbhaLinkDetails> ClearAbha(CancellationToken cancellationToken) =>
        integrations.ClearAbhaAsync(User.GetUserId(), Meta(), cancellationToken);

    [HasPermission(PermissionCodes.IntegrationsUse)]
    [HttpGet("integrations/labs/catalog")]
    public Task<IReadOnlyList<LabCatalogItem>> LabCatalog([FromQuery] string? providerCode, CancellationToken cancellationToken) =>
        integrations.ListLabCatalogAsync(providerCode, cancellationToken);

    [HasPermission(PermissionCodes.IntegrationsUse)]
    [HttpPost("integrations/labs/orders")]
    public Task<LabOrderDetails> CreateLabOrder(CreateLabOrderRequest request, CancellationToken cancellationToken) =>
        integrations.CreateLabOrderAsync(
            User.GetUserId(),
            request.ProviderCode,
            request.CatalogCode,
            request.AppointmentId,
            request.PrescriptionId,
            request.IdempotencyKey,
            Meta(),
            cancellationToken);

    [HasPermission(PermissionCodes.IntegrationsUse)]
    [HttpGet("integrations/labs/orders")]
    public Task<IReadOnlyList<LabOrderDetails>> LabOrders(CancellationToken cancellationToken) =>
        integrations.ListLabOrdersAsync(User.GetUserId(), cancellationToken);

    [HasPermission(PermissionCodes.IntegrationsUse)]
    [HttpGet("integrations/labs/reports")]
    public Task<IReadOnlyList<LabReportDetails>> LabReports(CancellationToken cancellationToken) =>
        integrations.ListLabReportsAsync(User.GetUserId(), cancellationToken);

    [HasPermission(PermissionCodes.IntegrationsUse)]
    [HttpGet("integrations/labs/reports/{reportId:guid}")]
    public Task<LabReportDetails> LabReport(Guid reportId, CancellationToken cancellationToken) =>
        integrations.GetLabReportAsync(User.GetUserId(), reportId, cancellationToken);

    [AllowAnonymous]
    [EnableRateLimiting("webhooks")]
    [HttpPost("integrations/labs/webhooks/{providerCode}")]
    public async Task<IActionResult> LabWebhook(string providerCode, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        var headers = Request.Headers.ToDictionary(item => item.Key, item => item.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        await integrations.HandleLabWebhookAsync(providerCode, body, headers, cancellationToken);
        return Ok();
    }

    [HasPermission(PermissionCodes.IntegrationsUse)]
    [HttpGet("integrations/pharmacies")]
    public Task<IReadOnlyList<PharmacyDirectoryItem>> Pharmacies([FromQuery] string? providerCode, [FromQuery] string? city, CancellationToken cancellationToken) =>
        integrations.ListPharmaciesAsync(providerCode, city, cancellationToken);

    [HasPermission(PermissionCodes.IntegrationsUse)]
    [HttpPost("integrations/pharmacies/shares")]
    public Task<PharmacyShareDetails> SharePrescription(SharePrescriptionRequest request, CancellationToken cancellationToken) =>
        integrations.SharePrescriptionAsync(
            User.GetUserId(),
            request.PrescriptionId,
            request.ProviderCode,
            request.PharmacyCode,
            request.IdempotencyKey,
            Meta(),
            cancellationToken);

    [HasPermission(PermissionCodes.IntegrationsUse)]
    [HttpGet("integrations/pharmacies/shares")]
    public Task<IReadOnlyList<PharmacyShareDetails>> PharmacyShares(CancellationToken cancellationToken) =>
        integrations.ListPharmacySharesAsync(User.GetUserId(), cancellationToken);

    [HasPermission(PermissionCodes.IntegrationsUse)]
    [HttpPost("integrations/pharmacies/shares/{shareId:guid}/withdraw")]
    public Task<PharmacyShareDetails> WithdrawShare(Guid shareId, CancellationToken cancellationToken) =>
        integrations.WithdrawPharmacyShareAsync(User.GetUserId(), shareId, Meta(), cancellationToken);

    [AllowAnonymous]
    [EnableRateLimiting("webhooks")]
    [HttpPost("integrations/pharmacies/webhooks/{providerCode}")]
    public async Task<IActionResult> PharmacyWebhook(string providerCode, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        var headers = Request.Headers.ToDictionary(item => item.Key, item => item.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        await integrations.HandlePharmacyWebhookAsync(providerCode, body, headers, cancellationToken);
        return Ok();
    }

    private RequestMetadata Meta() => new(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext));
}

public sealed record ConfigureIntegrationProviderRequest(
    string Mode,
    bool IsActive,
    string? BaseUrl,
    string? CredentialReference);

public sealed record IntegrationConsentRequest(string ProviderKind, string Purpose, string? ScopeJson);

public sealed record RevokeIntegrationConsentRequest(string ProviderKind, string Purpose);

public sealed record FhirExportRequest(Guid? AppointmentId, Guid? PrescriptionId);

public sealed record AbhaSandboxLinkRequest(string? PreferredHandle);

public sealed record CreateLabOrderRequest(
    string ProviderCode,
    string CatalogCode,
    Guid? AppointmentId,
    Guid? PrescriptionId,
    string IdempotencyKey);

public sealed record SharePrescriptionRequest(
    Guid PrescriptionId,
    string ProviderCode,
    string PharmacyCode,
    string IdempotencyKey);
