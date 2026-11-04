using Microsoft.Extensions.Logging;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Providers;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Providers;

namespace ProDoc.Application.Queue;

public sealed class StaffService(
    IQueueStore queue,
    IClinicAccess access,
    IProviderStore providers,
    IClock clock,
    ILogger<StaffService> logger)
{
    public async Task<IReadOnlyList<StaffMemberDetails>> ListAsync(Guid actorUserId, Guid clinicId, CancellationToken cancellationToken)
    {
        await access.EnsureClinicOwnerAsync(actorUserId, clinicId, cancellationToken);
        var rows = await queue.ListStaffAsync(clinicId, cancellationToken);
        var mapped = new List<StaffMemberDetails>();
        foreach (var row in rows)
        {
            mapped.Add(new StaffMemberDetails(
                row.Id,
                row.UserId,
                await providers.FindUserPhoneAsync(row.UserId, cancellationToken),
                row.Role,
                row.BranchId,
                row.IsActive));
        }

        return mapped;
    }

    public async Task<StaffMemberDetails> AssignAsync(
        Guid actorUserId,
        Guid clinicId,
        string phoneNumber,
        string role,
        Guid? branchId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await access.EnsureClinicOwnerAsync(actorUserId, clinicId, cancellationToken);
        var clinic = await providers.FindClinicAsync(clinicId, tracking: false, cancellationToken)
            ?? throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
        if (branchId is Guid branch && clinic.Branches.All(item => item.Id != branch || !item.IsActive))
        {
            throw DomainException.NotFound("branch_not_found", "That branch was not found.");
        }

        var phone = PhoneNumber.ParseIndianMobile(phoneNumber);
        var userId = await queue.FindUserIdByPhoneAsync(phone.E164.ToUpperInvariant(), cancellationToken)
            ?? throw DomainException.NotFound("account_not_found", "Ask that person to sign in once before assigning them.");
        if (userId == clinic.OwnerUserId)
        {
            throw DomainException.Validation("staff_invalid", "The clinic owner is already authorized.");
        }

        var existing = await queue.FindStaffAsync(clinicId, userId, cancellationToken);
        var now = clock.UtcNow;
        if (existing is not null && existing.IsActive)
        {
            throw DomainException.Conflict("staff_exists", "That account is already assigned to this clinic.");
        }

        ClinicStaffMember member;
        if (existing is not null)
        {
            existing.Reactivate(branchId, role, now);
            member = existing;
        }
        else
        {
            member = ClinicStaffMember.Assign(clinicId, userId, branchId, role, now);
            queue.AddStaff(member);
        }

        await queue.GrantRoleAsync(userId, member.Role, cancellationToken);
        await queue.AddAuditAsync(AuditLog.Record(actorUserId, AuditActions.ClinicStaffAssigned, now, metadata.IpAddress, metadata.UserAgent), cancellationToken);
        await queue.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Clinic staff {UserId} assigned to clinic {ClinicId}", userId, clinicId);
        return new StaffMemberDetails(member.Id, member.UserId, phone.E164, member.Role, member.BranchId, member.IsActive);
    }

    public async Task RemoveAsync(Guid actorUserId, Guid clinicId, Guid staffId, RequestMetadata metadata, CancellationToken cancellationToken)
    {
        await access.EnsureClinicOwnerAsync(actorUserId, clinicId, cancellationToken);
        var rows = await queue.ListStaffAsync(clinicId, cancellationToken);
        var member = rows.FirstOrDefault(item => item.Id == staffId)
            ?? throw DomainException.NotFound("staff_not_found", "That staff assignment was not found.");
        var tracked = await queue.FindStaffAsync(clinicId, member.UserId, cancellationToken)
            ?? throw DomainException.NotFound("staff_not_found", "That staff assignment was not found.");
        var now = clock.UtcNow;
        tracked.Deactivate(now);
        await queue.AddAuditAsync(AuditLog.Record(actorUserId, AuditActions.ClinicStaffRemoved, now, metadata.IpAddress, metadata.UserAgent), cancellationToken);
        await queue.SaveChangesAsync(cancellationToken);
    }
}
