using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Auth;
using ProDoc.Domain.Identity;

namespace ProDoc.Infrastructure.Auth;

public sealed class JwtAccessTokenIssuer(IOptions<AuthCryptoOptions> options) : IAccessTokenIssuer
{
    private readonly AuthCryptoOptions _options = options.Value;

    public AccessToken Issue(AccountSnapshot account, DateTimeOffset now)
    {
        var expires = now.AddMinutes(_options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.UserId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("lang", account.PreferredLanguage),
            new("home_surface", account.HomeSurface)
        };
        claims.AddRange(account.Roles.Select(role => new Claim("role", role)));
        claims.AddRange(account.Permissions.Select(permission => new Claim("perm", permission)));
        foreach (var surface in account.AvailableSurfaces ?? DashboardResolver.ListSurfaces(account.Roles))
        {
            claims.Add(new Claim("surface", surface));
        }

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
