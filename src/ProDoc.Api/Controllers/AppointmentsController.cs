using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Api.Contracts;
using ProDoc.Application.Appointments;
using ProDoc.Application.Providers;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/appointments")]
public sealed class AppointmentsController(BookingService booking) : ControllerBase
{
    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpPost("holds")]
    public Task<AppointmentDetails> Hold(HoldAppointmentRequest request, CancellationToken cancellationToken) =>
        booking.HoldAsync(User.GetUserId(), request.ScheduleId, request.Date, TimeOnly.Parse(request.Start), Metadata(), cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpPost("{appointmentId:guid}/confirm")]
    public Task<AppointmentDetails> Confirm(Guid appointmentId, ConfirmAppointmentRequest request, CancellationToken cancellationToken) =>
        booking.ConfirmAsync(User.GetUserId(), appointmentId, request.FamilyMemberId, request.Reason, request.Notes, Metadata(), cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpPost("{appointmentId:guid}/release")]
    public async Task<IActionResult> Release(Guid appointmentId, CancellationToken cancellationToken)
    {
        await booking.ReleaseAsync(User.GetUserId(), appointmentId, cancellationToken);
        return NoContent();
    }

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpGet]
    public Task<IReadOnlyList<AppointmentDetails>> Mine(CancellationToken cancellationToken) =>
        booking.ListMineAsync(User.GetUserId(), cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpGet("{appointmentId:guid}")]
    public Task<AppointmentDetails> Get(Guid appointmentId, CancellationToken cancellationToken) =>
        booking.GetAsync(User.GetUserId(), appointmentId, practice: false, cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpPost("{appointmentId:guid}/cancellation")]
    public Task<AppointmentDetails> Cancel(Guid appointmentId, AppointmentNoteRequest request, CancellationToken cancellationToken) =>
        booking.CancelAsync(User.GetUserId(), appointmentId, request.Reason, practice: false, Metadata(), cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpPost("{appointmentId:guid}/reschedule")]
    public Task<AppointmentDetails> Reschedule(Guid appointmentId, RescheduleAppointmentRequest request, CancellationToken cancellationToken) =>
        booking.RescheduleAsync(User.GetUserId(), appointmentId, request.Date, TimeOnly.Parse(request.Start), practice: false, Metadata(), cancellationToken);

    [HttpGet("practice")]
    public async Task<ActionResult<IReadOnlyList<AppointmentDetails>>> Practice([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        if (!CanPractice())
        {
            return Forbid();
        }

        return Ok(await booking.ListPracticeAsync(User.GetUserId(), from, to, cancellationToken));
    }

    [HttpGet("{appointmentId:guid}/practice")]
    public async Task<ActionResult<AppointmentDetails>> PracticeOne(Guid appointmentId, CancellationToken cancellationToken)
    {
        if (!CanPractice())
        {
            return Forbid();
        }

        return Ok(await booking.GetAsync(User.GetUserId(), appointmentId, practice: true, cancellationToken));
    }

    [HttpPost("{appointmentId:guid}/practice-cancellation")]
    public async Task<ActionResult<AppointmentDetails>> PracticeCancel(Guid appointmentId, AppointmentNoteRequest request, CancellationToken cancellationToken)
    {
        if (!CanPractice())
        {
            return Forbid();
        }

        return Ok(await booking.CancelAsync(User.GetUserId(), appointmentId, request.Reason, practice: true, Metadata(), cancellationToken));
    }

    [HttpPost("{appointmentId:guid}/practice-reschedule")]
    public async Task<ActionResult<AppointmentDetails>> PracticeReschedule(Guid appointmentId, RescheduleAppointmentRequest request, CancellationToken cancellationToken)
    {
        if (!CanPractice())
        {
            return Forbid();
        }

        return Ok(await booking.RescheduleAsync(User.GetUserId(), appointmentId, request.Date, TimeOnly.Parse(request.Start), practice: true, Metadata(), cancellationToken));
    }

    [HttpPost("{appointmentId:guid}/decision")]
    public async Task<ActionResult<AppointmentDetails>> Decide(Guid appointmentId, AppointmentDecisionRequest request, CancellationToken cancellationToken)
    {
        if (!CanPractice())
        {
            return Forbid();
        }

        return Ok(await booking.DecideAsync(User.GetUserId(), appointmentId, request.Decision == "Approve", request.Reason, Metadata(), cancellationToken));
    }

    [HasPermission(PermissionCodes.AppointmentsBookWalkIn)]
    [HttpPost("walk-ins")]
    public Task<AppointmentDetails> WalkIn(WalkInAppointmentRequest request, CancellationToken cancellationToken) =>
        booking.WalkInAsync(User.GetUserId(), request.ScheduleId, request.Date, TimeOnly.Parse(request.Start), request.PatientName, Metadata(), cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpGet("/api/v1/family-members")]
    public Task<IReadOnlyList<FamilyMemberDetails>> Family(CancellationToken cancellationToken) =>
        booking.ListFamilyAsync(User.GetUserId(), cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpPost("/api/v1/family-members")]
    public Task<FamilyMemberDetails> AddFamily(FamilyMemberRequest request, CancellationToken cancellationToken) =>
        booking.AddFamilyAsync(User.GetUserId(), request.FullName, request.DateOfBirth, request.Gender, request.Relationship, cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpDelete("/api/v1/family-members/{memberId:guid}")]
    public async Task<IActionResult> RemoveFamily(Guid memberId, CancellationToken cancellationToken)
    {
        await booking.RemoveFamilyAsync(User.GetUserId(), memberId, cancellationToken);
        return NoContent();
    }

    private bool CanPractice() =>
        User.Claims.Any(claim => claim.Type == "perm" && claim.Value is PermissionCodes.AppointmentsManageAssigned or PermissionCodes.ClinicManage or PermissionCodes.AppointmentsBookWalkIn);

    private RequestMetadata Metadata() => new(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext));
}
