using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Growth;
using ProDoc.Application.Providers;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;

namespace ProDoc.Application.Auth;

public sealed class AuthService(
    IAuthStore store,
    IOtpCodeProtector otpProtector,
    IOtpSender otpSender,
    IRefreshTokenFactory refreshTokens,
    IAccessTokenIssuer accessTokens,
    IClock clock,
    IOptions<SessionOptions> options,
    ILogger<AuthService> logger,
    GrowthService? growth = null)
{
    private readonly SessionOptions _options = options.Value;

    public async Task<OtpDispatchResult> RequestOtpAsync(RequestOtpCommand command, CancellationToken cancellationToken)
    {
        var phone = PhoneNumber.ParseIndianMobile(command.PhoneNumber);
        var now = clock.UtcNow;
        var since = now.AddMinutes(-_options.OtpRequestWindowMinutes);
        var recent = await store.CountRecentOtpChallengesAsync(phone.E164, since, cancellationToken);
        if (recent >= _options.OtpMaxRequestsPerWindow)
        {
            throw DomainException.TooManyRequests(
                "otp_rate_limited",
                "Too many codes were requested. Wait a few minutes and try again.");
        }

        var latest = await store.FindLatestOtpChallengeAsync(phone.E164, OtpPurpose.SignIn, cancellationToken);
        if (latest is not null && latest.CreatedAtUtc.AddSeconds(_options.OtpResendCooldownSeconds) > now)
        {
            throw DomainException.TooManyRequests(
                "otp_cooldown",
                "A code was just sent. Wait a moment before requesting another.");
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var challenge = OtpChallenge.Issue(
            phone.E164,
            otpProtector.Hash(code),
            OtpPurpose.SignIn,
            now,
            TimeSpan.FromMinutes(_options.OtpLifetimeMinutes),
            _options.OtpMaxAttempts);

        await store.AddOtpChallengeAsync(challenge, cancellationToken);
        await store.AddAuditLogAsync(
            AuditLog.Record(null, AuditActions.OtpRequested, now, command.IpAddress, command.UserAgent),
            cancellationToken);
        await otpSender.SendSignInCodeAsync(phone.E164, code, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);

        logger.LogInformation("OTP requested for {MaskedPhone}", phone.Masked);
        return new OtpDispatchResult(challenge.Id, challenge.ExpiresAtUtc, _options.ExposeOtpInResponse ? code : null);
    }

    public async Task<AuthSessionResult> VerifyOtpAsync(VerifyOtpCommand command, CancellationToken cancellationToken)
    {
        var phone = PhoneNumber.ParseIndianMobile(command.PhoneNumber);
        var now = clock.UtcNow;
        var submittedCode = command.Code?.Trim() ?? string.Empty;
        var challenge = await store.FindLatestOtpChallengeAsync(phone.E164, OtpPurpose.SignIn, cancellationToken);
        if (challenge is null || !challenge.IsActive(now) || !otpProtector.Verify(submittedCode, challenge.CodeHash))
        {
            if (challenge is not null && challenge.IsActive(now))
            {
                challenge.RegisterFailedAttempt();
                await store.SaveChangesAsync(cancellationToken);
            }

            throw DomainException.Validation("otp_invalid", "The code is invalid or has expired.");
        }

        challenge.Consume(now);
        var account = await store.FindAccountByPhoneAsync(phone.E164, cancellationToken);
        if (account?.DeletionRequestedAtUtc is not null)
        {
            await store.SaveChangesAsync(cancellationToken);
            throw DomainException.Validation(
                "account_deletion_pending",
                "This account is pending deletion and cannot be used.");
        }

        if (account?.SuspendedAtUtc is not null)
        {
            await store.SaveChangesAsync(cancellationToken);
            throw DomainException.Validation(
                "account_suspended",
                "This account is suspended. Contact ProDoc support.");
        }

        var isNewAccount = account is null;
        account ??= await store.CreatePatientAccountAsync(phone.E164, RolesFor(phone), now, cancellationToken);
        var session = await IssueSessionAsync(account, Guid.NewGuid(), command.DeviceName, now, cancellationToken);
        await store.AddAuditLogAsync(
            AuditLog.Record(account.UserId, AuditActions.SignedIn, now, command.IpAddress, command.UserAgent),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        if (isNewAccount && growth is not null)
        {
            await growth.AttributeReferralOnSignupAsync(
                account.UserId,
                command.ReferralCode,
                new RequestMetadata(command.IpAddress, command.UserAgent),
                cancellationToken);
        }

        logger.LogInformation("Signed in user {UserId}", account.UserId);
        return session.Result;
    }

    public async Task<AuthSessionResult> RefreshAsync(RefreshSessionCommand command, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var existing = await FindPresentedTokenAsync(command.RefreshToken, cancellationToken);
        switch (RefreshTokenRotation.Evaluate(existing, now))
        {
            case RefreshTokenDecision.Reject:
                throw DomainException.Unauthorized("refresh_invalid", "The session is no longer valid. Sign in again.");
            case RefreshTokenDecision.RevokeFamily:
                await store.RevokeFamilyAsync(existing!.FamilyId, now, cancellationToken);
                await store.AddAuditLogAsync(
                    AuditLog.Record(existing.UserId, AuditActions.RefreshReuseDetected, now, command.IpAddress, command.UserAgent),
                    cancellationToken);
                await store.SaveChangesAsync(cancellationToken);
                logger.LogWarning("Refresh token reuse detected for user {UserId}", existing.UserId);
                throw DomainException.Unauthorized("refresh_reuse", "This session was revoked. Sign in again.");
        }

        var account = await store.FindAccountByUserIdAsync(existing!.UserId, cancellationToken)
            ?? throw DomainException.Unauthorized("refresh_invalid", "The session is no longer valid. Sign in again.");
        if (account.DeletionRequestedAtUtc is not null)
        {
            throw DomainException.Validation(
                "account_deletion_pending",
                "This account is pending deletion and cannot be used.");
        }

        if (account.SuspendedAtUtc is not null)
        {
            throw DomainException.Validation(
                "account_suspended",
                "This account is suspended. Contact ProDoc support.");
        }

        var session = await IssueSessionAsync(account, existing.FamilyId, existing.DeviceName, now, cancellationToken);
        existing.Revoke(now, session.RefreshTokenId);
        await store.AddAuditLogAsync(
            AuditLog.Record(account.UserId, AuditActions.SessionRefreshed, now, command.IpAddress, command.UserAgent),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return session.Result;
    }

    public async Task LogoutAsync(LogoutCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return;
        }

        var existing = await store.FindRefreshTokenByHashAsync(refreshTokens.Hash(command.RefreshToken.Trim()), cancellationToken);
        if (existing is null || existing.RevokedAtUtc is not null)
        {
            return;
        }

        var now = clock.UtcNow;
        existing.Revoke(now, null);
        await store.AddAuditLogAsync(
            AuditLog.Record(existing.UserId, AuditActions.SignedOut, now, command.IpAddress, command.UserAgent),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task LogoutAllAsync(Guid userId, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        await store.RevokeAllForUserAsync(userId, now, cancellationToken);
        await store.AddAuditLogAsync(
            AuditLog.Record(userId, AuditActions.SignedOutAll, now, ipAddress, userAgent),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    private async Task<IssuedSession> IssueSessionAsync(
        AccountSnapshot account,
        Guid familyId,
        string? deviceName,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var issued = refreshTokens.Create();
        var refresh = RefreshToken.Issue(
            account.UserId,
            familyId,
            issued.Hash,
            now,
            now.AddDays(_options.RefreshTokenDays),
            NormalizeDeviceName(deviceName));
        await store.AddRefreshTokenAsync(refresh, cancellationToken);
        var access = accessTokens.Issue(account, now);
        var result = new AuthSessionResult(
            access.Value,
            access.ExpiresAtUtc,
            issued.Plaintext,
            refresh.ExpiresAtUtc,
            account);
        return new IssuedSession(result, refresh.Id);
    }

    private async Task<RefreshToken?> FindPresentedTokenAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        return await store.FindRefreshTokenByHashAsync(refreshTokens.Hash(refreshToken.Trim()), cancellationToken);
    }

    private IReadOnlyCollection<string> RolesFor(PhoneNumber phone)
    {
        var roles = new List<string> { RoleNames.Patient };
        if (_options.DevelopmentAdminPhones.Any(candidate => PhonesMatch(candidate, phone)))
        {
            roles.Add(RoleNames.PlatformAdmin);
        }

        return roles;
    }

    private static bool PhonesMatch(string configured, PhoneNumber phone)
    {
        try
        {
            return PhoneNumber.ParseIndianMobile(configured).E164 == phone.E164;
        }
        catch (DomainException)
        {
            return false;
        }
    }

    private static string? NormalizeDeviceName(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return null;
        }

        var trimmed = deviceName.Trim();
        if (trimmed.Length > RefreshToken.MaxDeviceNameLength)
        {
            throw DomainException.Validation("device_name_invalid", "Device name must be 80 characters or fewer.");
        }

        return trimmed;
    }

    private readonly record struct IssuedSession(AuthSessionResult Result, Guid RefreshTokenId);
}
