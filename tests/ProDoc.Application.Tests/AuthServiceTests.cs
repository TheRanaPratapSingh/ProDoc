using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Admin;
using ProDoc.Application.Auth;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Patients;

namespace ProDoc.Application.Tests;

public class AuthServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Request_returns_a_code_only_when_explicitly_exposed()
    {
        var harness = CreateHarness(exposeCode: false);

        var hidden = await harness.Service.RequestOtpAsync(new RequestOtpCommand("9876543210", null, null), CancellationToken.None);

        Assert.True(hidden.Succeeded);
        Assert.Null(hidden.Dispatch!.DevelopmentCode);
        Assert.Single(harness.Sender.Sent);

        harness.Clock.UtcNow = Now.AddSeconds(31);
        harness.Options.ExposeOtpInResponse = true;
        var shown = await harness.Service.RequestOtpAsync(new RequestOtpCommand("9876543210", null, null), CancellationToken.None);

        Assert.True(shown.Succeeded);
        Assert.Equal(6, shown.Dispatch!.DevelopmentCode!.Length);
    }

    [Fact]
    public async Task Second_request_inside_the_cooldown_is_rejected()
    {
        var harness = CreateHarness(exposeCode: true);
        await harness.Service.RequestOtpAsync(new RequestOtpCommand("9876543210", null, null), CancellationToken.None);

        var rejected = await harness.Service.RequestOtpAsync(new RequestOtpCommand("9876543210", null, null), CancellationToken.None);

        Assert.False(rejected.Succeeded);
        Assert.Equal("otp_cooldown", rejected.Failure!.Code);
        Assert.Equal(429, rejected.Failure.StatusCode);
    }

    [Fact]
    public async Task Wrong_code_consumes_an_attempt_and_the_right_code_signs_in()
    {
        var harness = CreateHarness(exposeCode: true);
        await harness.Service.RequestOtpAsync(new RequestOtpCommand("9876543210", null, null), CancellationToken.None);
        var code = harness.Sender.Sent[0].Code;

        var wrong = await harness.Service.VerifyOtpAsync(
            new VerifyOtpCommand("9876543210", "000000", "phone", null, null),
            CancellationToken.None);
        Assert.False(wrong.Succeeded);
        Assert.Equal("otp_invalid", wrong.Failure!.Code);
        Assert.Equal("Incorrect OTP. Please try again.", wrong.Failure.Message);
        Assert.Equal(400, wrong.Failure.StatusCode);
        Assert.Equal(1, harness.Store.Challenges[0].AttemptCount);

        var session = await harness.Service.VerifyOtpAsync(
            new VerifyOtpCommand("9876543210", code, "phone", null, null),
            CancellationToken.None);

        Assert.True(session.Succeeded);
        Assert.Contains(RoleNames.Patient, session.Session!.Account.Roles);
        Assert.Contains(PermissionCodes.AccountRead, session.Session.Account.Permissions);
        Assert.StartsWith("access-", session.Session.AccessToken, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Expired_code_returns_otp_expired_without_throwing()
    {
        var harness = CreateHarness(exposeCode: true);
        await harness.Service.RequestOtpAsync(new RequestOtpCommand("9876543210", null, null), CancellationToken.None);
        var code = harness.Sender.Sent[0].Code;
        harness.Clock.UtcNow = Now.AddMinutes(30);

        var expired = await harness.Service.VerifyOtpAsync(
            new VerifyOtpCommand("9876543210", code, "phone", null, null),
            CancellationToken.None);

        Assert.False(expired.Succeeded);
        Assert.Equal("otp_expired", expired.Failure!.Code);
        Assert.Equal(400, expired.Failure.StatusCode);
    }

    [Fact]
    public async Task Too_many_wrong_attempts_returns_rate_limit_failure()
    {
        var harness = CreateHarness(exposeCode: true);
        await harness.Service.RequestOtpAsync(new RequestOtpCommand("9876543210", null, null), CancellationToken.None);
        AuthVerifyResult? last = null;
        for (var i = 0; i < 10; i++)
        {
            last = await harness.Service.VerifyOtpAsync(
                new VerifyOtpCommand("9876543210", "000000", "phone", null, null),
                CancellationToken.None);
            if (last.Failure?.Code == "otp_attempts_exceeded")
            {
                break;
            }
        }

        Assert.NotNull(last);
        Assert.False(last!.Succeeded);
        Assert.Equal("otp_attempts_exceeded", last.Failure!.Code);
        Assert.Equal(429, last.Failure.StatusCode);
    }

    [Fact]
    public async Task Reused_refresh_token_revokes_the_replacement()
    {
        var harness = CreateHarness(exposeCode: true);
        var session = await SignInAsync(harness);
        var rotated = await harness.Service.RefreshAsync(new RefreshSessionCommand(session.RefreshToken, null, null), CancellationToken.None);
        Assert.True(rotated.Succeeded);

        var reuse = await harness.Service.RefreshAsync(new RefreshSessionCommand(session.RefreshToken, null, null), CancellationToken.None);
        var replacement = await harness.Service.RefreshAsync(
            new RefreshSessionCommand(rotated.Session!.RefreshToken, null, null),
            CancellationToken.None);

        Assert.False(reuse.Succeeded);
        Assert.False(replacement.Succeeded);
        Assert.Equal("refresh_reuse", reuse.Failure!.Code);
        Assert.Equal("refresh_reuse", replacement.Failure!.Code);
    }

    [Fact]
    public async Task Patient_login_does_not_grant_platform_admin_even_for_allowlisted_phone()
    {
        var harness = CreateHarness(exposeCode: true);
        harness.Options.SuperAdminPhones = ["8800834188"];

        await harness.Service.RequestOtpAsync(new RequestOtpCommand("8800834188", null, null, "patient"), CancellationToken.None);
        var session = await RequireSessionAsync(harness.Service.VerifyOtpAsync(
            new VerifyOtpCommand("8800834188", harness.Sender.Sent[^1].Code, "phone", null, null, null, "patient"),
            CancellationToken.None));

        Assert.Contains(RoleNames.Patient, session.Account.Roles);
        Assert.DoesNotContain(RoleNames.PlatformAdmin, session.Account.Roles);
        Assert.Equal("patient", session.Account.HomeSurface);
    }

    [Fact]
    public async Task Super_admin_allowlisted_phone_can_sign_in_via_admin_otp()
    {
        var harness = CreateHarness(exposeCode: true);
        harness.Options.SuperAdminPhones = ["8800834188"];

        await harness.Service.RequestOtpAsync(new RequestOtpCommand("8800834188", null, null, "admin"), CancellationToken.None);
        Assert.Equal(OtpPurpose.AdminSignIn, harness.Store.Challenges[^1].Purpose);

        var session = await RequireSessionAsync(harness.Service.VerifyOtpAsync(
            new VerifyOtpCommand("8800834188", harness.Sender.Sent[^1].Code, "phone", null, null, null, "admin"),
            CancellationToken.None));

        Assert.Contains(RoleNames.PlatformAdmin, session.Account.Roles);
        Assert.Contains(PermissionCodes.AdminAccess, session.Account.Permissions);
        Assert.Equal("admin", session.Account.HomeSurface);
    }

    [Fact]
    public async Task Admin_otp_upgrades_existing_patient_account_to_platform_admin()
    {
        var harness = CreateHarness(exposeCode: true);
        harness.Options.SuperAdminPhones = ["8800834188"];

        await harness.Service.RequestOtpAsync(new RequestOtpCommand("8800834188", null, null, "patient"), CancellationToken.None);
        var patientSession = await RequireSessionAsync(harness.Service.VerifyOtpAsync(
            new VerifyOtpCommand("8800834188", harness.Sender.Sent[^1].Code, "phone", null, null, null, "patient"),
            CancellationToken.None));
        Assert.DoesNotContain(RoleNames.PlatformAdmin, patientSession.Account.Roles);

        await harness.Service.RequestOtpAsync(new RequestOtpCommand("8800834188", null, null, "admin"), CancellationToken.None);
        var adminSession = await RequireSessionAsync(harness.Service.VerifyOtpAsync(
            new VerifyOtpCommand("8800834188", harness.Sender.Sent[^1].Code, "phone", null, null, null, "admin"),
            CancellationToken.None));

        Assert.Equal(patientSession.Account.UserId, adminSession.Account.UserId);
        Assert.Contains(RoleNames.PlatformAdmin, adminSession.Account.Roles);
        Assert.Contains(PermissionCodes.AdminAccess, adminSession.Account.Permissions);
        Assert.Equal("admin", adminSession.Account.HomeSurface);
    }

    [Fact]
    public async Task Admin_purpose_is_denied_for_non_allowlisted_phones()
    {
        var harness = CreateHarness(exposeCode: true);
        harness.Options.SuperAdminPhones = ["8800834188"];
        var denied = await harness.Service.RequestOtpAsync(
            new RequestOtpCommand("9876543210", null, null, "admin"),
            CancellationToken.None);
        Assert.False(denied.Succeeded);
        Assert.Equal("admin_login_denied", denied.Failure!.Code);
    }

    [Fact]
    public async Task Staff_invite_otp_activates_role_and_redirect_surface()
    {
        var harness = CreateHarness(exposeCode: true);
        var invitations = harness.Invitations;
        var invite = StaffInvitation.Create(
            "+919876543210",
            "Dr Mehta",
            null,
            RoleNames.Doctor,
            null,
            null,
            Guid.NewGuid(),
            Now,
            TimeSpan.FromHours(48));
        await invitations.AddAsync(invite, CancellationToken.None);

        await harness.Service.RequestOtpAsync(new RequestOtpCommand("9876543210", null, null), CancellationToken.None);
        Assert.Equal(OtpPurpose.StaffInvite, harness.Store.Challenges[^1].Purpose);

        var session = await RequireSessionAsync(harness.Service.VerifyOtpAsync(
            new VerifyOtpCommand("9876543210", harness.Sender.Sent[^1].Code, "phone", null, null),
            CancellationToken.None));

        Assert.Contains(RoleNames.Doctor, session.Account.Roles);
        Assert.Equal("doctor", session.Account.HomeSurface);
        Assert.Equal(StaffInvitationStatus.Accepted, invite.Status);
    }

    [Fact]
    public async Task Deletion_revokes_sessions_and_blocks_another_sign_in()
    {
        var harness = CreateHarness(exposeCode: true);
        var session = await SignInAsync(harness);
        var deletion = await harness.Accounts.RequestDeletionAsync(
            new RequestAccountDeletionCommand(session.Account.UserId, null, null),
            CancellationToken.None);

        Assert.Equal(Now, deletion.RequestedAtUtc);
        var blockedRefresh = await harness.Service.RefreshAsync(
            new RefreshSessionCommand(session.RefreshToken, null, null),
            CancellationToken.None);
        Assert.False(blockedRefresh.Succeeded);
        // Deletion revokes all refresh tokens first — expected auth failure without throwing.
        Assert.Contains(blockedRefresh.Failure!.Code, new[] { "refresh_invalid", "refresh_reuse", "account_deletion_pending" });

        harness.Clock.UtcNow = Now.AddMinutes(1);
        await harness.Service.RequestOtpAsync(new RequestOtpCommand("9876543210", null, null), CancellationToken.None);
        var blocked = await harness.Service.VerifyOtpAsync(
            new VerifyOtpCommand("9876543210", harness.Sender.Sent[^1].Code, null, null, null),
            CancellationToken.None);
        Assert.False(blocked.Succeeded);
        Assert.Equal("account_deletion_pending", blocked.Failure!.Code);
    }

    private static async Task<AuthSessionResult> RequireSessionAsync(Task<AuthVerifyResult> task)
    {
        var result = await task;
        Assert.True(result.Succeeded, result.Failure?.Message ?? "OTP verify failed");
        return result.Session!;
    }

    private static async Task<AuthSessionResult> SignInAsync(AuthHarness harness)
    {
        await harness.Service.RequestOtpAsync(new RequestOtpCommand("9876543210", null, null), CancellationToken.None);
        return await RequireSessionAsync(harness.Service.VerifyOtpAsync(
            new VerifyOtpCommand("9876543210", harness.Sender.Sent[^1].Code, "phone", null, null),
            CancellationToken.None));
    }

    private static AuthHarness CreateHarness(bool exposeCode)
    {
        var options = new SessionOptions { ExposeOtpInResponse = exposeCode, OtpResendCooldownSeconds = 30 };
        var clock = new FakeClock(Now);
        var store = new InMemoryAuthStore();
        var invitations = new InMemoryStaffInvitationStore();
        var sender = new CapturingOtpSender();
        var staffInvitations = new StaffInvitationService(
            invitations,
            store,
            new PlainOtpProtector(),
            sender,
            clock,
            Options.Create(options),
            NullLogger<StaffInvitationService>.Instance);
        var service = new AuthService(
            store,
            invitations,
            staffInvitations,
            new PlainOtpProtector(),
            sender,
            new SequentialRefreshTokenFactory(),
            new FakeAccessTokenIssuer(),
            clock,
            Options.Create(options),
            NullLogger<AuthService>.Instance);
        var accounts = new AccountService(store, clock, NullLogger<AccountService>.Instance);
        return new AuthHarness(service, accounts, store, invitations, sender, clock, options);
    }

    private sealed record AuthHarness(
        AuthService Service,
        AccountService Accounts,
        InMemoryAuthStore Store,
        InMemoryStaffInvitationStore Invitations,
        CapturingOtpSender Sender,
        FakeClock Clock,
        SessionOptions Options);
}

internal sealed class FakeClock(DateTimeOffset start) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = start;
}

internal sealed class PlainOtpProtector : IOtpCodeProtector
{
    public string Hash(string code) => "hash:" + code;

    public bool Verify(string code, string hash) => hash == Hash(code);
}

internal sealed class CapturingOtpSender : IOtpSender
{
    public List<(string Phone, string Code)> Sent { get; } = [];

    public Task SendSignInCodeAsync(string phoneE164, string code, CancellationToken cancellationToken)
    {
        Sent.Add((phoneE164, code));
        return Task.CompletedTask;
    }
}

internal sealed class SequentialRefreshTokenFactory : IRefreshTokenFactory
{
    private int _count;

    public IssuedRefreshToken Create()
    {
        var plaintext = $"refresh-{++_count}";
        return new IssuedRefreshToken(plaintext, Hash(plaintext));
    }

    public string Hash(string plaintext) => "hash:" + plaintext;
}

internal sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
{
    public AccessToken Issue(AccountSnapshot account, DateTimeOffset now) =>
        new($"access-{account.UserId:N}", now.AddMinutes(15));
}

internal sealed class InMemoryAuthStore : IAuthStore
{
    public List<OtpChallenge> Challenges { get; } = [];

    public List<RefreshToken> RefreshTokens { get; } = [];

    public List<AccountSnapshot> Accounts { get; } = [];

    public List<AuditLog> Audits { get; } = [];

    public Task<int> CountRecentOtpChallengesAsync(string phoneE164, DateTimeOffset sinceUtc, CancellationToken cancellationToken) =>
        Task.FromResult(Challenges.Count(challenge => challenge.PhoneE164 == phoneE164 && challenge.CreatedAtUtc >= sinceUtc));

    public Task AddOtpChallengeAsync(OtpChallenge challenge, CancellationToken cancellationToken)
    {
        Challenges.Add(challenge);
        return Task.CompletedTask;
    }

    public Task<OtpChallenge?> FindLatestOtpChallengeAsync(string phoneE164, OtpPurpose purpose, CancellationToken cancellationToken) =>
        Task.FromResult(Challenges
            .Where(challenge => challenge.PhoneE164 == phoneE164 && challenge.Purpose == purpose)
            .OrderByDescending(challenge => challenge.CreatedAtUtc)
            .FirstOrDefault());

    public Task<AccountSnapshot?> FindAccountByPhoneAsync(string phoneE164, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.SingleOrDefault(account => account.PhoneNumber == phoneE164));

    public Task<AccountSnapshot?> FindAccountByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.SingleOrDefault(account => account.UserId == userId));

    public Task<AccountSnapshot> CreatePatientAccountAsync(
        string phoneE164,
        IReadOnlyCollection<string> roles,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var distinctRoles = roles.Distinct(StringComparer.Ordinal).ToArray();
        var account = new AccountSnapshot(
            Guid.NewGuid(),
            phoneE164,
            null,
            Languages.English,
            distinctRoles,
            distinctRoles.Where(role => PermissionCodes.RoleGrants.ContainsKey(role))
                .SelectMany(role => PermissionCodes.RoleGrants[role])
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            null,
            null,
            DashboardResolver.Resolve(distinctRoles),
            AgeYears: null,
            Gender: null,
            ProfileCompleted: false);
        Accounts.Add(account);
        return Task.FromResult(account);
    }

    public Task GrantRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken)
    {
        var account = Require(userId);
        if (account.Roles.Contains(roleName, StringComparer.Ordinal))
        {
            return Task.CompletedTask;
        }

        var roles = account.Roles.Append(roleName).Distinct(StringComparer.Ordinal).ToArray();
        Replace(account with
        {
            Roles = roles,
            Permissions = roles.Where(role => PermissionCodes.RoleGrants.ContainsKey(role))
                .SelectMany(role => PermissionCodes.RoleGrants[role])
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            HomeSurface = DashboardResolver.Resolve(roles),
            AvailableSurfaces = DashboardResolver.ListSurfaces(roles)
        });
        return Task.CompletedTask;
    }

    public Task UpdatePatientProfileAsync(
        Guid userId,
        string? fullName,
        string preferredLanguage,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        int? ageYears = null,
        string? gender = null)
    {
        var account = Require(userId);
        var patient = Patient.Register(userId, now);
        if (account.DeletionRequestedAtUtc is not null)
        {
            patient.RequestDeletion(account.DeletionRequestedAtUtc.Value);
        }

        patient.UpdateProfile(fullName, preferredLanguage, now, ageYears, gender);
        Replace(account with
        {
            FullName = patient.FullName,
            PreferredLanguage = patient.PreferredLanguage,
            AgeYears = patient.AgeYears,
            Gender = patient.Gender,
            ProfileCompleted = patient.ProfileCompleted
        });
        return Task.CompletedTask;
    }

    public Task CompletePatientProfileAsync(
        Guid userId,
        string fullName,
        int ageYears,
        string? gender,
        string preferredLanguage,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var account = Require(userId);
        var patient = Patient.Register(userId, now);
        patient.CompleteBasicProfile(fullName, ageYears, gender, preferredLanguage, now);
        Replace(account with
        {
            FullName = patient.FullName,
            PreferredLanguage = patient.PreferredLanguage,
            AgeYears = patient.AgeYears,
            Gender = patient.Gender,
            ProfileCompleted = patient.ProfileCompleted
        });
        return Task.CompletedTask;
    }

    public Task MarkDeletionRequestedAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var account = Require(userId);
        if (account.DeletionRequestedAtUtc is null)
        {
            Replace(account with { DeletionRequestedAtUtc = now });
        }

        return Task.CompletedTask;
    }

    public Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        RefreshTokens.Add(token);
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> FindRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(RefreshTokens.SingleOrDefault(token => token.TokenHash == tokenHash));

    public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var token in RefreshTokens.Where(token => token.FamilyId == familyId && token.RevokedAtUtc is null))
        {
            token.Revoke(now, null);
        }

        return Task.CompletedTask;
    }

    public Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var token in RefreshTokens.Where(token => token.UserId == userId && token.RevokedAtUtc is null))
        {
            token.Revoke(now, null);
        }

        return Task.CompletedTask;
    }

    public Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        Audits.Add(auditLog);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private AccountSnapshot Require(Guid userId) =>
        Accounts.SingleOrDefault(account => account.UserId == userId)
        ?? throw DomainException.NotFound("account_not_found", "Account was not found.");

    private void Replace(AccountSnapshot replacement)
    {
        var index = Accounts.FindIndex(account => account.UserId == replacement.UserId);
        Accounts[index] = replacement;
    }
}

internal sealed class InMemoryStaffInvitationStore : IStaffInvitationStore
{
    public List<StaffInvitation> Items { get; } = [];

    public Task<StaffInvitation?> FindPendingByPhoneAsync(string phoneE164, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult(Items
            .Where(item => item.PhoneE164 == phoneE164 && item.IsPending(now))
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault());

    public Task<StaffInvitation?> FindByIdAsync(Guid invitationId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(item => item.Id == invitationId));

    public Task<IReadOnlyList<StaffInvitationDetails>> ListAsync(string? status, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StaffInvitationDetails>>(
            Items.Take(take).Select(item => new StaffInvitationDetails(
                item.Id,
                item.PhoneE164,
                item.FullName,
                item.Email,
                item.RoleName,
                item.ClinicId,
                item.BranchId,
                item.Status.ToString(),
                item.CreatedAtUtc,
                item.ExpiresAtUtc,
                item.AcceptedAtUtc)).ToList());

    public Task AddAsync(StaffInvitation invitation, CancellationToken cancellationToken)
    {
        Items.Add(invitation);
        return Task.CompletedTask;
    }

    public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task GrantRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task EnsurePatientProfileAsync(Guid userId, string? fullName, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task AssignClinicStaffAsync(
        Guid clinicId,
        Guid userId,
        Guid? branchId,
        string roleName,
        DateTimeOffset now,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task EnsureDoctorDraftAsync(Guid userId, string? fullName, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task ApplyDoctorInviteProfileAsync(
        Guid userId,
        string? fullName,
        Guid? specializationId,
        int? yearsOfExperience,
        string? qualifications,
        string? registrationNumber,
        DateTimeOffset now,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task AffiliateDoctorToClinicsAsync(
        Guid userId,
        IReadOnlyList<Guid> clinicIds,
        Guid? branchId,
        DateTimeOffset now,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<Guid> ActivateDoctorForPublicDiscoveryAsync(
        Guid userId,
        IReadOnlyList<Guid> clinicIds,
        Guid? branchId,
        DateTimeOffset now,
        CancellationToken cancellationToken) => Task.FromResult(Guid.NewGuid());

    public Task<(Guid DoctorId, string Status, bool IsPubliclyDiscoverable)?> GetDoctorDiscoveryStateAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        Task.FromResult<(Guid DoctorId, string Status, bool IsPubliclyDiscoverable)?>(
            (Guid.NewGuid(), "Approved", true));

    public Task AssignReceptionistDoctorsAsync(
        Guid clinicId,
        Guid receptionistUserId,
        IReadOnlyList<Guid> doctorIds,
        DateTimeOffset now,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task TransferClinicOwnershipAsync(Guid clinicId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<bool> ClinicExistsAsync(Guid clinicId, CancellationToken cancellationToken) => Task.FromResult(true);

    public Task<bool> DoctorExistsAsync(Guid doctorId, CancellationToken cancellationToken) => Task.FromResult(true);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
