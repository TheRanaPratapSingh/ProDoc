using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProDoc.Application.Abstractions;
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

        Assert.Null(hidden.DevelopmentCode);
        Assert.Single(harness.Sender.Sent);

        harness.Clock.UtcNow = Now.AddSeconds(31);
        harness.Options.ExposeOtpInResponse = true;
        var shown = await harness.Service.RequestOtpAsync(new RequestOtpCommand("9876543210", null, null), CancellationToken.None);

        Assert.Equal(6, shown.DevelopmentCode!.Length);
    }

    [Fact]
    public async Task Second_request_inside_the_cooldown_is_rejected()
    {
        var harness = CreateHarness(exposeCode: true);
        await harness.Service.RequestOtpAsync(new RequestOtpCommand("9876543210", null, null), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.RequestOtpAsync(new RequestOtpCommand("9876543210", null, null), CancellationToken.None));

        Assert.Equal("otp_cooldown", exception.Code);
        Assert.Equal(429, exception.StatusCode);
    }

    [Fact]
    public async Task Wrong_code_consumes_an_attempt_and_the_right_code_signs_in()
    {
        var harness = CreateHarness(exposeCode: true);
        await harness.Service.RequestOtpAsync(new RequestOtpCommand("9876543210", null, null), CancellationToken.None);
        var code = harness.Sender.Sent[0].Code;

        await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.VerifyOtpAsync(new VerifyOtpCommand("9876543210", "000000", "phone", null, null), CancellationToken.None));
        Assert.Equal(1, harness.Store.Challenges[0].AttemptCount);

        var session = await harness.Service.VerifyOtpAsync(
            new VerifyOtpCommand("9876543210", code, "phone", null, null),
            CancellationToken.None);

        Assert.Contains(RoleNames.Patient, session.Account.Roles);
        Assert.Contains(PermissionCodes.AccountRead, session.Account.Permissions);
        Assert.StartsWith("access-", session.AccessToken, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reused_refresh_token_revokes_the_replacement()
    {
        var harness = CreateHarness(exposeCode: true);
        var session = await SignInAsync(harness);
        var rotated = await harness.Service.RefreshAsync(new RefreshSessionCommand(session.RefreshToken, null, null), CancellationToken.None);

        var reuse = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.RefreshAsync(new RefreshSessionCommand(session.RefreshToken, null, null), CancellationToken.None));
        var replacement = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.RefreshAsync(new RefreshSessionCommand(rotated.RefreshToken, null, null), CancellationToken.None));

        Assert.Equal("refresh_reuse", reuse.Code);
        Assert.Equal("refresh_reuse", replacement.Code);
    }

    [Fact]
    public async Task Development_admin_phone_also_receives_the_platform_role()
    {
        var harness = CreateHarness(exposeCode: true);
        harness.Options.DevelopmentAdminPhones = ["9876543210"];

        var session = await SignInAsync(harness);

        Assert.Contains(RoleNames.PlatformAdmin, session.Account.Roles);
        Assert.Contains(PermissionCodes.AdminAccess, session.Account.Permissions);
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
        await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.RefreshAsync(new RefreshSessionCommand(session.RefreshToken, null, null), CancellationToken.None));

        harness.Clock.UtcNow = Now.AddMinutes(1);
        await harness.Service.RequestOtpAsync(new RequestOtpCommand("9876543210", null, null), CancellationToken.None);
        var blocked = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.VerifyOtpAsync(
                new VerifyOtpCommand("9876543210", harness.Sender.Sent[^1].Code, null, null, null),
                CancellationToken.None));
        Assert.Equal("account_deletion_pending", blocked.Code);
    }

    private static async Task<AuthSessionResult> SignInAsync(AuthHarness harness)
    {
        await harness.Service.RequestOtpAsync(new RequestOtpCommand("9876543210", null, null), CancellationToken.None);
        return await harness.Service.VerifyOtpAsync(
            new VerifyOtpCommand("9876543210", harness.Sender.Sent[^1].Code, "phone", null, null),
            CancellationToken.None);
    }

    private static AuthHarness CreateHarness(bool exposeCode)
    {
        var options = new SessionOptions { ExposeOtpInResponse = exposeCode, OtpResendCooldownSeconds = 30 };
        var clock = new FakeClock(Now);
        var store = new InMemoryAuthStore();
        var sender = new CapturingOtpSender();
        var service = new AuthService(
            store,
            new PlainOtpProtector(),
            sender,
            new SequentialRefreshTokenFactory(),
            new FakeAccessTokenIssuer(),
            clock,
            Options.Create(options),
            NullLogger<AuthService>.Instance);
        var accounts = new AccountService(store, clock, NullLogger<AccountService>.Instance);
        return new AuthHarness(service, accounts, store, sender, clock, options);
    }

    private sealed record AuthHarness(
        AuthService Service,
        AccountService Accounts,
        InMemoryAuthStore Store,
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
            null);
        Accounts.Add(account);
        return Task.FromResult(account);
    }

    public Task UpdatePatientProfileAsync(
        Guid userId,
        string? fullName,
        string preferredLanguage,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var account = Require(userId);
        var patient = Patient.Register(userId, now);
        if (account.DeletionRequestedAtUtc is not null)
        {
            patient.RequestDeletion(account.DeletionRequestedAtUtc.Value);
        }

        patient.UpdateProfile(fullName, preferredLanguage, now);
        Replace(account with { FullName = patient.FullName, PreferredLanguage = patient.PreferredLanguage });
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
