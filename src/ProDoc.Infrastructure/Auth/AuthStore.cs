using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Auth;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Patients;
using ProDoc.Infrastructure.Identity;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.Auth;

public sealed class AuthStore(ProDocDbContext db) : IAuthStore
{
    public Task<int> CountRecentOtpChallengesAsync(string phoneE164, DateTimeOffset sinceUtc, CancellationToken cancellationToken) =>
        db.OtpChallenges.CountAsync(
            challenge => challenge.PhoneE164 == phoneE164 && challenge.CreatedAtUtc >= sinceUtc,
            cancellationToken);

    public Task AddOtpChallengeAsync(OtpChallenge challenge, CancellationToken cancellationToken)
    {
        db.OtpChallenges.Add(challenge);
        return Task.CompletedTask;
    }

    public Task<OtpChallenge?> FindLatestOtpChallengeAsync(string phoneE164, OtpPurpose purpose, CancellationToken cancellationToken) =>
        db.OtpChallenges
            .Where(challenge => challenge.PhoneE164 == phoneE164 && challenge.Purpose == purpose)
            .OrderByDescending(challenge => challenge.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<AccountSnapshot?> FindAccountByPhoneAsync(string phoneE164, CancellationToken cancellationToken)
    {
        var normalized = phoneE164.ToUpperInvariant();
        var userId = await db.Users
            .Where(user => user.NormalizedUserName == normalized && user.PermanentlyDeletedAtUtc == null)
            .Select(user => (Guid?)user.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return userId is null ? null : await MapAccountAsync(userId.Value, cancellationToken);
    }

    public Task<AccountSnapshot?> FindAccountByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        MapAccountAsync(userId, cancellationToken);

    public async Task<AccountSnapshot> CreatePatientAccountAsync(
        string phoneE164,
        IReadOnlyCollection<string> roles,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = phoneE164,
            NormalizedUserName = phoneE164.ToUpperInvariant(),
            PhoneNumber = phoneE164,
            PhoneNumberConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = now
        };
        db.Users.Add(user);

        var distinctRoles = roles.Distinct(StringComparer.Ordinal).ToArray();
        foreach (var roleName in distinctRoles)
        {
            var normalized = roleName.ToUpperInvariant();
            var role = await db.Roles.SingleOrDefaultAsync(candidate => candidate.NormalizedName == normalized, cancellationToken)
                ?? throw new InvalidOperationException($"Role '{roleName}' is not seeded.");
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = role.Id });
        }

        db.Patients.Add(Patient.Register(user.Id, now));
        var surfaces = DashboardResolver.ListSurfaces(distinctRoles);
        return new AccountSnapshot(
            user.Id,
            phoneE164,
            null,
            Languages.English,
            distinctRoles,
            PermissionsFor(distinctRoles),
            null,
            null,
            DashboardResolver.Resolve(distinctRoles),
            surfaces,
            AgeYears: null,
            Gender: null,
            ProfileCompleted: false);
    }

    public async Task GrantRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken)
    {
        var normalized = roleName.ToUpperInvariant();
        var role = await db.Roles.SingleOrDefaultAsync(item => item.NormalizedName == normalized, cancellationToken)
            ?? throw new InvalidOperationException($"Role '{roleName}' is not seeded.");
        var exists = await db.UserRoles.AnyAsync(item => item.UserId == userId && item.RoleId == role.Id, cancellationToken);
        if (!exists)
        {
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = userId, RoleId = role.Id });
        }
    }

    public async Task UpdatePatientProfileAsync(
        Guid userId,
        string? fullName,
        string preferredLanguage,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        int? ageYears = null,
        string? gender = null)
    {
        var patient = await TrackedPatientAsync(userId, cancellationToken);
        patient.UpdateProfile(fullName, preferredLanguage, now, ageYears, gender);
    }

    public async Task CompletePatientProfileAsync(
        Guid userId,
        string fullName,
        int ageYears,
        string? gender,
        string preferredLanguage,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var patient = await TrackedPatientAsync(userId, cancellationToken);
        patient.CompleteBasicProfile(fullName, ageYears, gender, preferredLanguage, now);
    }

    public async Task MarkDeletionRequestedAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var patient = await TrackedPatientAsync(userId, cancellationToken);
        patient.RequestDeletion(now);
    }

    public Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        db.RefreshTokens.Add(token);
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> FindRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var tokens = await db.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.Revoke(now, null);
        }
    }

    public async Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var tokens = await db.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.Revoke(now, null);
        }
    }

    public Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(auditLog);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw DomainException.Conflict("concurrency_conflict", "The record changed. Reload it and try again.");
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw DomainException.Conflict("concurrency_conflict", "That request conflicted with another one. Try again.");
        }
    }

    private async Task<Patient> TrackedPatientAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Patients.SingleOrDefaultAsync(patient => patient.UserId == userId, cancellationToken)
        ?? throw DomainException.NotFound("account_not_found", "Account was not found.");

    private async Task<AccountSnapshot?> MapAccountAsync(Guid userId, CancellationToken cancellationToken)
    {
        var row = await (
            from user in db.Users.AsNoTracking()
            join patient in db.Patients.AsNoTracking() on user.Id equals patient.UserId
            where user.Id == userId && user.PermanentlyDeletedAtUtc == null
            select new
            {
                user.Id,
                Phone = user.PhoneNumber ?? user.UserName ?? string.Empty,
                patient.FullName,
                patient.PreferredLanguage,
                patient.DeletionRequestedAtUtc,
                user.SuspendedAtUtc,
                patient.AgeYears,
                patient.Gender,
                patient.ProfileCompleted
            }).SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var roles = await (
            from userRole in db.UserRoles.AsNoTracking()
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userRole.UserId == userId && role.Name != null
            select role.Name!).ToListAsync(cancellationToken);

        var surfaces = DashboardResolver.ListSurfaces(roles);
        return new AccountSnapshot(
            row.Id,
            row.Phone,
            row.FullName,
            row.PreferredLanguage,
            roles,
            PermissionsFor(roles),
            row.DeletionRequestedAtUtc,
            row.SuspendedAtUtc,
            DashboardResolver.Resolve(roles),
            surfaces,
            row.AgeYears,
            row.Gender,
            row.ProfileCompleted);
    }

    private static string[] PermissionsFor(IEnumerable<string> roles) =>
        roles.Where(role => PermissionCodes.RoleGrants.ContainsKey(role))
            .SelectMany(role => PermissionCodes.RoleGrants[role])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sql && sql.Number is 2601 or 2627;
}
