using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ProDoc.Application.Partners;
using ProDoc.Infrastructure.Auth;

namespace ProDoc.Infrastructure.Partners;

public sealed class PartnerAccessTokenIssuer(IOptions<AuthCryptoOptions> options) : IPartnerAccessTokenIssuer
{
    public const string AuthenticationScheme = "PartnerBearer";
    public const string Audience = "prodoc-partners";
    public const string TokenUse = "partner";
    public const int AccessTokenMinutes = 10;

    private readonly AuthCryptoOptions _options = options.Value;

    public PartnerTokenResult Issue(PartnerPrincipal principal, DateTimeOffset now)
    {
        var expires = now.AddMinutes(AccessTokenMinutes);
        var scope = string.Join(' ', principal.Scopes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, principal.PartnerAppId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("token_use", TokenUse),
            new("client_id", principal.ClientId),
            new("partner_org", principal.PartnerOrganizationId.ToString()),
            new("env", principal.Environment.ToString().ToLowerInvariant()),
            new("scope", scope),
            new("rate_limit", principal.RateLimitPerMinute.ToString()),
            new("daily_quota", principal.DailyQuota.ToString())
        };
        foreach (var item in principal.Scopes)
        {
            claims.Add(new Claim("scp", item));
        }

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new PartnerTokenResult(
            new JwtSecurityTokenHandler().WriteToken(token),
            "Bearer",
            AccessTokenMinutes * 60,
            scope);
    }
}
