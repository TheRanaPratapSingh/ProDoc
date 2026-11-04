using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Api.Contracts;
using ProDoc.Application.Scheduling;
using ProDoc.Domain.Common;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Route("api/v1/schedules")]
public sealed class SchedulesController(ScheduleService schedules) : ControllerBase
{
    [Authorize]
    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<ScheduleDetails>>> Mine(CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        return Ok(await schedules.ListMineAsync(User.GetUserId(), cancellationToken));
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<ScheduleDetails>> Create(CreateScheduleRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        return Ok(await schedules.CreateAsync(
            User.GetUserId(),
            request.DoctorId,
            request.ClinicId,
            request.BranchId,
            request.ConsultationFee,
            request.SlotDurationMinutes,
            request.BufferMinutes,
            cancellationToken));
    }

    [Authorize]
    [HttpPut("{scheduleId:guid}")]
    public async Task<ActionResult<ScheduleDetails>> Update(Guid scheduleId, UpdateScheduleRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        return Ok(await schedules.UpdateTermsAsync(
            User.GetUserId(),
            scheduleId,
            request.ConsultationFee,
            request.SlotDurationMinutes,
            request.BufferMinutes,
            request.MaxPatients,
            request.EffectiveFrom,
            request.EffectiveTo,
            request.TimeZoneId,
            request.RequiresApproval,
            cancellationToken));
    }

    [Authorize]
    [HttpPut("{scheduleId:guid}/hours")]
    public async Task<ActionResult<ScheduleDetails>> Hours(Guid scheduleId, PeriodsRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        return Ok(await schedules.ReplaceHoursAsync(User.GetUserId(), scheduleId, ParsePeriods(request), cancellationToken));
    }

    [Authorize]
    [HttpPut("{scheduleId:guid}/breaks")]
    public async Task<ActionResult<ScheduleDetails>> Breaks(Guid scheduleId, PeriodsRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        return Ok(await schedules.ReplaceBreaksAsync(User.GetUserId(), scheduleId, ParsePeriods(request), cancellationToken));
    }

    [Authorize]
    [HttpPost("{scheduleId:guid}/closures")]
    public async Task<ActionResult<ScheduleDetails>> Close(Guid scheduleId, ClosureRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        return Ok(await schedules.AddClosureAsync(User.GetUserId(), scheduleId, request.Date, request.Reason, cancellationToken));
    }

    [Authorize]
    [HttpDelete("{scheduleId:guid}/closures/{closureId:guid}")]
    public async Task<ActionResult<ScheduleDetails>> Reopen(Guid scheduleId, Guid closureId, CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        return Ok(await schedules.RemoveClosureAsync(User.GetUserId(), scheduleId, closureId, cancellationToken));
    }

    [Authorize]
    [HttpPost("{scheduleId:guid}/publication")]
    public async Task<ActionResult<ScheduleDetails>> Publish(Guid scheduleId, PublicationRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        return Ok(await schedules.SetPublishedAsync(User.GetUserId(), scheduleId, request.IsPublished, cancellationToken));
    }

    [Authorize]
    [HttpPost("{scheduleId:guid}/extras")]
    public async Task<ActionResult<ScheduleDetails>> Extra(Guid scheduleId, ExtraRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        return Ok(await schedules.AddExtraAsync(User.GetUserId(), scheduleId, request.Date, ParseTime(request.StartsAt), ParseTime(request.EndsAt), cancellationToken));
    }

    [Authorize]
    [HttpDelete("{scheduleId:guid}/extras/{extraId:guid}")]
    public async Task<ActionResult<ScheduleDetails>> RemoveExtra(Guid scheduleId, Guid extraId, CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        return Ok(await schedules.RemoveExtraAsync(User.GetUserId(), scheduleId, extraId, cancellationToken));
    }

    [Authorize]
    [HttpDelete("{scheduleId:guid}")]
    public async Task<IActionResult> Delete(Guid scheduleId, CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        await schedules.DeleteAsync(User.GetUserId(), scheduleId, cancellationToken);
        return NoContent();
    }

    [HasPermission(PermissionCodes.DoctorsSearch)]
    [HttpGet("{scheduleId:guid}/slots")]
    public async Task<ActionResult<SlotAvailability>> Slots(Guid scheduleId, [FromQuery] DateOnly date, CancellationToken cancellationToken) =>
        Ok(await schedules.GetAvailabilityAsync(scheduleId, date, cancellationToken));

    private bool CanManage() =>
        User.Claims.Any(claim => claim.Type == "perm" && claim.Value is PermissionCodes.ScheduleManage or PermissionCodes.ProviderOnboard or PermissionCodes.ClinicManage);

    private static IReadOnlyCollection<(DayOfWeek Day, TimeOnly StartsAt, TimeOnly EndsAt)> ParsePeriods(PeriodsRequest request) =>
        request.Periods.Select(period => (ParseDay(period.Day), ParseTime(period.StartsAt), ParseTime(period.EndsAt))).ToArray();

    private static DayOfWeek ParseDay(string value)
    {
        if (!Enum.TryParse<DayOfWeek>(value, ignoreCase: true, out var day))
        {
            throw DomainException.Validation("day_invalid", "Choose a day of the week.");
        }

        return day;
    }

    private static TimeOnly ParseTime(string value)
    {
        if (!TimeOnly.TryParse(value, out var time))
        {
            throw DomainException.Validation("time_invalid", "Enter a time such as 09:30.");
        }

        return time;
    }
}
