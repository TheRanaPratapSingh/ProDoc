using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using ProDoc.Domain.Common;

namespace ProDoc.Api.Auth;

internal static class ClientContext
{
    public static string? IpAddress(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString();

    public static string? UserAgent(HttpContext httpContext)
    {
        var agent = httpContext.Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(agent) ? null : agent;
    }

    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(subject, out var userId))
        {
            throw DomainException.Unauthorized("unauthorized", "Sign in required.");
        }

        return userId;
    }
}
