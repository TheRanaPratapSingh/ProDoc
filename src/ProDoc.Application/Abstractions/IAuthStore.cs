using ProDoc.Application.Auth;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;

namespace ProDoc.Application.Abstractions;

public interface IAuthStore
{
    Task<int> CountRecentOtpChallengesAsync(string phoneE164, DateTimeOffset sinceUtc, CancellationToken cancellationToken);

    Task AddOtpChallengeAsync(OtpChallenge challenge, CancellationToken cancellationToken);

    Task<OtpChallenge?> FindLatestOtpChallengeAsync(string phoneE164, OtpPurpose purpose, CancellationToken cancellationToken);

    Task<AccountSnapshot?> FindAccountByPhoneAsync(string phoneE164, CancellationToken cancellationToken);

    Task<AccountSnapshot?> FindAccountByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<AccountSnapshot> CreatePatientAccountAsync(
        string phoneE164,
        IReadOnlyCollection<string> roles,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task GrantRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken);

    Task UpdatePatientProfileAsync(
        Guid userId,
        string? fullName,
        string preferredLanguage,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        int? ageYears = null,
        string? gender = null);

    Task CompletePatientProfileAsync(
        Guid userId,
        string fullName,
        int ageYears,
        string? gender,
        string preferredLanguage,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task MarkDeletionRequestedAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken);

    Task<RefreshToken?> FindRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken);

    Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
