using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProDoc.Api.Auth;
using ProDoc.Api.Contracts;
using ProDoc.Api.Errors;
using ProDoc.Application.Auth;

namespace ProDoc.Api.Controllers;

[ApiController]
[EnableRateLimiting("auth")]
[Route("api/v1/auth")]
public sealed class AuthController(AuthService authService) : ControllerBase
{
    [HttpPost("otp/request")]
    [ProducesResponseType<OtpRequestedResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<OtpRequestedResponse>> RequestOtp(
        RequestOtpRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await authService.RequestOtpAsync(
            new RequestOtpCommand(request.PhoneNumber, ClientAddress(), ClientAgent(), request.Purpose),
            cancellationToken);

        if (!outcome.Succeeded || outcome.Dispatch is null)
        {
            return DomainProblemResults.FromAuthFailure(
                outcome.Failure ?? new AuthFailure("otp_request_failed", "Unable to send a code. Please try again.", 400));
        }

        var result = outcome.Dispatch;
        return Ok(new OtpRequestedResponse(result.ChallengeId, result.ExpiresAtUtc, result.DevelopmentCode, result.Purpose));
    }

    [HttpPost("otp/verify")]
    [ProducesResponseType<AuthSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthSessionResponse>> VerifyOtp(
        VerifyOtpRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await authService.VerifyOtpAsync(
            new VerifyOtpCommand(
                request.PhoneNumber,
                request.Code,
                request.DeviceName,
                ClientAddress(),
                ClientAgent(),
                request.ReferralCode,
                request.Purpose),
            cancellationToken);

        if (!outcome.Succeeded || outcome.Session is null)
        {
            return DomainProblemResults.FromAuthFailure(
                outcome.Failure ?? new AuthFailure("otp_invalid", "Incorrect OTP. Please try again.", 400));
        }

        return Ok(AuthSessionResponse.From(outcome.Session));
    }

    [HttpPost("refresh")]
    [ProducesResponseType<AuthSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthSessionResponse>> Refresh(
        RefreshSessionRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await authService.RefreshAsync(
            new RefreshSessionCommand(request.RefreshToken, ClientAddress(), ClientAgent()),
            cancellationToken);

        if (!outcome.Succeeded || outcome.Session is null)
        {
            return DomainProblemResults.FromAuthFailure(
                outcome.Failure ?? new AuthFailure("refresh_invalid", "The session is no longer valid. Sign in again.", 401));
        }

        return Ok(AuthSessionResponse.From(outcome.Session));
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(
            new LogoutCommand(request.RefreshToken ?? string.Empty, ClientAddress(), ClientAgent()),
            cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPost("logout-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        await authService.LogoutAllAsync(User.GetUserId(), ClientAddress(), ClientAgent(), cancellationToken);
        return NoContent();
    }

    private string? ClientAddress() => ClientContext.IpAddress(HttpContext);

    private string? ClientAgent() => ClientContext.UserAgent(HttpContext);
}
