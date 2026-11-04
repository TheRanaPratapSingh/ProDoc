using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Api.Contracts;
using ProDoc.Application.Providers;
using ProDoc.Application.Queue;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Route("api/v1/clinics/{clinicId:guid}/staff")]
public sealed class ClinicStaffController(StaffService staff) : ControllerBase
{
    [HasPermission(PermissionCodes.StaffManage)]
    [HttpGet]
    public Task<IReadOnlyList<StaffMemberDetails>> List(Guid clinicId, CancellationToken cancellationToken) =>
        staff.ListAsync(User.GetUserId(), clinicId, cancellationToken);

    [HasPermission(PermissionCodes.StaffManage)]
    [HttpPost]
    public Task<StaffMemberDetails> Assign(Guid clinicId, AssignStaffRequest request, CancellationToken cancellationToken) =>
        staff.AssignAsync(
            User.GetUserId(),
            clinicId,
            request.PhoneNumber,
            request.Role,
            request.BranchId,
            new RequestMetadata(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext)),
            cancellationToken);

    [HasPermission(PermissionCodes.StaffManage)]
    [HttpDelete("{staffId:guid}")]
    public async Task<IActionResult> Remove(Guid clinicId, Guid staffId, CancellationToken cancellationToken)
    {
        await staff.RemoveAsync(
            User.GetUserId(),
            clinicId,
            staffId,
            new RequestMetadata(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext)),
            cancellationToken);
        return NoContent();
    }
}
