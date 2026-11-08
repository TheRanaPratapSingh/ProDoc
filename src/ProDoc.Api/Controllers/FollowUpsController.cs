using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Api.Contracts;
using ProDoc.Application.Clinical;
using ProDoc.Application.Providers;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/follow-ups")]
public sealed class FollowUpsController(FollowUpService followUps) : ControllerBase
{
    [HasPermission(PermissionCodes.AppointmentsManageAssigned)]
    [HttpPost]
    public Task<FollowUpDetails> Create(CreateFollowUpRequest request, CancellationToken cancellationToken) =>
        followUps.CreateAsync(
            User.GetUserId(),
            request.SourceAppointmentId,
            request.SourceConsultationId,
            request.PatientUserId ?? Guid.Empty,
            request.FamilyMemberId,
            request.PatientName ?? string.Empty,
            request.ClinicId ?? Guid.Empty,
            request.BranchId ?? Guid.Empty,
            request.RecommendedDate,
            request.FollowUpNote,
            request.RequiresBooking,
            Meta(),
            cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsManageAssigned)]
    [HttpPut("{followUpId:guid}")]
    public Task<FollowUpDetails> Update(Guid followUpId, UpdateFollowUpRequest request, CancellationToken cancellationToken) =>
        followUps.UpdateAsync(
            User.GetUserId(),
            followUpId,
            request.RecommendedDate,
            request.FollowUpNote,
            request.RequiresBooking,
            Meta(),
            cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsManageAssigned)]
    [HttpPost("{followUpId:guid}/cancellation")]
    public Task<FollowUpDetails> Cancel(Guid followUpId, CancellationToken cancellationToken) =>
        followUps.CancelAsync(User.GetUserId(), followUpId, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsManageAssigned)]
    [HttpPost("{followUpId:guid}/completion")]
    public Task<FollowUpDetails> Complete(Guid followUpId, CancellationToken cancellationToken) =>
        followUps.CompleteAsync(User.GetUserId(), followUpId, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsManageAssigned)]
    [HttpGet("doctor")]
    public Task<IReadOnlyList<FollowUpDetails>> ListDoctor(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? patient,
        [FromQuery] string? status,
        [FromQuery] Guid? clinicId,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default) =>
        followUps.ListForDoctorAsync(
            User.GetUserId(),
            new FollowUpListFilter(from, to, patient, status, clinicId, null, take),
            cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsManageAssigned)]
    [HttpGet("doctor/{followUpId:guid}")]
    public Task<FollowUpDetails> GetDoctor(Guid followUpId, CancellationToken cancellationToken) =>
        followUps.GetForDoctorAsync(User.GetUserId(), followUpId, cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpGet("me")]
    public Task<IReadOnlyList<FollowUpDetails>> ListMine(CancellationToken cancellationToken) =>
        followUps.ListForPatientAsync(User.GetUserId(), cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpGet("me/{followUpId:guid}")]
    public Task<FollowUpDetails> GetMine(Guid followUpId, CancellationToken cancellationToken) =>
        followUps.GetForPatientAsync(User.GetUserId(), followUpId, cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpPost("{followUpId:guid}/link-appointment")]
    public async Task<IActionResult> LinkAppointment(
        Guid followUpId,
        LinkFollowUpAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        await followUps.LinkAppointmentAsync(User.GetUserId(), followUpId, request.AppointmentId, Meta(), cancellationToken);
        return NoContent();
    }

    [HasPermission(PermissionCodes.QueueOperate)]
    [HttpGet("reception")]
    public Task<IReadOnlyList<FollowUpDetails>> ListReception(
        [FromQuery] Guid clinicId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? patient,
        [FromQuery] string? status,
        [FromQuery] Guid? doctorId,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default) =>
        followUps.ListForReceptionAsync(
            User.GetUserId(),
            new FollowUpListFilter(from, to, patient, status, clinicId, doctorId, take),
            cancellationToken);

    [HasPermission(PermissionCodes.QueueOperate)]
    [HttpGet("reception/{followUpId:guid}")]
    public Task<FollowUpDetails> GetReception(Guid followUpId, CancellationToken cancellationToken) =>
        followUps.GetForReceptionAsync(User.GetUserId(), followUpId, cancellationToken);

    private RequestMetadata Meta() => new(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext));
}
