using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Application.Partners;
using ProDoc.Application.Providers;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/admin/partners")]
public sealed class PartnerAdminController(IPartnerService partners) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.PartnersManage)]
    public Task<IReadOnlyList<PartnerOrganizationDetails>> List([FromQuery] string? status, CancellationToken cancellationToken) =>
        partners.AdminListAsync(status, cancellationToken);

    [HttpGet("stats")]
    [HasPermission(PermissionCodes.PartnersManage)]
    public Task<PartnerPlatformStats> Stats(CancellationToken cancellationToken) =>
        partners.AdminStatsAsync(cancellationToken);

    [HttpGet("dead-letters")]
    [HasPermission(PermissionCodes.PartnersManage)]
    public Task<IReadOnlyList<PartnerWebhookDeliveryDetails>> DeadLetters(CancellationToken cancellationToken) =>
        partners.AdminDeadLettersAsync(cancellationToken);

    [HttpPost("{partnerOrganizationId:guid}/decision")]
    [HasPermission(PermissionCodes.PartnersManage)]
    public Task<PartnerOrganizationDetails> Decide(
        Guid partnerOrganizationId,
        PartnerDecisionRequest request,
        CancellationToken cancellationToken) =>
        partners.AdminDecideAsync(User.GetUserId(), partnerOrganizationId, request.Decision, request.Note, Meta(), cancellationToken);

    [HttpPut("apps/{appId:guid}")]
    [HasPermission(PermissionCodes.PartnersManage)]
    public Task<PartnerAppDetails> ConfigureApp(Guid appId, ConfigurePartnerAppRequest request, CancellationToken cancellationToken) =>
        partners.AdminConfigureAppAsync(
            User.GetUserId(),
            appId,
            request.Scopes,
            request.RateLimitPerMinute,
            request.DailyQuota,
            request.IsActive,
            Meta(),
            cancellationToken);

    private RequestMetadata Meta() => new(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext));
}

public sealed record PartnerDecisionRequest(string Decision, string? Note);

public sealed record ConfigurePartnerAppRequest(
    IReadOnlyList<string> Scopes,
    int RateLimitPerMinute,
    int DailyQuota,
    bool? IsActive);
