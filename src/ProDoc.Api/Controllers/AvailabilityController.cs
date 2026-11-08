using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Api.Errors;
using ProDoc.Application.Scheduling;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Route("api/v1/availability")]
public sealed class AvailabilityController(ScheduleService schedules) : ControllerBase
{
    [HasPermission(PermissionCodes.DoctorsSearch)]
    [HttpGet("doctors/{doctorId:guid}")]
    public async Task<ActionResult<IReadOnlyList<DayAvailability>>> Doctor(
        Guid doctorId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken)
    {
        var result = await schedules.GetCalendarAsync(
            doctorId,
            from,
            to,
            branchId,
            cancellationToken,
            ResolveHorizon());
        return result.ToActionResult(this);
    }

    [HasPermission(PermissionCodes.DoctorsSearch)]
    [HttpGet("next")]
    public async Task<ActionResult<DayAvailability>> Next(
        [FromQuery] Guid doctorId,
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken)
    {
        var day = await schedules.NextAvailableAsync(doctorId, branchId, cancellationToken, ResolveHorizon());
        return day is null ? NotFound() : Ok(day);
    }

    private AvailabilityHorizon ResolveHorizon()
    {
        // Doctors, receptionists and clinic managers keep extended calendar access.
        // Patients (DoctorsSearch only) are limited to this week + next week.
        var canManage = User.Claims.Any(claim =>
            claim.Type == "perm"
            && claim.Value is PermissionCodes.ScheduleManage
                or PermissionCodes.ClinicManage
                or PermissionCodes.AppointmentsManageClinic
                or PermissionCodes.AppointmentsBookWalkIn);
        return canManage ? AvailabilityHorizon.StaffExtended : AvailabilityHorizon.PatientTwoWeeks;
    }
}
