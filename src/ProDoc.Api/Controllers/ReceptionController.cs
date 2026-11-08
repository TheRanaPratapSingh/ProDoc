using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Api.Contracts;
using ProDoc.Application.Billing;
using ProDoc.Application.Providers;
using ProDoc.Application.Queue;
using ProDoc.Application.Reception;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/reception")]
public sealed class ReceptionController(ReceptionPortalService reception) : ControllerBase
{
    [HasPermission(PermissionCodes.QueueOperate)]
    [HttpGet("dashboard")]
    public Task<ReceptionDashboard> Dashboard(
        [FromQuery] Guid clinicId,
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken) =>
        reception.GetDashboardAsync(User.GetUserId(), clinicId, branchId, cancellationToken);

    [HasPermission(PermissionCodes.PatientsSearchClinic)]
    [HttpGet("patients")]
    public Task<IReadOnlyList<ReceptionPatientRow>> Patients(
        [FromQuery] Guid clinicId,
        [FromQuery] Guid? branchId,
        [FromQuery] string? q,
        CancellationToken cancellationToken) =>
        reception.SearchPatientsAsync(User.GetUserId(), clinicId, branchId, q, cancellationToken);

    [HasPermission(PermissionCodes.PatientsSearchClinic)]
    [HttpPost("patients")]
    public Task<ReceptionPatientProfile> RegisterPatient(DeskRegisterPatientRequest request, CancellationToken cancellationToken) =>
        reception.RegisterOrUpdatePatientAsync(User.GetUserId(), request.ClinicId, request.FullName, request.Phone, cancellationToken);

    [HasPermission(PermissionCodes.QueueOperate)]
    [HttpGet("clinics/{clinicId:guid}")]
    public Task<ReceptionClinicInfo> ClinicInfo(Guid clinicId, CancellationToken cancellationToken) =>
        reception.GetClinicInfoAsync(User.GetUserId(), clinicId, cancellationToken);

    [HasPermission(PermissionCodes.ReportsReadClinic)]
    [HttpGet("reports")]
    public Task<ReceptionReportSummary> Reports(
        [FromQuery] Guid clinicId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken) =>
        reception.GetReportAsync(User.GetUserId(), clinicId, from, to, cancellationToken);

    [HasPermission(PermissionCodes.BillingCollectDesk)]
    [HttpGet("payments/pending")]
    public Task<IReadOnlyList<ReceptionDeskPaymentRow>> PendingPayments(
        [FromQuery] Guid clinicId,
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken) =>
        reception.ListPendingPaymentsAsync(User.GetUserId(), clinicId, branchId, cancellationToken);

    [HasPermission(PermissionCodes.BillingCollectDesk)]
    [HttpPost("appointments/{appointmentId:guid}/collect")]
    public Task<PaymentDetails> Collect(Guid appointmentId, CancellationToken cancellationToken) =>
        reception.CollectPaymentAsync(User.GetUserId(), appointmentId, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.RecordsCoordinateClinic)]
    [HttpGet("appointments/{appointmentId:guid}/records-status")]
    public Task<ReceptionRecordsStatus> RecordsStatus(Guid appointmentId, CancellationToken cancellationToken) =>
        reception.GetRecordsStatusAsync(User.GetUserId(), appointmentId, cancellationToken);

    [HasPermission(PermissionCodes.AccountRead)]
    [HttpGet("notifications")]
    public Task<IReadOnlyList<NotificationDetails>> Notifications(CancellationToken cancellationToken) =>
        reception.ListNotificationsAsync(User.GetUserId(), cancellationToken);

    private RequestMetadata Meta() => new(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext));
}
