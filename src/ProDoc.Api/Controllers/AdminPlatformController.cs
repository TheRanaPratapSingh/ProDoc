using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Api.Contracts;
using ProDoc.Application.Admin;
using ProDoc.Application.Providers;
using ProDoc.Application.Support;
using ProDoc.Domain.Admin;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/admin")]
public sealed class AdminPlatformController(
    AdminPlatformService admin,
    StaffInvitationService invitations,
    AdminClinicService clinics) : ControllerBase
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
        [FromQuery] Guid? clinicId,
        CancellationToken cancellationToken) =>
        admin.SearchUsersAsync(q, role, suspended, cancellationToken, clinicId);

    [HasPermission(PermissionCodes.AdminUsers)]
    [HttpGet("users/{userId:guid}")]
    public Task<AdminUserDetails> UserDetails(Guid userId, CancellationToken cancellationToken) =>
        admin.GetUserAsync(userId, cancellationToken);

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
    [HttpDelete("users/{userId:guid}")]
    public async Task<IActionResult> DeleteUser(Guid userId, CancellationToken cancellationToken)
    {
        await admin.DeleteUserAsync(User.GetUserId(), userId, Meta(), cancellationToken);
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

    [HasPermission(PermissionCodes.AdminUsers)]
    [HttpGet("invitations")]
    public Task<IReadOnlyList<StaffInvitationDetails>> Invitations(
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        invitations.ListAsync(status, cancellationToken);

    [HasPermission(PermissionCodes.AdminUsers)]
    [HttpPost("invitations")]
    public Task<StaffInvitationDetails> Invite([FromBody] InviteStaffRequest request, CancellationToken cancellationToken) =>
        invitations.InviteAsync(
            new InviteStaffCommand(
                User.GetUserId(),
                request.FullName,
                request.PhoneNumber,
                request.Email,
                request.RoleName,
                request.ClinicId,
                request.BranchId,
                ClientContext.IpAddress(HttpContext),
                ClientContext.UserAgent(HttpContext),
                request.SpecializationId,
                request.YearsOfExperience,
                request.Qualifications,
                request.RegistrationNumber,
                request.Designation,
                request.ClinicIds,
                request.DoctorIds),
            cancellationToken);

    [HasPermission(PermissionCodes.AdminUsers)]
    [HttpGet("clinics")]
    public Task<IReadOnlyList<AdminClinicSummary>> Clinics([FromQuery] string? q, CancellationToken cancellationToken) =>
        clinics.ListAsync(q, cancellationToken);

    [HasPermission(PermissionCodes.AdminUsers)]
    [HttpGet("clinics/{clinicId:guid}")]
    public Task<AdminClinicDetails> Clinic(Guid clinicId, CancellationToken cancellationToken) =>
        clinics.GetAsync(clinicId, cancellationToken);

    [HasPermission(PermissionCodes.AdminUsers)]
    [HttpPost("clinics")]
    public Task<AdminClinicDetails> CreateClinic([FromBody] CreateAdminClinicRequest request, CancellationToken cancellationToken) =>
        clinics.CreateAsync(
            new CreateAdminClinicCommand(
                User.GetUserId(),
                request.Name,
                request.Type,
                request.PhoneNumber,
                request.Email,
                request.Website,
                request.Description,
                request.RegistrationNumber,
                request.BranchName,
                request.AddressLine,
                request.AddressLine2,
                request.Landmark,
                request.Locality,
                request.City,
                request.District,
                request.State,
                request.PostalCode,
                request.Country,
                request.Latitude,
                request.Longitude,
                request.GooglePlaceId,
                request.BranchPhoneNumber,
                ClientContext.IpAddress(HttpContext),
                ClientContext.UserAgent(HttpContext)),
            cancellationToken);

    [HasPermission(PermissionCodes.AdminUsers)]
    [HttpPut("clinics/{clinicId:guid}")]
    public Task<AdminClinicDetails> UpdateClinic(Guid clinicId, [FromBody] UpdateAdminClinicRequest request, CancellationToken cancellationToken) =>
        clinics.UpdateAsync(
            new UpdateAdminClinicCommand(
                User.GetUserId(),
                clinicId,
                request.Name,
                request.Type,
                request.PhoneNumber,
                request.Email,
                request.Website,
                request.Description,
                request.RegistrationNumber,
                ClientContext.IpAddress(HttpContext),
                ClientContext.UserAgent(HttpContext)),
            cancellationToken);

    [HasPermission(PermissionCodes.AdminUsers)]
    [HttpPut("clinics/{clinicId:guid}/branches/{branchId:guid}")]
    public Task<AdminClinicDetails> UpdateClinicBranch(
        Guid clinicId,
        Guid branchId,
        [FromBody] UpdateAdminClinicBranchRequest request,
        CancellationToken cancellationToken) =>
        clinics.UpdateBranchAsync(
            new UpdateAdminClinicBranchCommand(
                User.GetUserId(),
                clinicId,
                branchId,
                request.Name,
                request.AddressLine,
                request.AddressLine2,
                request.Landmark,
                request.Locality,
                request.City,
                request.District,
                request.State,
                request.PostalCode,
                request.Country,
                request.Latitude,
                request.Longitude,
                request.GooglePlaceId,
                request.PhoneNumber,
                request.IsPrimary,
                ClientContext.IpAddress(HttpContext),
                ClientContext.UserAgent(HttpContext)),
            cancellationToken);

    [HasPermission(PermissionCodes.AdminUsers)]
    [HttpPut("doctors/{doctorId:guid}/affiliations/{affiliationId:guid}/practice-location")]
    public async Task<IActionResult> UpdateDoctorPracticeLocation(
        Guid doctorId,
        Guid affiliationId,
        [FromBody] PracticeLocationRequest request,
        CancellationToken cancellationToken)
    {
        await clinics.UpdatePracticeLocationAsync(
            new UpdateDoctorPracticeLocationCommand(
                User.GetUserId(),
                doctorId,
                affiliationId,
                request.ClearOverride,
                request.AddressLine,
                request.AddressLine2,
                request.Landmark,
                request.Locality,
                request.City,
                request.District,
                request.State,
                request.PostalCode,
                request.Country,
                request.Latitude,
                request.Longitude,
                request.GooglePlaceId,
                ClientContext.IpAddress(HttpContext),
                ClientContext.UserAgent(HttpContext)),
            cancellationToken);
        return NoContent();
    }

    [HasPermission(PermissionCodes.AdminUsers)]
    [HttpPost("clinics/{clinicId:guid}/doctors/{doctorId:guid}")]
    public async Task<IActionResult> AssignClinicDoctor(Guid clinicId, Guid doctorId, CancellationToken cancellationToken)
    {
        await clinics.AssignDoctorAsync(
            User.GetUserId(),
            clinicId,
            doctorId,
            null,
            ClientContext.IpAddress(HttpContext),
            ClientContext.UserAgent(HttpContext),
            cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Approves an admin-created doctor for patient search and ensures a published clinic schedule.
    /// Use this to repair invite-activated doctors that remained Draft without a discoverable schedule.
    /// </summary>
    [HasPermission(PermissionCodes.AdminUsers)]
    [HttpPost("doctors/{doctorId:guid}/activate-discovery")]
    public async Task<IActionResult> ActivateDoctorDiscovery(Guid doctorId, CancellationToken cancellationToken)
    {
        await clinics.ActivateDoctorDiscoveryAsync(
            User.GetUserId(),
            doctorId,
            ClientContext.IpAddress(HttpContext),
            ClientContext.UserAgent(HttpContext),
            cancellationToken);
        return NoContent();
    }

    [HasPermission(PermissionCodes.AdminUsers)]
    [HttpDelete("clinics/{clinicId:guid}/doctors/{doctorId:guid}")]
    public async Task<IActionResult> RemoveClinicDoctor(Guid clinicId, Guid doctorId, CancellationToken cancellationToken)
    {
        await clinics.RemoveDoctorAsync(User.GetUserId(), clinicId, doctorId, cancellationToken);
        return NoContent();
    }

    [HasPermission(PermissionCodes.AdminUsers)]
    [HttpPost("clinics/{clinicId:guid}/staff")]
    public async Task<IActionResult> AssignClinicStaff(Guid clinicId, [FromBody] AssignAdminClinicStaffRequest request, CancellationToken cancellationToken)
    {
        await clinics.AssignStaffAsync(
            User.GetUserId(),
            clinicId,
            request.UserId,
            request.RoleName,
            request.BranchId,
            request.DoctorIds,
            ClientContext.IpAddress(HttpContext),
            ClientContext.UserAgent(HttpContext),
            cancellationToken);
        return NoContent();
    }

    [HasPermission(PermissionCodes.AdminUsers)]
    [HttpDelete("clinics/{clinicId:guid}/staff/{userId:guid}")]
    public async Task<IActionResult> RemoveClinicStaff(Guid clinicId, Guid userId, CancellationToken cancellationToken)
    {
        await clinics.RemoveStaffAsync(clinicId, userId, cancellationToken);
        return NoContent();
    }

    [HasPermission(PermissionCodes.AdminUsers)]
    [HttpPost("invitations/{invitationId:guid}/resend")]
    public Task<StaffInvitationDetails> ResendInvite(Guid invitationId, CancellationToken cancellationToken) =>
        invitations.ResendAsync(
            User.GetUserId(),
            invitationId,
            ClientContext.IpAddress(HttpContext),
            ClientContext.UserAgent(HttpContext),
            cancellationToken);

    [HasPermission(PermissionCodes.AdminUsers)]
    [HttpPost("invitations/{invitationId:guid}/verify")]
    public Task<StaffInviteActivationResult> VerifyInvite(
        Guid invitationId,
        [FromBody] VerifyStaffInvitationRequest request,
        CancellationToken cancellationToken) =>
        invitations.VerifyInviteAsync(
            User.GetUserId(),
            invitationId,
            request.Code,
            ClientContext.IpAddress(HttpContext),
            ClientContext.UserAgent(HttpContext),
            cancellationToken);

    [HasPermission(PermissionCodes.AdminUsers)]
    [HttpPost("invitations/{invitationId:guid}/cancel")]
    public async Task<IActionResult> CancelInvite(Guid invitationId, CancellationToken cancellationToken)
    {
        await invitations.CancelAsync(
            User.GetUserId(),
            invitationId,
            ClientContext.IpAddress(HttpContext),
            ClientContext.UserAgent(HttpContext),
            cancellationToken);
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
    [HttpGet("support/catalog")]
    public SupportCatalogDetails Catalog([FromServices] SupportService support) =>
        support.GetCatalog(User.FindFirst("role")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value);

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
            cancellationToken,
            request.IssueType,
            request.CreatedByRole ?? User.FindFirst("role")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value);

    [HttpGet("support/tickets/mine")]
    public Task<IReadOnlyList<SupportTicketSummary>> Mine(CancellationToken cancellationToken) =>
        admin.ListMyTicketsAsync(User.GetUserId(), cancellationToken);

    [HttpGet("support/tickets/{ticketId:guid}")]
    public Task<SupportTicketDetails> Get(Guid ticketId, CancellationToken cancellationToken) =>
        admin.GetTicketAsync(User.GetUserId(), ticketId, isAdmin: false, cancellationToken);

    [HttpPost("support/tickets/{ticketId:guid}/notes")]
    public Task<SupportTicketDetails> Note(Guid ticketId, AddTicketNoteRequest request, CancellationToken cancellationToken) =>
        admin.AddUserNoteAsync(User.GetUserId(), ticketId, request.Body, Meta(), cancellationToken);

    [HttpPost("support/tickets/{ticketId:guid}/close")]
    public Task<SupportTicketDetails> Close(
        Guid ticketId,
        [FromServices] SupportService support,
        CancellationToken cancellationToken) =>
        support.CloseTicketAsync(User.GetUserId(), ticketId, Meta(), cancellationToken);

    [HttpPost("support/tickets/{ticketId:guid}/reopen")]
    public Task<SupportTicketDetails> Reopen(
        Guid ticketId,
        AddTicketNoteRequest? request,
        [FromServices] SupportService support,
        CancellationToken cancellationToken) =>
        support.ReopenTicketAsync(User.GetUserId(), ticketId, request?.Body, Meta(), cancellationToken);

    private RequestMetadata Meta() => new(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext));
}

public sealed record SuspendUserRequest(string? Reason);

public sealed record InviteStaffRequest(
    string FullName,
    string PhoneNumber,
    string? Email,
    string RoleName,
    Guid? ClinicId,
    Guid? BranchId,
    Guid? SpecializationId = null,
    int? YearsOfExperience = null,
    string? Qualifications = null,
    string? RegistrationNumber = null,
    string? Designation = null,
    IReadOnlyList<Guid>? ClinicIds = null,
    IReadOnlyList<Guid>? DoctorIds = null);

public sealed record VerifyStaffInvitationRequest(string Code);

public sealed record CreateAdminClinicRequest(
    string Name,
    string Type,
    string PhoneNumber,
    string? Email,
    string? Website,
    string? Description,
    string? RegistrationNumber,
    string? BranchName,
    string AddressLine,
    string? AddressLine2,
    string City,
    string State,
    string PostalCode,
    string? BranchPhoneNumber,
    string? Landmark = null,
    string? Locality = null,
    string? District = null,
    string? Country = null,
    decimal? Latitude = null,
    decimal? Longitude = null,
    string? GooglePlaceId = null);

public sealed record UpdateAdminClinicBranchRequest(
    string Name,
    string AddressLine,
    string? AddressLine2,
    string? Landmark,
    string? Locality,
    string City,
    string? District,
    string State,
    string PostalCode,
    string? Country,
    decimal? Latitude,
    decimal? Longitude,
    string? GooglePlaceId,
    string? PhoneNumber,
    bool IsPrimary);

public sealed record UpdateAdminClinicRequest(
    string Name,
    string Type,
    string PhoneNumber,
    string? Email,
    string? Website,
    string? Description,
    string? RegistrationNumber);

public sealed record AssignAdminClinicStaffRequest(
    Guid UserId,
    string RoleName,
    Guid? BranchId,
    IReadOnlyList<Guid>? DoctorIds);

public sealed record CreateSupportTicketRequest(
    string Category,
    string Priority,
    string Subject,
    string Description,
    Guid? AppointmentId,
    Guid? PaymentId,
    Guid? ClinicId,
    string? IssueType = null,
    string? CreatedByRole = null);

public sealed record UpdateSupportTicketRequest(
    string? Status,
    string? Priority,
    Guid? AssignedToUserId,
    string? Note,
    bool InternalNote = true);

public sealed record AddTicketNoteRequest(string Body);

public sealed record UpsertSettingRequest(string Value, string? Description);

public sealed record UpsertFlagRequest(bool Enabled, string? Description);
