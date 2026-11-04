using System.Security.Cryptography;

namespace ProDoc.Domain.Providers;

public static class ApplicationReference
{
    public static string Create(string prefix, DateTimeOffset now)
    {
        Span<byte> bytes = stackalloc byte[4];
        RandomNumberGenerator.Fill(bytes);
        return $"{prefix}-{now.UtcDateTime:yyyy}-{Convert.ToHexString(bytes)}";
    }
}
