using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Application.Admin;
using ProDoc.Application.Providers;
using ProDoc.Domain.Admin;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/admin")]
public sealed class AdminPlatformController(AdminPlatformService admin) : ControllerBase
{
    [HasPermission(PermissionCodes.AdminAccess)]
    [HttpGet("dashboard")]
    public Task<AdminDashboardMetrics> Dashboard(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? state,
        [FromQuery] string? city,
        [FromQuery] Guid? clinicId,
        [FromQuery] string? appointmentStatus,
        CancellationToken cancellationToken) =>
        admin.DashboardAsync(from, to, state, city, clinicId, appointmentStatus, cancellationToken);

    [HasPermission(PermissionCodes.AdminUsers)]
    [HttpGet("users")]
    public Task<IReadOnlyList<AdminUserSummary>> Users(
        [FromQuery] string? q,
        [FromQuery] string? role,
        [FromQuery] bool? suspended,
        CancellationToken cancellationToken) =>
        admin.SearchUsersAsync(q, role, suspended, cancellationToken);

    [HasPermission(PermissionCodes.AdminUsers)]
    [HttpPost("users/{userId:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid userId, [FromBody] SuspendUserRequest request, CancellationToken cancellationToken)
    {
        await admin.SuspendUserAsync(User.GetUserId(), userId, request.Reason, Meta(), cancellationToken);
        return NoContent();
    }

    [HasPermission(PermissionCodes.AdminUsers)]
    [HttpPost("users/{userId:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid userId, CancellationToken cancellationToken)
    {
        await admin.ReactivateUserAsync(User.GetUserId(), userId, Meta(), cancellationToken);
        return NoContent();
    }

    [HasPermission(PermissionCodes.AdminUsers)]
    [HttpPost("users/{userId:guid}/roles/{roleName}")]
    public async Task<IActionResult> GrantRole(Guid userId, string roleName, CancellationToken cancellationToken)
    {
        await admin.GrantRoleAsync(User.GetUserId(), userId, roleName, Meta(), cancellationToken);
        return NoContent();
    }

    [HasPermission(PermissionCodes.AdminUsers)]
    [HttpDelete("users/{userId:guid}/roles/{roleName}")]
    public async Task<IActionResult> RevokeRole(Guid userId, string roleName, CancellationToken cancellationToken)
    {
        await admin.RevokeRoleAsync(User.GetUserId(), userId, roleName, Meta(), cancellationToken);
        return NoContent();
    }

    [HasPermission(PermissionCodes.AuditRead)]
    [HttpGet("audit")]
    public Task<IReadOnlyList<AuditEntryDetails>> Audit(
        [FromQuery] string? action,
        [FromQuery] Guid? actorUserId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken) =>
        admin.ListAuditAsync(action, actorUserId, from, to, cancellationToken);

    [HasPermission(PermissionCodes.AdminAccess)]
    [HttpGet("providers")]
    public Task<IReadOnlyList<ProviderDirectoryItem>> Providers(
        [FromQuery] string kind = "doctor",
        [FromQuery] string? status = null,
        [FromQuery] string? city = null,
        [FromQuery] string? state = null,
        CancellationToken cancellationToken = default) =>
        admin.ListProvidersAsync(kind, status, city, state, cancellationToken);

    [HasPermission(PermissionCodes.CommissionsManage)]
    [HttpGet("finance/report")]
    public Task<IReadOnlyList<FinanceReportRow>> FinanceReport(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? state,
        [FromQuery] string? city,
        [FromQuery] Guid? clinicId,
        CancellationToken cancellationToken) =>
        admin.FinanceReportAsync(from, to, state, city, clinicId, cancellationToken);

    [HasPermission(PermissionCodes.CommissionsManage)]
    [HttpGet("finance/export.csv")]
    public async Task<IActionResult> FinanceExport(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? state,
        [FromQuery] string? city,
        [FromQuery] Guid? clinicId,
        CancellationToken cancellationToken)
    {
        var bytes = await admin.ExportFinanceCsvAsync(from, to, state, city, clinicId, cancellationToken);
        return File(bytes, "text/csv", "prodoc-finance-report.csv");
    }

    [HasPermission(PermissionCodes.AdminSupport)]
    [HttpGet("support/tickets")]
    public Task<IReadOnlyList<SupportTicketSummary>> AllTickets([FromQuery] string? status, CancellationToken cancellationToken)
    {
        SupportTicketStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SupportTicketStatus>(status, true, out var value))
        {
            parsed = value;
        }

        return admin.ListAllTicketsAsync(parsed, cancellationToken);
    }

    [HasPermission(PermissionCodes.AdminSupport)]
    [HttpGet("support/tickets/{ticketId:guid}")]
    public Task<SupportTicketDetails> GetTicket(Guid ticketId, CancellationToken cancellationToken) =>
        admin.GetTicketAsync(User.GetUserId(), ticketId, isAdmin: true, cancellationToken);

    [HasPermission(PermissionCodes.AdminSupport)]
    [HttpPut("support/tickets/{ticketId:guid}")]
    public Task<SupportTicketDetails> UpdateTicket(Guid ticketId, UpdateSupportTicketRequest request, CancellationToken cancellationToken) =>
        admin.UpdateTicketAsync(
            User.GetUserId(),
            ticketId,
            request.Status,
            request.Priority,
            request.AssignedToUserId,
            request.Note,
            request.InternalNote,
            Meta(),
            cancellationToken);

    [HasPermission(PermissionCodes.AdminConfig)]
    [HttpGet("settings")]
    public Task<IReadOnlyList<PlatformSettingDetails>> Settings(CancellationToken cancellationToken) =>
        admin.ListSettingsAsync(cancellationToken);

    [HasPermission(PermissionCodes.AdminConfig)]
    [HttpPut("settings/{key}")]
    public Task<PlatformSettingDetails> PutSetting(string key, UpsertSettingRequest request, CancellationToken cancellationToken) =>
        admin.UpsertSettingAsync(User.GetUserId(), key, request.Value, request.Description, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.AdminConfig)]
    [HttpGet("feature-flags")]
    public Task<IReadOnlyList<FeatureFlagDetails>> Flags(CancellationToken cancellationToken) =>
        admin.ListFlagsAsync(cancellationToken);

    [HasPermission(PermissionCodes.AdminConfig)]
    [HttpPut("feature-flags/{key}")]
    public Task<FeatureFlagDetails> PutFlag(string key, UpsertFlagRequest request, CancellationToken cancellationToken) =>
        admin.UpsertFlagAsync(User.GetUserId(), key, request.Enabled, request.Description, Meta(), cancellationToken);

    private RequestMetadata Meta() => new(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext));
}

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class SupportController(AdminPlatformService admin) : ControllerBase
{
    [HttpPost("support/tickets")]
    public Task<SupportTicketDetails> Create(CreateSupportTicketRequest request, CancellationToken cancellationToken) =>
        admin.CreateTicketAsync(
            User.GetUserId(),
            request.Category,
            request.Priority,
            request.Subject,
            request.Description,
            request.AppointmentId,
            request.PaymentId,
            request.ClinicId,
            Meta(),
            cancellationToken);

    [HttpGet("support/tickets/mine")]
    public Task<IReadOnlyList<SupportTicketSummary>> Mine(CancellationToken cancellationToken) =>
        admin.ListMyTicketsAsync(User.GetUserId(), cancellationToken);

    [HttpGet("support/tickets/{ticketId:guid}")]
    public Task<SupportTicketDetails> Get(Guid ticketId, CancellationToken cancellationToken) =>
        admin.GetTicketAsync(User.GetUserId(), ticketId, isAdmin: false, cancellationToken);

    [HttpPost("support/tickets/{ticketId:guid}/notes")]
    public Task<SupportTicketDetails> Note(Guid ticketId, AddTicketNoteRequest request, CancellationToken cancellationToken) =>
        admin.AddUserNoteAsync(User.GetUserId(), ticketId, request.Body, Meta(), cancellationToken);

    private RequestMetadata Meta() => new(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext));
}

public sealed record SuspendUserRequest(string? Reason);

public sealed record CreateSupportTicketRequest(
    string Category,
    string Priority,
    string Subject,
    string Description,
    Guid? AppointmentId,
    Guid? PaymentId,
    Guid? ClinicId);

public sealed record UpdateSupportTicketRequest(
    string? Status,
    string? Priority,
    Guid? AssignedToUserId,
    string? Note,
    bool InternalNote = true);

public sealed record AddTicketNoteRequest(string Body);

public sealed record UpsertSettingRequest(string Value, string? Description);

public sealed record UpsertFlagRequest(bool Enabled, string? Description);
