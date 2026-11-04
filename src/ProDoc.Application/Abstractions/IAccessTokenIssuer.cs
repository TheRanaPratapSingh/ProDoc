using ProDoc.Application.Auth;

namespace ProDoc.Application.Abstractions;

public readonly record struct AccessToken(string Value, DateTimeOffset ExpiresAtUtc);

public interface IAccessTokenIssuer
{
    AccessToken Issue(AccountSnapshot account, DateTimeOffset now);
}
