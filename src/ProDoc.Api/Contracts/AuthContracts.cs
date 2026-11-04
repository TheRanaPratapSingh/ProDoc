using FluentValidation;
using ProDoc.Application.Auth;

namespace ProDoc.Api.Contracts;

public sealed class RequestOtpRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
}

public sealed class RequestOtpRequestValidator : AbstractValidator<RequestOtpRequest>
{
    public RequestOtpRequestValidator()
    {
        RuleFor(request => request.PhoneNumber).NotEmpty().MaximumLength(20);
    }
}

public sealed class VerifyOtpRequest
{
    public string PhoneNumber { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? DeviceName { get; set; }

    public string? ReferralCode { get; set; }
}

public sealed class VerifyOtpRequestValidator : AbstractValidator<VerifyOtpRequest>
{
    public VerifyOtpRequestValidator()
    {
        RuleFor(request => request.PhoneNumber).NotEmpty().MaximumLength(20);
        RuleFor(request => request.Code).NotEmpty().MaximumLength(10);
        RuleFor(request => request.DeviceName).MaximumLength(80);
        RuleFor(request => request.ReferralCode).MaximumLength(16);
    }
}

public sealed class RefreshSessionRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class RefreshSessionRequestValidator : AbstractValidator<RefreshSessionRequest>
{
    public RefreshSessionRequestValidator()
    {
        RuleFor(request => request.RefreshToken).NotEmpty().MaximumLength(512);
    }
}

public sealed class LogoutRequest
{
    public string? RefreshToken { get; set; }
}

public sealed class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
    {
        RuleFor(request => request.RefreshToken).MaximumLength(512);
    }
}

public sealed class UpdateProfileRequest
{
    public string? FullName { get; set; }

    public string PreferredLanguage { get; set; } = "en";
}

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(request => request.FullName).MaximumLength(80);
        RuleFor(request => request.PreferredLanguage)
            .Must(language => language is "en" or "hi")
            .WithMessage("Choose English or Hindi.");
    }
}

public sealed record OtpRequestedResponse(Guid ChallengeId, DateTimeOffset ExpiresAtUtc, string? DevelopmentCode);

public sealed record AccountResponse(
    Guid UserId,
    string PhoneNumber,
    string? FullName,
    string PreferredLanguage,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    DateTimeOffset? DeletionRequestedAtUtc)
{
    public static AccountResponse From(AccountSnapshot account) => new(
        account.UserId,
        account.PhoneNumber,
        account.FullName,
        account.PreferredLanguage,
        account.Roles,
        account.Permissions,
        account.DeletionRequestedAtUtc);
}

public sealed record AuthSessionResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    AccountResponse Account)
{
    public static AuthSessionResponse From(AuthSessionResult session) => new(
        session.AccessToken,
        session.AccessTokenExpiresAtUtc,
        session.RefreshToken,
        session.RefreshTokenExpiresAtUtc,
        AccountResponse.From(session.Account));
}

public sealed record DeletionRequestResponse(DateTimeOffset RequestedAtUtc, string Message);
