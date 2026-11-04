using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProDoc.Application.Partners;

namespace ProDoc.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("oauth/partner")]
[EnableRateLimiting("partner_oauth")]
public sealed class PartnerOAuthController(IPartnerService partners) : ControllerBase
{
    /// <summary>OAuth 2.0 client_credentials token endpoint for partner applications.</summary>
    [HttpPost("token")]
    [Consumes("application/x-www-form-urlencoded", "application/json")]
    public async Task<ActionResult<PartnerOAuthTokenResponse>> Token(CancellationToken cancellationToken)
    {
        string? clientId = null;
        string? clientSecret = null;
        string? scope = null;
        string? grantType = null;

        if (Request.HasFormContentType)
        {
            clientId = Request.Form["client_id"];
            clientSecret = Request.Form["client_secret"];
            scope = Request.Form["scope"];
            grantType = Request.Form["grant_type"];
        }
        else
        {
            var body = await Request.ReadFromJsonAsync<PartnerTokenRequest>(cancellationToken: cancellationToken);
            clientId = body?.ClientId;
            clientSecret = body?.ClientSecret;
            scope = body?.Scope;
            grantType = body?.GrantType;
        }

        if (!string.Equals(grantType, "client_credentials", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "unsupported_grant_type", error_description = "Only client_credentials is supported." });
        }

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return BadRequest(new { error = "invalid_request", error_description = "client_id and client_secret are required." });
        }

        var token = await partners.ExchangeClientCredentialsAsync(clientId, clientSecret, scope, cancellationToken);
        return Ok(new PartnerOAuthTokenResponse(token.AccessToken, token.TokenType, token.ExpiresIn, token.Scope));
    }
}

public sealed record PartnerTokenRequest(
    [property: JsonPropertyName("grant_type")] string? GrantType,
    [property: JsonPropertyName("client_id")] string? ClientId,
    [property: JsonPropertyName("client_secret")] string? ClientSecret,
    [property: JsonPropertyName("scope")] string? Scope);

public sealed record PartnerOAuthTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("scope")] string Scope);
