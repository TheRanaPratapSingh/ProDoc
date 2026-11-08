using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Api.Contracts;
using ProDoc.Api.Errors;
using ProDoc.Application.Appointments;
using ProDoc.Application.Providers;
using ProDoc.Domain.Common;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/appointments")]
public sealed class AppointmentsController(BookingService booking, AppointmentShareService shares) : ControllerBase
{
    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpPost("holds")]
    public async Task<ActionResult<AppointmentDetails>> Hold(HoldAppointmentRequest request, CancellationToken cancellationToken)
    {
        if (!TryParseTime(request.Start, out var start, out var error))
        {
            return DomainProblemResults.FromAppError(error!);
        }

        var result = await booking.HoldAsync(
            User.GetUserId(),
            request.ScheduleId,
            request.Date,
            start,
            Metadata(),
            cancellationToken);
        return result.ToActionResult(this);
    }

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpPost("{appointmentId:guid}/confirm")]
    public async Task<ActionResult<AppointmentDetails>> Confirm(
        Guid appointmentId,
        ConfirmAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await booking.ConfirmAsync(
            User.GetUserId(),
            appointmentId,
            request.FamilyMemberId,
            request.Reason,
            request.Notes,
            Metadata(),
            cancellationToken,
            request.FollowUpId);
        return result.ToActionResult(this);
    }

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpPost("{appointmentId:guid}/release")]
    public async Task<IActionResult> Release(Guid appointmentId, CancellationToken cancellationToken)
    {
        var result = await booking.ReleaseAsync(User.GetUserId(), appointmentId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpGet]
    public Task<IReadOnlyList<AppointmentDetails>> Mine(CancellationToken cancellationToken) =>
        booking.ListMineAsync(User.GetUserId(), cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpGet("{appointmentId:guid}")]
    public async Task<ActionResult<AppointmentDetails>> Get(Guid appointmentId, CancellationToken cancellationToken)
    {
        var result = await booking.GetAsync(User.GetUserId(), appointmentId, practice: false, cancellationToken);
        return result.ToActionResult(this);
    }

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpPost("{appointmentId:guid}/cancellation")]
    public async Task<ActionResult<AppointmentDetails>> Cancel(
        Guid appointmentId,
        AppointmentNoteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await booking.CancelAsync(
            User.GetUserId(),
            appointmentId,
            request.Reason,
            practice: false,
            Metadata(),
            cancellationToken);
        return result.ToActionResult(this);
    }

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpPost("{appointmentId:guid}/reschedule")]
    public async Task<ActionResult<AppointmentDetails>> Reschedule(
        Guid appointmentId,
        RescheduleAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseTime(request.Start, out var start, out var error))
        {
            return DomainProblemResults.FromAppError(error!);
        }

        var result = await booking.RescheduleAsync(
            User.GetUserId(),
            appointmentId,
            request.Date,
            start,
            practice: false,
            Metadata(),
            cancellationToken,
            request.Reason);
        return result.ToActionResult(this);
    }

    [HttpGet("practice")]
    public async Task<ActionResult<IReadOnlyList<AppointmentDetails>>> Practice(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? clinicId,
        [FromQuery] bool assignedOnly,
        CancellationToken cancellationToken)
    {
        if (!CanPractice())
        {
            return Forbid();
        }

        if (DateValidation.ValidateInclusiveRange(from, to) is { } rangeError)
        {
            return DomainProblemResults.FromAppError(rangeError);
        }

        return Ok(await booking.ListPracticeAsync(User.GetUserId(), from, to, cancellationToken, clinicId, assignedOnly));
    }

    [HttpGet("{appointmentId:guid}/practice")]
    public async Task<ActionResult<AppointmentDetails>> PracticeOne(Guid appointmentId, CancellationToken cancellationToken)
    {
        if (!CanPractice())
        {
            return Forbid();
        }

        var result = await booking.GetAsync(User.GetUserId(), appointmentId, practice: true, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{appointmentId:guid}/practice-cancellation")]
    public async Task<ActionResult<AppointmentDetails>> PracticeCancel(
        Guid appointmentId,
        AppointmentNoteRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanPractice())
        {
            return Forbid();
        }

        var result = await booking.CancelAsync(
            User.GetUserId(),
            appointmentId,
            request.Reason,
            practice: true,
            Metadata(),
            cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{appointmentId:guid}/practice-reschedule")]
    public async Task<ActionResult<AppointmentDetails>> PracticeReschedule(
        Guid appointmentId,
        RescheduleAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanPractice())
        {
            return Forbid();
        }

        if (!TryParseTime(request.Start, out var start, out var error))
        {
            return DomainProblemResults.FromAppError(error!);
        }

        var result = await booking.RescheduleAsync(
            User.GetUserId(),
            appointmentId,
            request.Date,
            start,
            practice: true,
            Metadata(),
            cancellationToken,
            request.Reason);
        return result.ToActionResult(this);
    }

    [HttpPost("{appointmentId:guid}/decision")]
    public async Task<ActionResult<AppointmentDetails>> Decide(
        Guid appointmentId,
        AppointmentDecisionRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanPractice())
        {
            return Forbid();
        }

        return Ok(await booking.DecideAsync(
            User.GetUserId(),
            appointmentId,
            request.Decision == "Approve",
            request.Reason,
            Metadata(),
            cancellationToken));
    }

    [HasPermission(PermissionCodes.AppointmentsBookWalkIn)]
    [HttpPost("walk-ins")]
    public async Task<ActionResult<AppointmentDetails>> WalkIn(WalkInAppointmentRequest request, CancellationToken cancellationToken)
    {
        if (!TryParseTime(request.Start, out var start, out var error))
        {
            return DomainProblemResults.FromAppError(error!);
        }

        return Ok(await booking.WalkInAsync(
            User.GetUserId(),
            request.ScheduleId,
            request.Date,
            start,
            request.PatientName,
            Metadata(),
            cancellationToken));
    }

    [HasPermission(PermissionCodes.AppointmentsManageClinic)]
    [HttpPost("desk-bookings")]
    public async Task<ActionResult<AppointmentDetails>> DeskBook(
        DeskBookAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseTime(request.Start, out var start, out var error))
        {
            return DomainProblemResults.FromAppError(error!);
        }

        return Ok(await booking.BookForPatientAsync(
            User.GetUserId(),
            request.ScheduleId,
            request.Date,
            start,
            request.PatientName,
            request.Phone,
            request.Reason,
            request.Notes,
            Metadata(),
            cancellationToken));
    }

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpGet("/api/v1/family-members")]
    public Task<IReadOnlyList<FamilyMemberDetails>> Family(CancellationToken cancellationToken) =>
        booking.ListFamilyAsync(User.GetUserId(), cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpPost("/api/v1/family-members")]
    public Task<FamilyMemberDetails> AddFamily(FamilyMemberRequest request, CancellationToken cancellationToken) =>
        booking.AddFamilyAsync(
            User.GetUserId(),
            request.FullName,
            request.DateOfBirth,
            request.Gender,
            request.Relationship,
            cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpDelete("/api/v1/family-members/{memberId:guid}")]
    public async Task<IActionResult> RemoveFamily(Guid memberId, CancellationToken cancellationToken)
    {
        await booking.RemoveFamilyAsync(User.GetUserId(), memberId, cancellationToken);
        return NoContent();
    }

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpGet("{appointmentId:guid}/share-preview")]
    public Task<SharedAppointmentSummary> SharePreview(Guid appointmentId, CancellationToken cancellationToken) =>
        shares.PreviewAsync(User.GetUserId(), appointmentId, cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpGet("{appointmentId:guid}/share-links")]
    public Task<IReadOnlyList<AppointmentShareLinkStatus>> ListShareLinks(Guid appointmentId, CancellationToken cancellationToken) =>
        shares.ListLinksAsync(User.GetUserId(), appointmentId, cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpPost("{appointmentId:guid}/share-links")]
    public Task<AppointmentShareLinkDetails> CreateShareLink(
        Guid appointmentId,
        [FromBody] CreateAppointmentShareLinkRequest? request,
        CancellationToken cancellationToken) =>
        shares.CreateLinkAsync(User.GetUserId(), appointmentId, request?.LifetimeHours, Metadata(), cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpPost("{appointmentId:guid}/share-links/{shareId:guid}/revoke")]
    public async Task<IActionResult> RevokeShareLink(Guid appointmentId, Guid shareId, CancellationToken cancellationToken)
    {
        await shares.RevokeLinkAsync(User.GetUserId(), appointmentId, shareId, Metadata(), cancellationToken);
        return NoContent();
    }

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpPost("{appointmentId:guid}/share-links/revoke-all")]
    public async Task<IActionResult> RevokeAllShareLinks(Guid appointmentId, CancellationToken cancellationToken)
    {
        await shares.RevokeAllAsync(User.GetUserId(), appointmentId, Metadata(), cancellationToken);
        return NoContent();
    }

    private bool CanPractice() =>
        User.Claims.Any(claim =>
            claim.Type == "perm"
            && claim.Value is PermissionCodes.AppointmentsManageAssigned
                or PermissionCodes.ClinicManage
                or PermissionCodes.AppointmentsBookWalkIn
                or PermissionCodes.AppointmentsManageClinic);

    private RequestMetadata Metadata() => new(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext));

    private static bool TryParseTime(string? value, out TimeOnly time, out AppError? error)
    {
        if (TimeOnly.TryParse(value, out time))
        {
            error = null;
            return true;
        }

        time = default;
        error = AppError.Validation(
            "time_invalid",
            "Please select a valid time.",
            new Dictionary<string, string[]> { ["start"] = ["Please select a valid time."] });
        return false;
    }
}
