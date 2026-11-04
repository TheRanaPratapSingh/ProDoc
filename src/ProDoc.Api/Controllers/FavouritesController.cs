using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Application.Scheduling;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/favourites")]
public sealed class FavouritesController(FavouriteService favourites) : ControllerBase
{
    [HasPermission(PermissionCodes.DoctorsSearch)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DiscoveredDoctor>>> List(CancellationToken cancellationToken) =>
        Ok(await favourites.ListAsync(User.GetUserId(), cancellationToken));

    [HasPermission(PermissionCodes.DoctorsSearch)]
    [HttpPut("{doctorId:guid}")]
    public async Task<IActionResult> Add(Guid doctorId, CancellationToken cancellationToken)
    {
        await favourites.AddAsync(User.GetUserId(), doctorId, cancellationToken);
        return NoContent();
    }

    [HasPermission(PermissionCodes.DoctorsSearch)]
    [HttpDelete("{doctorId:guid}")]
    public async Task<IActionResult> Remove(Guid doctorId, CancellationToken cancellationToken)
    {
        await favourites.RemoveAsync(User.GetUserId(), doctorId, cancellationToken);
        return NoContent();
    }
}
