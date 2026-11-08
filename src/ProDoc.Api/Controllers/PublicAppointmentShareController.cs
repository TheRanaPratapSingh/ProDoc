using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProDoc.Application.Appointments;

namespace ProDoc.Api.Controllers;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting("appointment_share")]
[Route("api/v1/public/appointment-shares")]
public sealed class PublicAppointmentShareController(AppointmentShareService shares) : ControllerBase
{
    [HttpGet("{token}")]
    public Task<SharedAppointmentSummary> Get(string token, CancellationToken cancellationToken) =>
        shares.ResolvePublicAsync(token, cancellationToken);
}
