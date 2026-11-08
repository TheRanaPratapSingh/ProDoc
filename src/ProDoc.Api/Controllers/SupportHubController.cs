using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Application.Admin;
using ProDoc.Application.Providers;
using ProDoc.Application.Support;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class SupportHubController(SupportService support) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("help/categories")]
    public Task<IReadOnlyList<HelpCategoryDetails>> Categories(CancellationToken cancellationToken) =>
        support.ListCategoriesAsync(activeOnly: true, cancellationToken);

    [AllowAnonymous]
    [HttpGet("help/articles")]
    public Task<IReadOnlyList<HelpArticleSummary>> SearchHelp(
        [FromQuery] string? q,
        [FromQuery] string? category,
        CancellationToken cancellationToken) =>
        support.SearchHelpAsync(q, category, publishedOnly: true, cancellationToken);

    [AllowAnonymous]
    [HttpGet("help/articles/{slugOrId}")]
    public Task<HelpArticleDetails> GetHelp(string slugOrId, CancellationToken cancellationToken) =>
        support.GetHelpArticleAsync(slugOrId, allowUnpublished: false, cancellationToken);

    [HttpPost("help/articles/{slugOrId}/feedback")]
    [AllowAnonymous]
    public Task<HelpArticleFeedbackResult> ArticleFeedback(
        string slugOrId,
        HelpArticleFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        Guid? userId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            try
            {
                userId = User.GetUserId();
            }
            catch
            {
                userId = null;
            }
        }

        return support.SubmitArticleFeedbackAsync(userId, slugOrId, request.WasHelpful, cancellationToken);
    }

    [Authorize]
    [HasPermission(PermissionCodes.AdminSupport)]
    [HttpGet("admin/help/articles")]
    public Task<IReadOnlyList<HelpArticleSummary>> AdminSearchHelp(
        [FromQuery] string? q,
        [FromQuery] string? category,
        CancellationToken cancellationToken) =>
        support.SearchHelpAsync(q, category, publishedOnly: false, cancellationToken);

    [Authorize]
    [HasPermission(PermissionCodes.AdminSupport)]
    [HttpPut("admin/help/articles")]
    public Task<HelpArticleDetails> UpsertHelp(UpsertHelpArticleRequest request, CancellationToken cancellationToken) =>
        support.UpsertHelpArticleAsync(
            User.GetUserId(),
            request.Id,
            request.CategoryId,
            request.Slug,
            request.TitleEn,
            request.TitleHi,
            request.BodyEn,
            request.BodyHi,
            request.Tags,
            request.IsPublished,
            Meta(),
            cancellationToken);

    [Authorize]
    [HasPermission(PermissionCodes.AdminSupport)]
    [HttpGet("admin/support/workspace")]
    public Task<AgentTicketWorkspace> Workspace(
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] string? filter,
        CancellationToken cancellationToken) =>
        support.AgentWorkspaceAsync(User.GetUserId(), status, priority, filter, cancellationToken);

    [Authorize]
    [HasPermission(PermissionCodes.AdminSupport)]
    [HttpPut("admin/support/tickets/{ticketId:guid}/manage")]
    public Task<SupportTicketDetails> ManageTicket(Guid ticketId, ManageSupportTicketRequest request, CancellationToken cancellationToken) =>
        support.UpdateTicketAsync(
            User.GetUserId(),
            ticketId,
            request.Status,
            request.Priority,
            request.AssignedToUserId,
            request.Note,
            request.InternalNote,
            request.ResolutionDetails,
            request.Escalate,
            Meta(),
            cancellationToken);

    [Authorize]
    [HttpPost("support/tickets/{ticketId:guid}/attachments")]
    [RequestSizeLimit(5_000_000)]
    public async Task<SupportAttachmentDetails> UploadAttachment(Guid ticketId, IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var isAgent = User.Claims.Any(claim => claim.Type == "perm" && claim.Value == PermissionCodes.AdminSupport);
        return await support.AddAttachmentAsync(
            User.GetUserId(),
            ticketId,
            file.FileName,
            file.ContentType,
            stream,
            isAgent,
            Meta(),
            cancellationToken);
    }

    [Authorize]
    [HttpGet("support/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DownloadAttachment(Guid attachmentId, CancellationToken cancellationToken)
    {
        var isAgent = User.Claims.Any(claim => claim.Type == "perm" && claim.Value == PermissionCodes.AdminSupport);
        var (stream, fileName, contentType) = await support.DownloadAttachmentAsync(User.GetUserId(), attachmentId, isAgent, cancellationToken);
        return File(stream, contentType, fileName);
    }

    [Authorize]
    [HasPermission(PermissionCodes.AdminSupport)]
    [HttpGet("admin/support/disputes")]
    public Task<IReadOnlyList<DisputeCaseSummary>> Disputes([FromQuery] string? status, CancellationToken cancellationToken) =>
        support.ListDisputesAsync(status, cancellationToken);

    [Authorize]
    [HasPermission(PermissionCodes.AdminSupport)]
    [HttpGet("admin/support/disputes/{disputeId:guid}")]
    public Task<DisputeCaseDetails> Dispute(Guid disputeId, CancellationToken cancellationToken) =>
        support.GetDisputeAsync(disputeId, cancellationToken);

    [Authorize]
    [HasPermission(PermissionCodes.AdminSupport)]
    [HttpPost("admin/support/tickets/{ticketId:guid}/disputes")]
    public Task<DisputeCaseDetails> OpenDispute(Guid ticketId, OpenDisputeRequest request, CancellationToken cancellationToken) =>
        support.OpenDisputeAsync(User.GetUserId(), ticketId, request.Kind, request.Summary, Meta(), cancellationToken);

    [Authorize]
    [HasPermission(PermissionCodes.AdminSupport)]
    [HttpPost("admin/support/disputes/{disputeId:guid}/actions")]
    public Task<DisputeCaseDetails> DisputeAction(Guid disputeId, DisputeActionRequest request, CancellationToken cancellationToken) =>
        support.UpdateDisputeAsync(
            User.GetUserId(),
            disputeId,
            request.Action,
            request.EvidenceRequest,
            request.Decision,
            request.DecisionNotes,
            request.RefundAmount,
            Meta(),
            cancellationToken);

    [Authorize]
    [HasPermission(PermissionCodes.AdminSupport)]
    [HttpGet("admin/support/analytics")]
    public Task<SupportAnalytics> Analytics(CancellationToken cancellationToken) =>
        support.AnalyticsAsync(cancellationToken);

    private RequestMetadata Meta() => new(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext));
}

public sealed record UpsertHelpArticleRequest(
    Guid? Id,
    Guid CategoryId,
    string Slug,
    string TitleEn,
    string TitleHi,
    string BodyEn,
    string BodyHi,
    string? Tags,
    bool IsPublished);

public sealed record ManageSupportTicketRequest(
    string? Status,
    string? Priority,
    Guid? AssignedToUserId,
    string? Note,
    bool InternalNote = true,
    string? ResolutionDetails = null,
    bool Escalate = false);

public sealed record OpenDisputeRequest(string Kind, string Summary);

public sealed record DisputeActionRequest(
    string Action,
    string? EvidenceRequest,
    string? Decision,
    string? DecisionNotes,
    decimal? RefundAmount);

public sealed record HelpArticleFeedbackRequest(bool WasHelpful);
