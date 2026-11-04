using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProDoc.Application.Enterprise;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Common;
using ProDoc.Domain.Enterprise;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Providers;
using ProDoc.Infrastructure.Identity;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.Enterprise;

public sealed class EnterpriseStore(
    ProDocDbContext db,
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<Guid>> roles) : IEnterpriseStore
{
    public Task<Organization?> FindOrganizationAsync(Guid organizationId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.Organizations.AsQueryable() : db.Organizations.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == organizationId, cancellationToken);
    }

    public async Task<IReadOnlyList<Organization>> ListOrganizationsForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var orgIds = await db.OrganizationMemberships.AsNoTracking()
            .Where(item => item.UserId == userId && item.IsActive)
            .Select(item => item.OrganizationId)
            .ToListAsync(cancellationToken);
        return await db.Organizations.AsNoTracking()
            .Where(item => orgIds.Contains(item.Id))
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
    }

    public void AddOrganization(Organization organization) => db.Organizations.Add(organization);

    public Task<OrganizationMembership?> FindMembershipAsync(Guid organizationId, Guid userId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.OrganizationMemberships.AsQueryable() : db.OrganizationMemberships.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.OrganizationId == organizationId && item.UserId == userId, cancellationToken);
    }

    public Task<OrganizationMembership?> FindMembershipByIdAsync(Guid membershipId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.OrganizationMemberships.AsQueryable() : db.OrganizationMemberships.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == membershipId, cancellationToken);
    }

    public async Task<IReadOnlyList<OrganizationMembership>> ListMembershipsAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await db.OrganizationMemberships.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task<int> CountActiveMembershipsAsync(Guid organizationId, CancellationToken cancellationToken) =>
        db.OrganizationMemberships.AsNoTracking().CountAsync(item => item.OrganizationId == organizationId && item.IsActive, cancellationToken);

    public void AddMembership(OrganizationMembership membership) => db.OrganizationMemberships.Add(membership);

    public Task<Clinic?> FindClinicAsync(Guid clinicId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.Clinics.Include(item => item.Branches).AsQueryable() : db.Clinics.AsNoTracking().Include(item => item.Branches);
        return query.SingleOrDefaultAsync(item => item.Id == clinicId, cancellationToken);
    }

    public async Task<IReadOnlyList<Clinic>> ListClinicsForOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await db.Clinics.AsNoTracking()
            .Include(item => item.Branches)
            .Where(item => item.OrganizationId == organizationId)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Clinic>> ListOwnedClinicsAsync(Guid ownerUserId, CancellationToken cancellationToken) =>
        await db.Clinics.AsNoTracking()
            .Include(item => item.Branches)
            .Where(item => item.OwnerUserId == ownerUserId)
            .ToListAsync(cancellationToken);

    public Task<OrganizationSetting?> FindSettingAsync(Guid organizationId, string key, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.OrganizationSettings.AsQueryable() : db.OrganizationSettings.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.OrganizationId == organizationId && item.Key == key, cancellationToken);
    }

    public async Task<IReadOnlyList<OrganizationSetting>> ListSettingsAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await db.OrganizationSettings.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId)
            .OrderBy(item => item.Key)
            .ToListAsync(cancellationToken);

    public void AddSetting(OrganizationSetting setting) => db.OrganizationSettings.Add(setting);

    public Task<BranchSettingOverride?> FindBranchOverrideAsync(Guid branchId, string key, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.BranchSettingOverrides.AsQueryable() : db.BranchSettingOverrides.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.BranchId == branchId && item.Key == key, cancellationToken);
    }

    public async Task<IReadOnlyList<BranchSettingOverride>> ListBranchOverridesAsync(Guid organizationId, Guid? branchId, CancellationToken cancellationToken)
    {
        var query = db.BranchSettingOverrides.AsNoTracking().Where(item => item.OrganizationId == organizationId);
        if (branchId is Guid id)
        {
            query = query.Where(item => item.BranchId == id);
        }

        return await query.OrderBy(item => item.Key).ToListAsync(cancellationToken);
    }

    public void AddBranchOverride(BranchSettingOverride setting) => db.BranchSettingOverrides.Add(setting);

    public Task<ConsultationResource?> FindResourceAsync(Guid resourceId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.ConsultationResources.AsQueryable() : db.ConsultationResources.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == resourceId, cancellationToken);
    }

    public async Task<IReadOnlyList<ConsultationResource>> ListResourcesAsync(Guid organizationId, Guid? branchId, CancellationToken cancellationToken)
    {
        var query = db.ConsultationResources.AsNoTracking().Where(item => item.OrganizationId == organizationId);
        if (branchId is Guid id)
        {
            query = query.Where(item => item.BranchId == id);
        }

        return await query.OrderBy(item => item.Name).ToListAsync(cancellationToken);
    }

    public void AddResource(ConsultationResource resource) => db.ConsultationResources.Add(resource);

    public Task<bool> HasResourceConflictAsync(
        Guid resourceId,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        Guid? excludeAllocationId,
        CancellationToken cancellationToken) =>
        db.ResourceAllocations.AsNoTracking()
            .AnyAsync(
                item => item.ResourceId == resourceId
                    && (excludeAllocationId == null || item.Id != excludeAllocationId)
                    && item.StartsAtUtc < endsAtUtc
                    && item.EndsAtUtc > startsAtUtc,
                cancellationToken);

    public async Task<IReadOnlyList<ResourceAllocation>> ListAllocationsAsync(Guid organizationId, Guid? resourceId, CancellationToken cancellationToken)
    {
        var query = db.ResourceAllocations.AsNoTracking().Where(item => item.OrganizationId == organizationId);
        if (resourceId is Guid id)
        {
            query = query.Where(item => item.ResourceId == id);
        }

        return await query.OrderByDescending(item => item.StartsAtUtc).Take(200).ToListAsync(cancellationToken);
    }

    public void AddAllocation(ResourceAllocation allocation) => db.ResourceAllocations.Add(allocation);

    public Task<OrganizationSubscription?> FindSubscriptionAsync(Guid organizationId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.OrganizationSubscriptions.AsQueryable() : db.OrganizationSubscriptions.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.OrganizationId == organizationId, cancellationToken);
    }

    public void AddSubscription(OrganizationSubscription subscription) => db.OrganizationSubscriptions.Add(subscription);

    public async Task<Guid?> FindUserIdByPhoneAsync(string phoneE164, CancellationToken cancellationToken)
    {
        var normalized = phoneE164.ToUpperInvariant();
        var user = await users.Users.AsNoTracking().SingleOrDefaultAsync(item => item.NormalizedUserName == normalized, cancellationToken);
        return user?.Id;
    }

    public async Task<string?> FindUserPhoneAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        return user?.PhoneNumber;
    }

    public async Task GrantOrganizationAdminRoleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString())
            ?? throw DomainException.NotFound("account_not_found", "That account was not found.");
        if (!await roles.RoleExistsAsync(RoleNames.OrganizationAdmin))
        {
            await roles.CreateAsync(new IdentityRole<Guid>(RoleNames.OrganizationAdmin) { Id = Guid.NewGuid() });
        }

        if (!await users.IsInRoleAsync(user, RoleNames.OrganizationAdmin))
        {
            await users.AddToRoleAsync(user, RoleNames.OrganizationAdmin);
        }
    }

    public async Task<OrganizationAnalytics> BuildAnalyticsAsync(
        Guid organizationId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        Guid? clinicId,
        Guid? branchId,
        Guid? doctorId,
        CancellationToken cancellationToken)
    {
        var clinicIds = await db.Clinics.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        if (clinicId is Guid onlyClinic)
        {
            clinicIds = clinicIds.Where(id => id == onlyClinic).ToList();
        }

        var appointments = await db.Appointments.AsNoTracking()
            .Where(item => clinicIds.Contains(item.ClinicId)
                && item.CreatedAtUtc >= fromUtc
                && item.CreatedAtUtc <= toUtc
                && !item.IsWalkIn)
            .ToListAsync(cancellationToken);
        if (branchId is Guid onlyBranch)
        {
            appointments = appointments.Where(item => item.BranchId == onlyBranch).ToList();
        }

        if (doctorId is Guid onlyDoctor)
        {
            appointments = appointments.Where(item => item.DoctorId == onlyDoctor).ToList();
        }

        var completed = appointments.Count(item => item.Status == AppointmentStatus.Completed);
        var cancelled = appointments.Count(item => item.Status == AppointmentStatus.Cancelled);
        var noShows = appointments.Count(item => item.Status == AppointmentStatus.Expired);
        var volume = appointments.Count;
        var uniquePatients = appointments.Select(item => item.AccountUserId).Distinct().Count();
        var appointmentIds = appointments.Select(item => item.Id).ToList();
        var snapshots = await db.CommissionSnapshots.AsNoTracking()
            .Where(item => appointmentIds.Contains(item.AppointmentId))
            .ToListAsync(cancellationToken);
        var gross = appointments.Where(item => item.Status == AppointmentStatus.Completed).Sum(item => item.ConsultationFee);
        var commission = snapshots.Sum(item => item.CommissionAmount);
        var cancellationRate = volume == 0 ? 0d : Math.Round(100d * cancelled / volume, 2);
        var noShowRate = volume == 0 ? 0d : Math.Round(100d * noShows / volume, 2);

        var branches = await db.ClinicBranches.AsNoTracking()
            .Where(item => clinicIds.Contains(item.ClinicId) && item.IsActive)
            .ToListAsync(cancellationToken);
        var clinics = await db.Clinics.AsNoTracking().Where(item => clinicIds.Contains(item.Id)).ToListAsync(cancellationToken);
        var queueWaits = await db.QueueEntries.AsNoTracking()
            .Where(item => item.AppointmentId != null && appointmentIds.Contains(item.AppointmentId.Value))
            .Select(item => new { item.BranchId, item.CheckInAtUtc, item.CalledAtUtc })
            .ToListAsync(cancellationToken);

        var branchRows = branches.Select(branch =>
        {
            var subset = appointments.Where(item => item.BranchId == branch.Id).ToList();
            var waits = queueWaits
                .Where(item => item.BranchId == branch.Id && item.CalledAtUtc is not null)
                .Select(item => (item.CalledAtUtc!.Value - item.CheckInAtUtc).TotalMinutes)
                .ToList();
            var clinic = clinics.SingleOrDefault(item => item.Id == branch.ClinicId);
            var subsetIds = subset.Select(item => item.Id).ToHashSet();
            return new BranchPerformanceRow(
                branch.Id,
                branch.Name,
                branch.ClinicId,
                clinic?.Name,
                subset.Count,
                subset.Count(item => item.Status == AppointmentStatus.Completed),
                subset.Count(item => item.Status == AppointmentStatus.Cancelled),
                subset.Count(item => item.Status == AppointmentStatus.Expired),
                subset.Where(item => item.Status == AppointmentStatus.Completed).Sum(item => item.ConsultationFee),
                snapshots.Where(item => subsetIds.Contains(item.AppointmentId)).Sum(item => item.CommissionAmount),
                waits.Count == 0 ? null : Math.Round(waits.Average(), 1));
        }).OrderByDescending(item => item.Appointments).ToArray();

        var doctorIds = appointments.Select(item => item.DoctorId).Distinct().ToList();
        var doctors = await db.Doctors.AsNoTracking().Where(item => doctorIds.Contains(item.Id)).ToListAsync(cancellationToken);
        var doctorRows = doctorIds.Select(id =>
        {
            var subset = appointments.Where(item => item.DoctorId == id).ToList();
            var doctor = doctors.SingleOrDefault(item => item.Id == id);
            var name = doctor is null
                ? "Doctor"
                : string.IsNullOrWhiteSpace(doctor.DisplayName)
                    ? $"{doctor.FirstName} {doctor.LastName}".Trim()
                    : doctor.DisplayName;
            return new DoctorUtilizationRow(
                id,
                name,
                subset.Count,
                subset.Count(item => item.Status == AppointmentStatus.Completed),
                subset.Where(item => item.Status == AppointmentStatus.Completed).Sum(item => item.ConsultationFee));
        }).OrderByDescending(item => item.Appointments).ToArray();

        var daily = appointments
            .GroupBy(item => item.CreatedAtUtc.UtcDateTime.Date)
            .OrderBy(group => group.Key)
            .Select(group => new NamedCountPoint(group.Key.ToString("yyyy-MM-dd"), group.Count()))
            .ToArray();

        return new OrganizationAnalytics(
            volume,
            completed,
            cancelled,
            noShows,
            uniquePatients,
            gross,
            commission,
            cancellationRate,
            noShowRate,
            branchRows,
            doctorRows,
            daily);
    }

    public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(audit);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
