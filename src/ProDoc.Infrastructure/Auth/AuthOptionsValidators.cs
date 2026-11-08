using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ProDoc.Application.Auth;

namespace ProDoc.Infrastructure.Auth;

public sealed class AuthCryptoOptionsValidator(IHostEnvironment environment) : IValidateOptions<AuthCryptoOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthCryptoOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience))
        {
            return ValidateOptionsResult.Fail("Auth issuer and audience are required.");
        }

        if (options.SigningKey.Length < 32)
        {
            return ValidateOptionsResult.Fail("Auth:SigningKey must be at least 32 characters.");
        }

        if (options.OtpHashKey.Length < 32)
        {
            return ValidateOptionsResult.Fail("Auth:OtpHashKey must be at least 32 characters.");
        }

        if (options.AccessTokenMinutes is < 5 or > 60)
        {
            return ValidateOptionsResult.Fail("Auth:AccessTokenMinutes must be between 5 and 60.");
        }

        if (!environment.IsDevelopment() &&
            (options.SigningKey == DevelopmentAuthKeys.SigningKey || options.OtpHashKey == DevelopmentAuthKeys.OtpHashKey))
        {
            return ValidateOptionsResult.Fail("Development auth keys cannot be used outside Development.");
        }

        return ValidateOptionsResult.Success;
    }
}

public sealed class SessionOptionsValidator(IHostEnvironment environment) : IValidateOptions<SessionOptions>
{
    public ValidateOptionsResult Validate(string? name, SessionOptions options)
    {
        if (options.RefreshTokenDays is < 1 or > 90)
        {
            return ValidateOptionsResult.Fail("Auth:RefreshTokenDays must be between 1 and 90.");
        }

        if (options.OtpLifetimeMinutes is < 1 or > 10)
        {
            return ValidateOptionsResult.Fail("Auth:OtpLifetimeMinutes must be between 1 and 10.");
        }

        if (options.OtpMaxAttempts is < 3 or > 10)
        {
            return ValidateOptionsResult.Fail("Auth:OtpMaxAttempts must be between 3 and 10.");
        }

        if (options.OtpMaxRequestsPerWindow is < 1 or > 200)
        {
            return ValidateOptionsResult.Fail("Auth:OtpMaxRequestsPerWindow must be between 1 and 200.");
        }

        if (options.OtpRequestWindowMinutes is < 1 or > 60)
        {
            return ValidateOptionsResult.Fail("Auth:OtpRequestWindowMinutes must be between 1 and 60.");
        }

        if (options.OtpResendCooldownSeconds is < 0 or > 120)
        {
            return ValidateOptionsResult.Fail("Auth:OtpResendCooldownSeconds must be between 0 and 120.");
        }

        if (!environment.IsDevelopment() && options.ExposeOtpInResponse)
        {
            return ValidateOptionsResult.Fail("Auth:ExposeOtpInResponse cannot be enabled outside Development.");
        }

        if (!environment.IsDevelopment() && options.DevelopmentAdminPhones.Length > 0)
        {
            return ValidateOptionsResult.Fail("Auth:DevelopmentAdminPhones must be empty outside Development.");
        }

        foreach (var phone in options.SuperAdminPhones.Concat(options.DevelopmentAdminPhones))
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return ValidateOptionsResult.Fail("Auth Super Admin phone entries cannot be blank.");
            }

            try
            {
                _ = Domain.Identity.PhoneNumber.ParseIndianMobile(phone);
            }
            catch (Domain.Common.DomainException)
            {
                return ValidateOptionsResult.Fail($"Auth Super Admin phone '{phone}' is not a valid Indian mobile number.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
