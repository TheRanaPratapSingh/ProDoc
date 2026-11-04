using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Application.Partners;
using ProDoc.Application.Providers;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/partners")]
public sealed class PartnerPortalController(IPartnerService partners) : ControllerBase
{
    [HttpGet("catalog")]
    [HasPermission(PermissionCodes.PartnersDevelop)]
    public PartnerCatalog Catalog() => partners.Catalog();

    [HttpGet("docs/guides")]
    [HasPermission(PermissionCodes.PartnersDevelop)]
    public IReadOnlyList<PartnerDocsGuide> Guides() => partners.DocsGuides();

    [HttpGet("me")]
    [HasPermission(PermissionCodes.PartnersDevelop)]
    public Task<PartnerOrganizationDetails?> Me(CancellationToken cancellationToken) =>
        partners.MyOrganizationAsync(User.GetUserId(), cancellationToken);

    [HttpPost("register")]
    [HasPermission(PermissionCodes.PartnersDevelop)]
    public Task<PartnerOrganizationDetails> Register(RegisterPartnerRequest request, CancellationToken cancellationToken) =>
        partners.RegisterAsync(User.GetUserId(), request.Name, request.ContactEmail, request.Website, Meta(), cancellationToken);

    [HttpPost("{partnerOrganizationId:guid}/apps")]
    [HasPermission(PermissionCodes.PartnersDevelop)]
    public Task<PartnerAppDetails> CreateApp(Guid partnerOrganizationId, CreatePartnerAppRequest request, CancellationToken cancellationToken) =>
        partners.CreateAppAsync(
            User.GetUserId(),
            partnerOrganizationId,
            request.Name,
            request.Scopes,
            request.Environment,
            Meta(),
            cancellationToken);

    [HttpGet("{partnerOrganizationId:guid}/apps")]
    [HasPermission(PermissionCodes.PartnersDevelop)]
    public Task<IReadOnlyList<PartnerAppDetails>> Apps(Guid partnerOrganizationId, CancellationToken cancellationToken) =>
        partners.ListAppsAsync(User.GetUserId(), partnerOrganizationId, cancellationToken);

    [HttpPost("apps/{appId:guid}/credentials")]
    [HasPermission(PermissionCodes.PartnersDevelop)]
    public Task<PartnerCredentialIssued> IssueCredential(Guid appId, CancellationToken cancellationToken) =>
        partners.IssueCredentialAsync(User.GetUserId(), appId, Meta(), cancellationToken);

    [HttpGet("apps/{appId:guid}/credentials")]
    [HasPermission(PermissionCodes.PartnersDevelop)]
    public Task<IReadOnlyList<PartnerCredentialDetails>> Credentials(Guid appId, CancellationToken cancellationToken) =>
        partners.ListCredentialsAsync(User.GetUserId(), appId, cancellationToken);

    [HttpPost("apps/{appId:guid}/credentials/{credentialId:guid}/revoke")]
    [HasPermission(PermissionCodes.PartnersDevelop)]
    public async Task<IActionResult> RevokeCredential(Guid appId, Guid credentialId, CancellationToken cancellationToken)
    {
        await partners.RevokeCredentialAsync(User.GetUserId(), appId, credentialId, Meta(), cancellationToken);
        return NoContent();
    }

    [HttpPost("apps/{appId:guid}/webhooks")]
    [HasPermission(PermissionCodes.PartnersDevelop)]
    public Task<PartnerWebhookEndpointCreated> CreateWebhook(Guid appId, CreatePartnerWebhookRequest request, CancellationToken cancellationToken) =>
        partners.CreateWebhookAsync(User.GetUserId(), appId, request.TargetUrl, request.EventTypes, Meta(), cancellationToken);

    [HttpGet("apps/{appId:guid}/webhooks")]
    [HasPermission(PermissionCodes.PartnersDevelop)]
    public Task<IReadOnlyList<PartnerWebhookEndpointDetails>> Webhooks(Guid appId, CancellationToken cancellationToken) =>
        partners.ListWebhooksAsync(User.GetUserId(), appId, cancellationToken);

    [HttpPost("webhooks/{endpointId:guid}/rotate-secret")]
    [HasPermission(PermissionCodes.PartnersDevelop)]
    public async Task<ActionResult<object>> RotateWebhook(Guid endpointId, CancellationToken cancellationToken)
    {
        var secret = await partners.RotateWebhookSecretAsync(User.GetUserId(), endpointId, Meta(), cancellationToken);
        return Ok(new { signingSecret = secret });
    }

    [HttpPost("webhooks/{endpointId:guid}/active")]
    [HasPermission(PermissionCodes.PartnersDevelop)]
    public async Task<IActionResult> SetWebhookActive(Guid endpointId, PartnerSetWebhookActiveRequest request, CancellationToken cancellationToken)
    {
        await partners.SetWebhookActiveAsync(User.GetUserId(), endpointId, request.Active, Meta(), cancellationToken);
        return NoContent();
    }

    [HttpGet("apps/{appId:guid}/deliveries")]
    [HasPermission(PermissionCodes.PartnersDevelop)]
    public Task<IReadOnlyList<PartnerWebhookDeliveryDetails>> Deliveries(Guid appId, CancellationToken cancellationToken) =>
        partners.ListDeliveriesAsync(User.GetUserId(), appId, cancellationToken);

    [HttpGet("apps/{appId:guid}/usage")]
    [HasPermission(PermissionCodes.PartnersDevelop)]
    public Task<PartnerUsageSummary> Usage(Guid appId, CancellationToken cancellationToken) =>
        partners.UsageAsync(User.GetUserId(), appId, cancellationToken);

    private RequestMetadata Meta() => new(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext));
}

public sealed record RegisterPartnerRequest(string Name, string ContactEmail, string? Website);

public sealed record CreatePartnerAppRequest(string Name, IReadOnlyList<string> Scopes, string Environment);

public sealed record CreatePartnerWebhookRequest(string TargetUrl, IReadOnlyList<string> EventTypes);

public sealed record PartnerSetWebhookActiveRequest(bool Active);
