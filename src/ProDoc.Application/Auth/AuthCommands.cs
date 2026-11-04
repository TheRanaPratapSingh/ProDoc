namespace ProDoc.Application.Auth;

public sealed record RequestOtpCommand(string PhoneNumber, string? IpAddress, string? UserAgent);

public sealed record VerifyOtpCommand(
    string PhoneNumber,
    string Code,
    string? DeviceName,
    string? IpAddress,
    string? UserAgent,
    string? ReferralCode = null);

public sealed record RefreshSessionCommand(string RefreshToken, string? IpAddress, string? UserAgent);

public sealed record LogoutCommand(string RefreshToken, string? IpAddress, string? UserAgent);

public sealed record UpdateProfileCommand(Guid UserId, string? FullName, string PreferredLanguage, string? IpAddress, string? UserAgent);

public sealed record RequestAccountDeletionCommand(Guid UserId, string? IpAddress, string? UserAgent);
