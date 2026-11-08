using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Api.Contracts;
using ProDoc.Api.Errors;
using ProDoc.Application.Providers;
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
            Meta(),
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
            request.ExpectedRowVersion,
            Meta(),
            cancellationToken,
            request.BookingMode,
            request.ConfirmBookingModeChange));
    }

    [Authorize]
    [HttpPut("{scheduleId:guid}/hours")]
    public async Task<ActionResult<ScheduleDetails>> Hours(Guid scheduleId, PeriodsRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        return Ok(await schedules.ReplaceHoursAsync(
            User.GetUserId(),
            scheduleId,
            ParsePeriods(request),
            request.ExpectedRowVersion,
            Meta(),
            cancellationToken));
    }

    [Authorize]
    [HttpPut("{scheduleId:guid}/breaks")]
    public async Task<ActionResult<ScheduleDetails>> Breaks(Guid scheduleId, PeriodsRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        return Ok(await schedules.ReplaceBreaksAsync(
            User.GetUserId(),
            scheduleId,
            ParsePeriods(request),
            request.ExpectedRowVersion,
            Meta(),
            cancellationToken));
    }

    [Authorize]
    [HttpPost("{scheduleId:guid}/closures")]
    public async Task<ActionResult<ScheduleDetails>> Close(Guid scheduleId, ClosureRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        return Ok(await schedules.AddClosureAsync(
            User.GetUserId(),
            scheduleId,
            request.Date,
            request.Reason,
            request.ExpectedRowVersion,
            Meta(),
            cancellationToken));
    }

    [Authorize]
    [HttpDelete("{scheduleId:guid}/closures/{closureId:guid}")]
    public async Task<ActionResult<ScheduleDetails>> Reopen(
        Guid scheduleId,
        Guid closureId,
        [FromQuery] string? expectedRowVersion,
        CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        return Ok(await schedules.RemoveClosureAsync(
            User.GetUserId(),
            scheduleId,
            closureId,
            expectedRowVersion,
            Meta(),
            cancellationToken));
    }

    [Authorize]
    [HttpPost("{scheduleId:guid}/publication")]
    public async Task<ActionResult<ScheduleDetails>> Publish(Guid scheduleId, PublicationRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        return Ok(await schedules.SetPublishedAsync(
            User.GetUserId(),
            scheduleId,
            request.IsPublished,
            request.ExpectedRowVersion,
            Meta(),
            cancellationToken));
    }

    [Authorize]
    [HttpPost("{scheduleId:guid}/extras")]
    public async Task<ActionResult<ScheduleDetails>> Extra(Guid scheduleId, ExtraRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        return Ok(await schedules.AddExtraAsync(
            User.GetUserId(),
            scheduleId,
            request.Date,
            ParseTime(request.StartsAt),
            ParseTime(request.EndsAt),
            request.ExpectedRowVersion,
            Meta(),
            cancellationToken));
    }

    [Authorize]
    [HttpDelete("{scheduleId:guid}/extras/{extraId:guid}")]
    public async Task<ActionResult<ScheduleDetails>> RemoveExtra(
        Guid scheduleId,
        Guid extraId,
        [FromQuery] string? expectedRowVersion,
        CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        return Ok(await schedules.RemoveExtraAsync(
            User.GetUserId(),
            scheduleId,
            extraId,
            expectedRowVersion,
            Meta(),
            cancellationToken));
    }

    [Authorize]
    [HttpPut("{scheduleId:guid}/day-override")]
    public async Task<ActionResult<ScheduleDetails>> ReplaceDayOverride(
        Guid scheduleId,
        ReplaceDayOverrideRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        var windows = request.Windows
            .Select(window => (ParseTime(window.StartsAt), ParseTime(window.EndsAt)))
            .ToArray();
        return Ok(await schedules.ReplaceDayOverrideAsync(
            User.GetUserId(),
            scheduleId,
            request.Date,
            windows,
            request.Available,
            request.UnavailableReason,
            request.ExpectedRowVersion,
            Meta(),
            cancellationToken));
    }

    [Authorize]
    [HttpPost("{scheduleId:guid}/day-override/reset")]
    public async Task<ActionResult<ScheduleDetails>> ResetDayOverride(
        Guid scheduleId,
        ResetDayOverrideRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        return Ok(await schedules.ResetDayOverrideAsync(
            User.GetUserId(),
            scheduleId,
            request.Date,
            request.ExpectedRowVersion,
            Meta(),
            cancellationToken));
    }

    [Authorize]
    [HttpPost("{scheduleId:guid}/copy-day")]
    public async Task<ActionResult<ScheduleDetails>> CopyDay(
        Guid scheduleId,
        CopyDayScheduleRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        return Ok(await schedules.CopyDayScheduleAsync(
            User.GetUserId(),
            scheduleId,
            request.SourceDate,
            request.TargetDates,
            request.OverwriteExisting,
            request.ExpectedRowVersion,
            Meta(),
            cancellationToken));
    }

    [Authorize]
    [HttpGet("{scheduleId:guid}/closure-impact")]
    public async Task<ActionResult<ClosureImpact>> ClosureImpact(
        Guid scheduleId,
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        return Ok(await schedules.PreviewClosureImpactAsync(User.GetUserId(), scheduleId, date, cancellationToken));
    }

    [Authorize]
    [HttpDelete("{scheduleId:guid}")]
    public async Task<IActionResult> Delete(
        Guid scheduleId,
        [FromQuery] string? expectedRowVersion,
        CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        await schedules.DeleteAsync(User.GetUserId(), scheduleId, expectedRowVersion, Meta(), cancellationToken);
        return NoContent();
    }

    [HasPermission(PermissionCodes.DoctorsSearch)]
    [HttpGet("{scheduleId:guid}/slots")]
    public async Task<ActionResult<SlotAvailability>> Slots(
        Guid scheduleId,
        [FromQuery] DateOnly date,
        [FromQuery] Guid? exceptAppointmentId,
        CancellationToken cancellationToken)
    {
        var horizon = CanManageSlotsBeyondTwoWeeks()
            ? AvailabilityHorizon.StaffExtended
            : AvailabilityHorizon.PatientTwoWeeks;
        var result = await schedules.GetAvailabilityAsync(scheduleId, date, cancellationToken, exceptAppointmentId, horizon);
        return result.ToActionResult(this);
    }

    private bool CanManage() =>
        User.Claims.Any(claim => claim.Type == "perm" && claim.Value is PermissionCodes.ScheduleManage or PermissionCodes.ProviderOnboard or PermissionCodes.ClinicManage);

    private bool CanManageSlotsBeyondTwoWeeks() =>
        User.Claims.Any(claim =>
            claim.Type == "perm"
            && claim.Value is PermissionCodes.ScheduleManage
                or PermissionCodes.ClinicManage
                or PermissionCodes.ProviderOnboard
                or PermissionCodes.AppointmentsManageClinic
                or PermissionCodes.AppointmentsBookWalkIn);

    private RequestMetadata Meta() => new(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext));

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
