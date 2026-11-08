namespace ProDoc.Application.Auth;

public sealed class SessionOptions
{
    public const string SectionName = "Auth";

    public int RefreshTokenDays { get; set; } = 30;

    public bool ExposeOtpInResponse { get; set; }

    public int OtpLifetimeMinutes { get; set; } = 5;

    public int OtpMaxAttempts { get; set; } = 5;

    public int OtpMaxRequestsPerWindow { get; set; } = 5;

    public int OtpRequestWindowMinutes { get; set; } = 15;

    public int OtpResendCooldownSeconds { get; set; } = 30;

    /// <summary>
    /// Authorized Super Admin mobile numbers (Indian mobile / E.164). Backend-only allowlist.
    /// Only these numbers may use Super Admin OTP login; they are never public registration.
    /// </summary>
    public string[] SuperAdminPhones { get; set; } = [];

    /// <summary>
    /// Development-only extra allowlist. Merged into SuperAdminPhones when the host is Development.
    /// Must remain empty outside Development.
    /// </summary>
    public string[] DevelopmentAdminPhones { get; set; } = [];
}
