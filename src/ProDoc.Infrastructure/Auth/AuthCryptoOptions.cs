namespace ProDoc.Infrastructure.Auth;

public sealed class AuthCryptoOptions
{
    public const string SectionName = "Auth";

    public string Issuer { get; set; } = "prodoc";

    public string Audience { get; set; } = "prodoc-clients";

    public string SigningKey { get; set; } = string.Empty;

    public string OtpHashKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
}

public static class DevelopmentAuthKeys
{
    public const string SigningKey = "dev-only-signing-key-change-before-any-shared-env-01";

    public const string OtpHashKey = "dev-only-otp-hash-key-change-before-any-shared-env";
}
