using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Application.Appointments;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Route("api/v1/admin/appointments")]
public sealed class AdminAppointmentsController(BookingService booking) : ControllerBase
{
    [HasPermission(PermissionCodes.AdminAccess)]
    [HttpGet]
    public Task<IReadOnlyList<AppointmentSummary>> List([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        booking.ListAdminAsync(from, to, cancellationToken);
}