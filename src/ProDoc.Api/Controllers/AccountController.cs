using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Api.Contracts;
using ProDoc.Application.Auth;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Route("api/v1/account")]
public sealed class AccountController(AccountService accounts) : ControllerBase
{
    [HasPermission(PermissionCodes.AccountRead)]
    [HttpGet("me")]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountResponse>> Me(CancellationToken cancellationToken)
    {
        var account = await accounts.GetAsync(User.GetUserId(), cancellationToken);
        return Ok(AccountResponse.From(account));
    }

    [HasPermission(PermissionCodes.AccountWrite)]
    [HttpPut("profile")]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AccountResponse>> UpdateProfile(
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var account = await accounts.UpdateProfileAsync(
            new UpdateProfileCommand(
                User.GetUserId(),
                request.FullName,
                request.PreferredLanguage,
                ClientContext.IpAddress(HttpContext),
                ClientContext.UserAgent(HttpContext)),
            cancellationToken);
        return Ok(AccountResponse.From(account));
    }

    [HasPermission(PermissionCodes.AccountDeleteRequest)]
    [HttpPost("deletion-request")]
    [ProducesResponseType<DeletionRequestResponse>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<DeletionRequestResponse>> RequestDeletion(CancellationToken cancellationToken)
    {
        var result = await accounts.RequestDeletionAsync(
            new RequestAccountDeletionCommand(
                User.GetUserId(),
                ClientContext.IpAddress(HttpContext),
                ClientContext.UserAgent(HttpContext)),
            cancellationToken);

        return Accepted(new DeletionRequestResponse(
            result.RequestedAtUtc,
            "Deletion has been requested. Active sessions were revoked. The account stays on record until the erasure workflow completes."));
    }
}
