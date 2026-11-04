using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using ProDoc.Application.Abstractions;

namespace ProDoc.Infrastructure.Auth;

public sealed class ActiveAccountRequirement : IAuthorizationRequirement;

public sealed class ActiveAccountHandler(IAuthStore store, IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<ActiveAccountRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveAccountRequirement requirement)
    {
        var subject = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(subject, out var userId))
        {
            return;
        }

        var cancellationToken = httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        var account = await store.FindAccountByUserIdAsync(userId, cancellationToken);
        if (account is { DeletionRequestedAtUtc: null, SuspendedAtUtc: null })
        {
            context.Succeed(requirement);
        }
    }
}
