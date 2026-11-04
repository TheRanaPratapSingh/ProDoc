using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Api.Contracts;
using ProDoc.Application.Scheduling;
using ProDoc.Domain.Common;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/clinics/{clinicId:guid}")]
public sealed class ClinicOperationsController(ClinicOperationsService operations) : ControllerBase
{
    [HttpGet("branches/{branchId:guid}/operating-hours")]
    public async Task<ActionResult<BranchOperations>> Hours(Guid clinicId, Guid branchId, CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        return Ok(await operations.GetAsync(User.GetUserId(), clinicId, branchId, cancellationToken));
    }

    [HttpPut("branches/{branchId:guid}/operating-hours")]
    public async Task<ActionResult<BranchOperations>> ReplaceHours(Guid clinicId, Guid branchId, OperatingHoursRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        var periods = request.Periods.Select(period => (ParseDay(period.Day), ParseTime(period.OpensAt), ParseTime(period.ClosesAt), period.IsClosed)).ToArray();
        return Ok(await operations.ReplaceHoursAsync(User.GetUserId(), clinicId, branchId, periods, cancellationToken));
    }

    [HttpPost("branches/{branchId:guid}/closures")]
    public async Task<ActionResult<BranchOperations>> Close(Guid clinicId, Guid branchId, ClosureRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        return Ok(await operations.AddClosureAsync(User.GetUserId(), clinicId, branchId, request.Date, request.Reason, cancellationToken));
    }

    [HttpDelete("branches/{branchId:guid}/closures/{closureId:guid}")]
    public async Task<ActionResult<BranchOperations>> Reopen(Guid clinicId, Guid branchId, Guid closureId, CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        return Ok(await operations.RemoveClosureAsync(User.GetUserId(), clinicId, branchId, closureId, cancellationToken));
    }

    [HttpPut("facilities")]
    public async Task<ActionResult<IReadOnlyList<string>>> Facilities(Guid clinicId, FacilityRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        return Ok(await operations.ReplaceFacilitiesAsync(User.GetUserId(), clinicId, request.Names, cancellationToken));
    }

    private bool CanManage() =>
        User.Claims.Any(claim => claim.Type == "perm" && claim.Value is PermissionCodes.ClinicManage or PermissionCodes.ProviderOnboard);

    private static DayOfWeek ParseDay(string value)
    {
        if (!Enum.TryParse<DayOfWeek>(value, true, out var day))
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
