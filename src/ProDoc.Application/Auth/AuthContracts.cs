namespace ProDoc.Application.Auth;

public sealed record AccountSnapshot(
    Guid UserId,
    string PhoneNumber,
    string? FullName,
    string PreferredLanguage,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    DateTimeOffset? DeletionRequestedAtUtc,
    DateTimeOffset? SuspendedAtUtc = null);

public sealed record OtpDispatchResult(Guid ChallengeId, DateTimeOffset ExpiresAtUtc, string? DevelopmentCode);

public sealed record AuthSessionResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    AccountSnapshot Account);

public sealed record DeletionRequestResult(DateTimeOffset RequestedAtUtc);
