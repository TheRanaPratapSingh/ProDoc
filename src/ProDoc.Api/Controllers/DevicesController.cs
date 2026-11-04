using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Application.Mobile;
using ProDoc.Application.Providers;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/devices")]
public sealed class DevicesController(MobileDeviceService devices) : ControllerBase
{
    [HasPermission(PermissionCodes.AccountRead)]
    [HttpPost("push")]
    public Task<PushDeviceDetails> Register(RegisterPushDeviceRequest request, CancellationToken cancellationToken) =>
        devices.RegisterAsync(
            User.GetUserId(),
            request.Platform,
            request.Token,
            request.AppVersion,
            Meta(),
            cancellationToken);

    [HasPermission(PermissionCodes.AccountRead)]
    [HttpDelete("push")]
    public async Task<IActionResult> Unregister(CancellationToken cancellationToken)
    {
        await devices.UnregisterAsync(User.GetUserId(), Meta(), cancellationToken);
        return NoContent();
    }

    private RequestMetadata Meta() => new(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext));
}
