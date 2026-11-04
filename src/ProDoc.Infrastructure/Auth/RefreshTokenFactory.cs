using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using ProDoc.Application.Abstractions;

namespace ProDoc.Infrastructure.Auth;

public sealed class RefreshTokenFactory : IRefreshTokenFactory
{
    public IssuedRefreshToken Create()
    {
        var plaintext = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return new IssuedRefreshToken(plaintext, Hash(plaintext));
    }

    public string Hash(string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)));
    }
}
