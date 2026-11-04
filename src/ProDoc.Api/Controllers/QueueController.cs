using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Api.Contracts;
using ProDoc.Application.Providers;
using ProDoc.Application.Queue;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class QueueController(QueueService queues) : ControllerBase
{
    [HasPermission(PermissionCodes.QueueOperate)]
    [HttpGet("clinics/desk")]
    public Task<IReadOnlyList<DeskClinic>> DeskClinics(CancellationToken cancellationToken) =>
        queues.ListDeskClinicsAsync(User.GetUserId(), cancellationToken);

    [HasPermission(PermissionCodes.QueueOperate)]
    [HttpGet("clinics/{clinicId:guid}/branches/{branchId:guid}/doctors")]
    public Task<IReadOnlyList<DeskDoctor>> DeskDoctors(Guid clinicId, Guid branchId, CancellationToken cancellationToken) =>
        queues.ListDeskDoctorsAsync(User.GetUserId(), clinicId, branchId, cancellationToken);

    [HasPermission(PermissionCodes.QueueOperate)]
    [HttpGet("clinics/{clinicId:guid}/branches/{branchId:guid}/queue-settings")]
    public Task<QueueSettingsDetails> GetSettings(Guid clinicId, Guid branchId, CancellationToken cancellationToken) =>
        queues.GetSettingsAsync(User.GetUserId(), clinicId, branchId, cancellationToken);

    [HasPermission(PermissionCodes.ClinicManage)]
    [HttpPut("clinics/{clinicId:guid}/branches/{branchId:guid}/queue-settings")]
    public Task<QueueSettingsDetails> PutSettings(Guid clinicId, Guid branchId, UpdateQueueSettingsRequest request, CancellationToken cancellationToken) =>
        queues.UpdateSettingsAsync(
            User.GetUserId(),
            clinicId,
            branchId,
            request.TokenPrefix,
            request.ScheduledPatientPriority,
            request.EarlyCheckInMinutes,
            request.LateCheckInMinutes,
            request.AllowWalkIns,
            request.AllowPriorityPatients,
            cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpPost("appointments/{appointmentId:guid}/check-in/qr-token")]
    public Task<CheckInTokenDetails> MintQr(Guid appointmentId, CancellationToken cancellationToken) =>
        queues.MintQrTokenAsync(User.GetUserId(), appointmentId, cancellationToken);

    [HasPermission(PermissionCodes.QueueOperate)]
    [HttpPost("check-ins/qr")]
    public Task<QueueEntryDetails> CheckInQr(QrCheckInRequest request, CancellationToken cancellationToken) =>
        queues.CheckInByQrAsync(User.GetUserId(), request.Token, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.QueueOperate)]
    [HttpPost("appointments/{appointmentId:guid}/check-in")]
    public Task<QueueEntryDetails> CheckIn(Guid appointmentId, CancellationToken cancellationToken) =>
        queues.CheckInAppointmentAsync(User.GetUserId(), appointmentId, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.PatientsSearchClinic)]
    [HttpGet("clinics/{clinicId:guid}/branches/{branchId:guid}/appointments/search")]
    public Task<IReadOnlyList<AppointmentSearchItem>> Search(
        Guid clinicId,
        Guid branchId,
        [FromQuery] string? reference,
        [FromQuery] string? patientName,
        [FromQuery] string? phone,
        [FromQuery] Guid? doctorId,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken) =>
        queues.SearchAppointmentsAsync(User.GetUserId(), clinicId, branchId, reference, patientName, phone, doctorId, date, cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsBookWalkIn)]
    [HttpPost("clinics/{clinicId:guid}/branches/{branchId:guid}/walk-ins")]
    public Task<WalkInDetails> WalkIn(Guid clinicId, Guid branchId, DeskWalkInRequest request, CancellationToken cancellationToken) =>
        queues.RegisterWalkInAsync(
            User.GetUserId(),
            clinicId,
            branchId,
            request.DoctorId,
            request.PatientName,
            request.ContactNumber,
            request.Reason,
            request.ConsultationFee,
            Meta(),
            cancellationToken);

    [HasPermission(PermissionCodes.QueueOperate)]
    [HttpGet("clinics/{clinicId:guid}/branches/{branchId:guid}/queue")]
    public Task<IReadOnlyList<QueueEntryDetails>> BranchQueue(Guid clinicId, Guid branchId, [FromQuery] DateOnly? date, CancellationToken cancellationToken) =>
        queues.ListBranchQueueAsync(User.GetUserId(), clinicId, branchId, date, cancellationToken);

    [HttpGet("clinics/{clinicId:guid}/branches/{branchId:guid}/doctors/{doctorId:guid}/queue")]
    public async Task<ActionResult<IReadOnlyList<QueueEntryDetails>>> DoctorQueue(
        Guid clinicId,
        Guid branchId,
        Guid doctorId,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        if (!CanOperateQueue())
        {
            return Forbid();
        }

        return Ok(await queues.ListDoctorQueueAsync(User.GetUserId(), clinicId, branchId, doctorId, date, cancellationToken));
    }

    [HttpGet("clinics/{clinicId:guid}/branches/{branchId:guid}/doctors/{doctorId:guid}/queue/summary")]
    public async Task<ActionResult<QueueSummary>> Summary(Guid clinicId, Guid branchId, Guid doctorId, CancellationToken cancellationToken)
    {
        if (!CanOperateQueue())
        {
            return Forbid();
        }

        return Ok(await queues.DoctorSummaryAsync(User.GetUserId(), clinicId, branchId, doctorId, cancellationToken));
    }

    [HttpGet("clinics/{clinicId:guid}/branches/{branchId:guid}/doctors/{doctorId:guid}/queue/display")]
    public async Task<ActionResult<QueueDisplayBoard>> Display(Guid clinicId, Guid branchId, Guid doctorId, CancellationToken cancellationToken)
    {
        if (!CanOperateQueue())
        {
            return Forbid();
        }

        return Ok(await queues.DisplayAsync(User.GetUserId(), clinicId, branchId, doctorId, cancellationToken));
    }

    [HttpPost("clinics/{clinicId:guid}/branches/{branchId:guid}/doctors/{doctorId:guid}/queue/call-next")]
    public async Task<ActionResult<QueueEntryDetails>> CallNext(Guid clinicId, Guid branchId, Guid doctorId, CancellationToken cancellationToken)
    {
        if (!CanOperateQueue())
        {
            return Forbid();
        }

        var next = await queues.CallNextAsync(User.GetUserId(), clinicId, branchId, doctorId, Meta(), cancellationToken);
        return next is null ? NoContent() : Ok(next);
    }

    [HttpPost("queue-entries/{entryId:guid}/call")]
    public Task<ActionResult<QueueEntryDetails>> Call(Guid entryId, CancellationToken cancellationToken) =>
        Act(entryId, () => queues.CallAsync(User.GetUserId(), entryId, Meta(), cancellationToken));

    [HttpPost("queue-entries/{entryId:guid}/recall")]
    public Task<ActionResult<QueueEntryDetails>> Recall(Guid entryId, CancellationToken cancellationToken) =>
        Act(entryId, () => queues.RecallAsync(User.GetUserId(), entryId, Meta(), cancellationToken));

    [HttpPost("queue-entries/{entryId:guid}/start-consultation")]
    public Task<ActionResult<QueueEntryDetails>> Start(Guid entryId, CancellationToken cancellationToken) =>
        Act(entryId, () => queues.StartAsync(User.GetUserId(), entryId, Meta(), cancellationToken));

    [HttpPost("queue-entries/{entryId:guid}/complete-consultation")]
    public Task<ActionResult<QueueEntryDetails>> Complete(Guid entryId, CancellationToken cancellationToken) =>
        Act(entryId, () => queues.CompleteAsync(User.GetUserId(), entryId, Meta(), cancellationToken));

    [HttpPost("queue-entries/{entryId:guid}/skip")]
    public Task<ActionResult<QueueEntryDetails>> Skip(Guid entryId, QueueNoteRequest request, CancellationToken cancellationToken) =>
        Act(entryId, () => queues.SkipAsync(User.GetUserId(), entryId, request.Reason, Meta(), cancellationToken));

    [HttpPost("queue-entries/{entryId:guid}/hold")]
    public Task<ActionResult<QueueEntryDetails>> Hold(Guid entryId, QueueNoteRequest request, CancellationToken cancellationToken) =>
        Act(entryId, () => queues.HoldAsync(User.GetUserId(), entryId, request.Reason, Meta(), cancellationToken));

    [HttpPost("queue-entries/{entryId:guid}/resume")]
    public Task<ActionResult<QueueEntryDetails>> Resume(Guid entryId, CancellationToken cancellationToken) =>
        Act(entryId, () => queues.ResumeAsync(User.GetUserId(), entryId, Meta(), cancellationToken));

    [HttpPost("queue-entries/{entryId:guid}/cancel")]
    public Task<ActionResult<QueueEntryDetails>> Cancel(Guid entryId, QueueNoteRequest request, CancellationToken cancellationToken) =>
        Act(entryId, () => queues.CancelEntryAsync(User.GetUserId(), entryId, request.Reason, Meta(), cancellationToken));

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpGet("patients/me/queue")]
    public Task<IReadOnlyList<QueueEntryDetails>> Mine(CancellationToken cancellationToken) =>
        queues.ListMyQueueAsync(User.GetUserId(), cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpGet("patients/me/queue/{entryId:guid}")]
    public Task<QueueEntryDetails> MineOne(Guid entryId, CancellationToken cancellationToken) =>
        queues.GetMyEntryAsync(User.GetUserId(), entryId, cancellationToken);

    [HasPermission(PermissionCodes.AppointmentsBookSelf)]
    [HttpGet("patients/me/notifications")]
    public Task<IReadOnlyList<NotificationDetails>> Notifications(CancellationToken cancellationToken) =>
        queues.ListNotificationsAsync(User.GetUserId(), cancellationToken);

    [HasPermission(PermissionCodes.AdminAccess)]
    [HttpGet("admin/queues")]
    public Task<IReadOnlyList<AdminQueueSnapshot>> Admin([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        queues.AdminSnapshotsAsync(from, to, cancellationToken);

    private async Task<ActionResult<QueueEntryDetails>> Act(Guid entryId, Func<Task<QueueEntryDetails>> action)
    {
        if (!CanOperateQueue())
        {
            return Forbid();
        }

        return Ok(await action());
    }

    private bool CanOperateQueue() =>
        User.Claims.Any(claim => claim.Type == "perm"
            && claim.Value is PermissionCodes.QueueOperate or PermissionCodes.AppointmentsManageAssigned or PermissionCodes.ClinicManage);

    private RequestMetadata Meta() => new(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext));
}
