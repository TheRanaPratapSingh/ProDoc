using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ProDoc.Application.Partners;
using ProDoc.Domain.Common;
using ProDoc.Domain.Partners;
using ProDoc.Infrastructure.Partners;

namespace ProDoc.Api.Auth;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPartnerScopeAttribute(string scope) : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true
            || !string.Equals(user.FindFirstValue("token_use"), PartnerAccessTokenIssuer.TokenUse, StringComparison.Ordinal))
        {
            context.Result = new UnauthorizedObjectResult(new { title = "Partner authentication required.", type = "urn:prodoc:partner_unauthorized" });
            return;
        }

        var scopes = user.FindAll("scp").Select(item => item.Value)
            .Concat((user.FindFirstValue("scope") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (!scopes.Contains(scope, StringComparer.OrdinalIgnoreCase))
        {
            context.Result = new ObjectResult(new { title = $"Scope '{scope}' is required.", type = "urn:prodoc:partner_forbidden_scope" })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }
}

public static class PartnerPrincipalExtensions
{
    public static Guid GetPartnerAppId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");
        return Guid.TryParse(value, out var id)
            ? id
            : throw DomainException.Unauthorized("partner_unauthorized", "Partner token is missing subject.");
    }

    public static async Task<PartnerPrincipal> LoadPartnerPrincipalAsync(
        this ClaimsPrincipal user,
        IPartnerService partners,
        CancellationToken cancellationToken)
    {
        var appId = user.GetPartnerAppId();
        var principal = await partners.RequirePrincipalAsync(appId, cancellationToken);
        var tokenScopes = user.FindAll("scp").Select(item => item.Value)
            .Concat((user.FindFirstValue("scope") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(item => principal.Scopes.Contains(item, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return principal with { Scopes = tokenScopes.Length == 0 ? principal.Scopes : tokenScopes };
    }
}
