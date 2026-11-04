using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
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
        CancellationToken cancellationToken) =>
        Ok(await schedules.GetCalendarAsync(doctorId, from, to, branchId, cancellationToken));

    [HasPermission(PermissionCodes.DoctorsSearch)]
    [HttpGet("next")]
    public async Task<ActionResult<DayAvailability>> Next([FromQuery] Guid doctorId, [FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        var day = await schedules.NextAvailableAsync(doctorId, branchId, cancellationToken);
        return day is null ? NotFound() : Ok(day);
    }
}
