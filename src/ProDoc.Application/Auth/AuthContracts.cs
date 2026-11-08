namespace ProDoc.Application.Auth;

public sealed record AccountSnapshot(
    Guid UserId,
    string PhoneNumber,
    string? FullName,
    string PreferredLanguage,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    DateTimeOffset? DeletionRequestedAtUtc,
    DateTimeOffset? SuspendedAtUtc = null,
    string HomeSurface = "patient",
    IReadOnlyList<string>? AvailableSurfaces = null,
    int? AgeYears = null,
    string? Gender = null,
    bool ProfileCompleted = false);

public sealed record OtpDispatchResult(Guid ChallengeId, DateTimeOffset ExpiresAtUtc, string? DevelopmentCode, string Purpose);

public sealed record AuthSessionResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    AccountSnapshot Account);

/// <summary>
/// Expected OTP / session failures. Failures are returned as values (not thrown)
/// so incorrect/expired codes and rate limits stay business responses instead of debugger breaks.
/// </summary>
public sealed record AuthFailure(string Code, string Message, int StatusCode);

/// <summary>
/// OTP request outcome. Rate limits, invalid phones, and delivery failures are Failures — never thrown.
/// </summary>
public sealed class AuthOtpRequestResult
{
    private AuthOtpRequestResult(OtpDispatchResult? dispatch, AuthFailure? failure)
    {
        Dispatch = dispatch;
        Failure = failure;
    }

    public OtpDispatchResult? Dispatch { get; }

    public AuthFailure? Failure { get; }

    public bool Succeeded => Dispatch is not null;

    public static AuthOtpRequestResult Ok(OtpDispatchResult dispatch) => new(dispatch, null);

    public static AuthOtpRequestResult Fail(string code, string message, int statusCode = 400) =>
        new(null, new AuthFailure(code, message, statusCode));

    public static AuthOtpRequestResult Fail(AuthFailure failure) => new(null, failure);
}

public sealed class AuthVerifyResult
{
    private AuthVerifyResult(AuthSessionResult? session, AuthFailure? failure)
    {
        Session = session;
        Failure = failure;
    }

    public AuthSessionResult? Session { get; }

    public AuthFailure? Failure { get; }

    public bool Succeeded => Session is not null;

    public static AuthVerifyResult Ok(AuthSessionResult session) => new(session, null);

    public static AuthVerifyResult Fail(string code, string message, int statusCode = 400) =>
        new(null, new AuthFailure(code, message, statusCode));

    public static AuthVerifyResult Fail(AuthFailure failure) => new(null, failure);

    public static AuthVerifyResult Fail(Domain.Common.DomainException exception) =>
        new(null, new AuthFailure(exception.Code, exception.Title, exception.StatusCode));
}

/// <summary>
/// Refresh-token outcome. Invalid/reuse cases are Failures — never thrown.
/// </summary>
public sealed class AuthRefreshResult
{
    private AuthRefreshResult(AuthSessionResult? session, AuthFailure? failure)
    {
        Session = session;
        Failure = failure;
    }

    public AuthSessionResult? Session { get; }

    public AuthFailure? Failure { get; }

    public bool Succeeded => Session is not null;

    public static AuthRefreshResult Ok(AuthSessionResult session) => new(session, null);

    public static AuthRefreshResult Fail(string code, string message, int statusCode = 401) =>
        new(null, new AuthFailure(code, message, statusCode));
}

public sealed record DeletionRequestResult(DateTimeOffset RequestedAtUtc);
