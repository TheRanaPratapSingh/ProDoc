using Microsoft.Extensions.Logging;
using ProDoc.Application.Abstractions;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;

namespace ProDoc.Application.Auth;

public sealed class AccountService(IAuthStore store, IClock clock, ILogger<AccountService> logger)
{
    public async Task<AccountSnapshot> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await store.FindAccountByUserIdAsync(userId, cancellationToken)
            ?? throw DomainException.NotFound("account_not_found", "Account was not found.");
    }

    public async Task<AccountSnapshot> UpdateProfileAsync(UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        await store.UpdatePatientProfileAsync(
            command.UserId,
            command.FullName,
            command.PreferredLanguage,
            now,
            cancellationToken,
            command.AgeYears,
            command.Gender);
        await store.AddAuditLogAsync(
            AuditLog.Record(command.UserId, AuditActions.ProfileUpdated, now, command.IpAddress, command.UserAgent),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Updated profile for user {UserId}", command.UserId);
        return await GetAsync(command.UserId, cancellationToken);
    }

    public async Task<AccountSnapshot> CompleteProfileAsync(
        CompletePatientProfileCommand command,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        await store.CompletePatientProfileAsync(
            command.UserId,
            command.FullName,
            command.AgeYears,
            command.Gender,
            command.PreferredLanguage,
            now,
            cancellationToken);
        await store.AddAuditLogAsync(
            AuditLog.Record(command.UserId, AuditActions.ProfileUpdated, now, command.IpAddress, command.UserAgent),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Completed basic profile for user {UserId}", command.UserId);
        return await GetAsync(command.UserId, cancellationToken);
    }

    public async Task<DeletionRequestResult> RequestDeletionAsync(
        RequestAccountDeletionCommand command,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        await store.MarkDeletionRequestedAsync(command.UserId, now, cancellationToken);
        await store.RevokeAllForUserAsync(command.UserId, now, cancellationToken);
        await store.AddAuditLogAsync(
            AuditLog.Record(command.UserId, AuditActions.DeletionRequested, now, command.IpAddress, command.UserAgent),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deletion requested for user {UserId}", command.UserId);

        var account = await GetAsync(command.UserId, cancellationToken);
        return new DeletionRequestResult(account.DeletionRequestedAtUtc ?? now);
    }
}
