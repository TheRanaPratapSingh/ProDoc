using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ProDoc.Application.Abstractions;

namespace ProDoc.Infrastructure.Auth;

public sealed class HmacOtpCodeProtector(IOptions<AuthCryptoOptions> options) : IOtpCodeProtector
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(options.Value.OtpHashKey);

    public string Hash(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return Convert.ToBase64String(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(code)));
    }

    public bool Verify(string code, string hash)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        var actual = Encoding.UTF8.GetBytes(Hash(code));
        var expected = Encoding.UTF8.GetBytes(hash);
        return actual.Length == expected.Length && CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
