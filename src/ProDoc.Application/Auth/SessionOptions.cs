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

    public string[] DevelopmentAdminPhones { get; set; } = [];
}
