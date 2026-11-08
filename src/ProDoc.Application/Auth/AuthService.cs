using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Admin;
using ProDoc.Application.Growth;
using ProDoc.Application.Providers;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;

namespace ProDoc.Application.Auth;

public sealed class AuthService(
    IAuthStore store,
    IStaffInvitationStore invitations,
    StaffInvitationService staffInvitations,
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

    public async Task<AuthOtpRequestResult> RequestOtpAsync(RequestOtpCommand command, CancellationToken cancellationToken)
    {
        if (!PhoneNumber.TryParseIndianMobile(command.PhoneNumber, out var phone))
        {
            return AuthOtpRequestResult.Fail("phone_invalid", "Enter a valid Indian mobile number.");
        }

        var now = clock.UtcNow;
        var purposeOutcome = await ResolveRequestPurposeAsync(phone, command.Purpose, now, cancellationToken);
        if (purposeOutcome.Failure is not null)
        {
            return AuthOtpRequestResult.Fail(purposeOutcome.Failure);
        }

        var purpose = purposeOutcome.Purpose;

        var since = now.AddMinutes(-_options.OtpRequestWindowMinutes);
        var recent = await store.CountRecentOtpChallengesAsync(phone.E164, since, cancellationToken);
        if (recent >= _options.OtpMaxRequestsPerWindow)
        {
            return AuthOtpRequestResult.Fail(
                "otp_rate_limited",
                "Too many codes were requested. Wait a few minutes and try again.",
                statusCode: 429);
        }

        var latest = await store.FindLatestOtpChallengeAsync(phone.E164, purpose, cancellationToken);
        if (latest is not null && latest.CreatedAtUtc.AddSeconds(_options.OtpResendCooldownSeconds) > now)
        {
            return AuthOtpRequestResult.Fail(
                "otp_cooldown",
                "A code was just sent. Wait a moment before requesting another.",
                statusCode: 429);
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var challenge = OtpChallenge.Issue(
            phone.E164,
            otpProtector.Hash(code),
            purpose,
            now,
            TimeSpan.FromMinutes(_options.OtpLifetimeMinutes),
            _options.OtpMaxAttempts);

        await store.AddOtpChallengeAsync(challenge, cancellationToken);
        await store.AddAuditLogAsync(
            AuditLog.Record(null, AuditActions.OtpRequested, now, command.IpAddress, command.UserAgent),
            cancellationToken);
        // Persist before send so a delivered SMS is never unverifiable after a DB failure.
        await store.SaveChangesAsync(cancellationToken);

        try
        {
            await otpSender.SendSignInCodeAsync(phone.E164, code, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "OTP delivery failed for {MaskedPhone}", phone.Masked);
            return AuthOtpRequestResult.Fail(
                "otp_delivery_failed",
                "We could not send the code. Wait a moment and try again.");
        }

        logger.LogInformation("OTP requested for {MaskedPhone} purpose {Purpose}", phone.Masked, purpose);
        return AuthOtpRequestResult.Ok(new OtpDispatchResult(
            challenge.Id,
            challenge.ExpiresAtUtc,
            _options.ExposeOtpInResponse ? code : null,
            purpose.ToString()));
    }

    public async Task<AuthVerifyResult> VerifyOtpAsync(VerifyOtpCommand command, CancellationToken cancellationToken)
    {
        if (!PhoneNumber.TryParseIndianMobile(command.PhoneNumber, out var phone))
        {
            return AuthVerifyResult.Fail("phone_invalid", "Enter a valid Indian mobile number.");
        }

        var now = clock.UtcNow;
        var purposeOutcome = await ResolveVerifyPurposeAsync(phone, command.Purpose, now, cancellationToken);
        if (purposeOutcome.Failure is not null)
        {
            return AuthVerifyResult.Fail(purposeOutcome.Failure);
        }

        var purpose = purposeOutcome.Purpose;
        var submittedCode = command.Code?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(submittedCode) || submittedCode.Length != 6 || !submittedCode.All(char.IsDigit))
        {
            return AuthVerifyResult.Fail("otp_format", "Enter the 6-digit code.");
        }

        var challenge = await store.FindLatestOtpChallengeAsync(phone.E164, purpose, cancellationToken);
        if (challenge is null)
        {
            return AuthVerifyResult.Fail("otp_invalid", "Incorrect OTP. Please try again.");
        }

        if (challenge.ConsumedAtUtc is not null)
        {
            return AuthVerifyResult.Fail("otp_used", "This code was already used. Request a new one.");
        }

        if (challenge.ExpiresAtUtc <= now)
        {
            return AuthVerifyResult.Fail("otp_expired", "This code has expired. Request a new one.");
        }

        if (challenge.AttemptCount >= challenge.MaxAttempts)
        {
            return AuthVerifyResult.Fail(
                "otp_attempts_exceeded",
                "Too many incorrect attempts. Request a new code.",
                statusCode: 429);
        }

        if (!otpProtector.Verify(submittedCode, challenge.CodeHash))
        {
            challenge.RegisterFailedAttempt();
            await store.SaveChangesAsync(cancellationToken);
            if (challenge.AttemptCount >= challenge.MaxAttempts)
            {
                return AuthVerifyResult.Fail(
                    "otp_attempts_exceeded",
                    "Too many incorrect attempts. Request a new code.",
                    statusCode: 429);
            }

            return AuthVerifyResult.Fail("otp_invalid", "Incorrect OTP. Please try again.");
        }

        challenge.Consume(now);

        AccountSnapshot account;
        var isNewAccount = false;

        if (purpose == OtpPurpose.StaffInvite)
        {
            try
            {
                account = await staffInvitations.AcceptPendingInviteAsync(phone.E164, now, cancellationToken);
            }
            catch (DomainException exception)
            {
                // Invite acceptance still uses DomainException in StaffInvitationService — translate without rethrowing.
                return AuthVerifyResult.Fail(exception);
            }
        }
        else if (purpose == OtpPurpose.AdminSignIn)
        {
            if (!IsAuthorizedSuperAdmin(phone))
            {
                return AuthVerifyResult.Fail(
                    "admin_login_denied",
                    "Super Admin access is restricted.",
                    statusCode: 401);
            }

            account = await EnsureSuperAdminAccountAsync(phone.E164, now, cancellationToken);
            if (InactiveAccountFailure(account) is { } inactive)
            {
                return AuthVerifyResult.Fail(inactive);
            }
        }
        else
        {
            var existing = await store.FindAccountByPhoneAsync(phone.E164, cancellationToken);
            if (existing is null)
            {
                isNewAccount = true;
                // Patient path never grants PlatformAdmin — Super Admin is allowlist + admin OTP only.
                account = await store.CreatePatientAccountAsync(phone.E164, [RoleNames.Patient], now, cancellationToken);
            }
            else
            {
                if (InactiveAccountFailure(existing) is { } inactive)
                {
                    return AuthVerifyResult.Fail(inactive);
                }

                account = existing;
            }
        }

        var deviceName = TryNormalizeDeviceName(command.DeviceName, out var deviceFailure);
        if (deviceFailure is not null)
        {
            return AuthVerifyResult.Fail(deviceFailure);
        }

        var session = await IssueSessionAsync(account, Guid.NewGuid(), deviceName, now, cancellationToken);
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

        logger.LogInformation("Signed in user {UserId} surface {Surface}", account.UserId, account.HomeSurface);
        return AuthVerifyResult.Ok(session.Result);
    }

    public async Task<AuthRefreshResult> RefreshAsync(RefreshSessionCommand command, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var existing = await FindPresentedTokenAsync(command.RefreshToken, cancellationToken);
        switch (RefreshTokenRotation.Evaluate(existing, now))
        {
            case RefreshTokenDecision.Reject:
                return AuthRefreshResult.Fail(
                    "refresh_invalid",
                    "The session is no longer valid. Sign in again.");
            case RefreshTokenDecision.RevokeFamily:
                await store.RevokeFamilyAsync(existing!.FamilyId, now, cancellationToken);
                await store.AddAuditLogAsync(
                    AuditLog.Record(existing.UserId, AuditActions.RefreshReuseDetected, now, command.IpAddress, command.UserAgent),
                    cancellationToken);
                await store.SaveChangesAsync(cancellationToken);
                logger.LogWarning("Refresh token reuse detected for user {UserId}", existing.UserId);
                return AuthRefreshResult.Fail(
                    "refresh_reuse",
                    "This session was revoked. Sign in again.");
        }

        var account = await store.FindAccountByUserIdAsync(existing!.UserId, cancellationToken);
        if (account is null)
        {
            return AuthRefreshResult.Fail(
                "refresh_invalid",
                "The session is no longer valid. Sign in again.");
        }

        if (InactiveAccountFailure(account) is { } inactive)
        {
            return AuthRefreshResult.Fail(inactive.Code, inactive.Message, inactive.StatusCode);
        }

        var session = await IssueSessionAsync(account, existing.FamilyId, existing.DeviceName, now, cancellationToken);
        existing.Revoke(now, session.RefreshTokenId);
        await store.AddAuditLogAsync(
            AuditLog.Record(account.UserId, AuditActions.SessionRefreshed, now, command.IpAddress, command.UserAgent),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return AuthRefreshResult.Ok(session.Result);
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

    private async Task<PurposeOutcome> ResolveRequestPurposeAsync(
        PhoneNumber phone,
        string? requested,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var normalized = (requested ?? "patient").Trim().ToLowerInvariant();
        if (normalized is "admin" or "adminsignin" or "admin_sign_in")
        {
            // Backend allowlist only — never trust client role selection or public registration.
            if (!IsAuthorizedSuperAdmin(phone))
            {
                return PurposeOutcome.Fail(
                    "admin_login_denied",
                    "Super Admin access is restricted.",
                    statusCode: 401);
            }

            var account = await store.FindAccountByPhoneAsync(phone.E164, cancellationToken);
            if (account is not null && InactiveAccountFailure(account) is { } inactive)
            {
                return PurposeOutcome.Fail(inactive);
            }

            return PurposeOutcome.Ok(OtpPurpose.AdminSignIn);
        }

        var pending = await invitations.FindPendingByPhoneAsync(phone.E164, now, cancellationToken);
        if (pending is not null)
        {
            return PurposeOutcome.Ok(OtpPurpose.StaffInvite);
        }

        return PurposeOutcome.Ok(OtpPurpose.SignIn);
    }

    private async Task<PurposeOutcome> ResolveVerifyPurposeAsync(
        PhoneNumber phone,
        string? requested,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var normalized = (requested ?? "patient").Trim().ToLowerInvariant();
        if (normalized is "admin" or "adminsignin" or "admin_sign_in")
        {
            if (!IsAuthorizedSuperAdmin(phone))
            {
                return PurposeOutcome.Fail(
                    "admin_login_denied",
                    "Super Admin access is restricted.",
                    statusCode: 401);
            }

            return PurposeOutcome.Ok(OtpPurpose.AdminSignIn);
        }

        if (normalized is "staff" or "staffinvite" or "staff_invite" or "invite")
        {
            return PurposeOutcome.Ok(OtpPurpose.StaffInvite);
        }

        var pending = await invitations.FindPendingByPhoneAsync(phone.E164, now, cancellationToken);
        return PurposeOutcome.Ok(pending is not null ? OtpPurpose.StaffInvite : OtpPurpose.SignIn);
    }

    private async Task<AccountSnapshot> EnsureSuperAdminAccountAsync(
        string phoneE164,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var account = await store.FindAccountByPhoneAsync(phoneE164, cancellationToken);
        if (account is null)
        {
            return await store.CreatePatientAccountAsync(
                phoneE164,
                [RoleNames.Patient, RoleNames.PlatformAdmin],
                now,
                cancellationToken);
        }

        if (!account.Roles.Contains(RoleNames.PlatformAdmin, StringComparer.Ordinal))
        {
            await store.GrantRoleAsync(account.UserId, RoleNames.PlatformAdmin, cancellationToken);
            // Persist before reloading — IssueSessionAsync re-reads the account for JWT claims,
            // and AsNoTracking queries will miss a staged UserRole until SaveChanges.
            await store.SaveChangesAsync(cancellationToken);
            account = await store.FindAccountByUserIdAsync(account.UserId, cancellationToken) ?? account;
        }

        return account;
    }

    private bool IsAuthorizedSuperAdmin(PhoneNumber phone) =>
        AuthorizedSuperAdminPhones().Any(candidate => PhonesMatch(candidate, phone));

    private IEnumerable<string> AuthorizedSuperAdminPhones()
    {
        foreach (var phone in _options.SuperAdminPhones)
        {
            if (!string.IsNullOrWhiteSpace(phone))
            {
                yield return phone;
            }
        }

        // DevelopmentAdminPhones is validated empty outside Development.
        foreach (var phone in _options.DevelopmentAdminPhones)
        {
            if (!string.IsNullOrWhiteSpace(phone))
            {
                yield return phone;
            }
        }
    }

    private static AuthFailure? InactiveAccountFailure(AccountSnapshot account)
    {
        if (account.DeletionRequestedAtUtc is not null)
        {
            return new AuthFailure(
                "account_deletion_pending",
                "This account is pending deletion and cannot be used.",
                400);
        }

        if (account.SuspendedAtUtc is not null)
        {
            return new AuthFailure(
                "account_suspended",
                "This account is suspended. Contact ProDoc support.",
                400);
        }

        return null;
    }

    private async Task<IssuedSession> IssueSessionAsync(
        AccountSnapshot account,
        Guid familyId,
        string? deviceName,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Refresh snapshot so HomeSurface / roles are current after invite acceptance.
        account = await store.FindAccountByUserIdAsync(account.UserId, cancellationToken) ?? account;
        var issued = refreshTokens.Create();
        var refresh = RefreshToken.Issue(
            account.UserId,
            familyId,
            issued.Hash,
            now,
            now.AddDays(_options.RefreshTokenDays),
            deviceName);
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

    private static bool PhonesMatch(string configured, PhoneNumber phone) =>
        PhoneNumber.TryParseIndianMobile(configured, out var candidate) && candidate.E164 == phone.E164;

    private static string? TryNormalizeDeviceName(string? deviceName, out AuthFailure? failure)
    {
        failure = null;
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return null;
        }

        var trimmed = deviceName.Trim();
        if (trimmed.Length > RefreshToken.MaxDeviceNameLength)
        {
            failure = new AuthFailure("device_name_invalid", "Device name must be 80 characters or fewer.", 400);
            return null;
        }

        return trimmed;
    }

    private readonly record struct IssuedSession(AuthSessionResult Result, Guid RefreshTokenId);

    private readonly record struct PurposeOutcome(OtpPurpose Purpose, AuthFailure? Failure)
    {
        public static PurposeOutcome Ok(OtpPurpose purpose) => new(purpose, null);

        public static PurposeOutcome Fail(string code, string message, int statusCode = 400) =>
            new(default, new AuthFailure(code, message, statusCode));

        public static PurposeOutcome Fail(AuthFailure failure) => new(default, failure);
    }
}
