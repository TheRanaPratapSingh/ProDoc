using Microsoft.EntityFrameworkCore;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Admin;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Providers;
using ProDoc.Infrastructure.Identity;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.Admin;

public sealed class AdminClinicStore(ProDocDbContext db) : IAdminClinicStore
{
    public Task<Clinic?> FindClinicAsync(Guid clinicId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.Clinics.Include(item => item.Branches) : db.Clinics.AsNoTracking().Include(item => item.Branches);
        return query.SingleOrDefaultAsync(item => item.Id == clinicId, cancellationToken);
    }

    public Task AddClinicAsync(Clinic clinic, CancellationToken cancellationToken)
    {
        db.Clinics.Add(clinic);
        return Task.CompletedTask;
    }

    public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(audit);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<AdminClinicSummary>> ListClinicsAsync(string? query, int take, CancellationToken cancellationToken)
    {
        var clinics = db.Clinics.AsNoTracking()
            .Where(item => item.Status == VerificationStatus.Approved)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            clinics = clinics.Where(item =>
                (item.Name != null && item.Name.Contains(q))
                || item.Reference.Contains(q)
                || (item.PhoneNumber != null && item.PhoneNumber.Contains(q))
                || (item.Email != null && item.Email.Contains(q)));
        }

        var rows = await clinics
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Take(take)
            .Select(item => new
            {
                item.Id,
                item.Reference,
                item.Name,
                Status = item.Status.ToString(),
                item.PhoneNumber,
                item.Email,
                item.UpdatedAtUtc,
                City = item.Branches.Where(branch => branch.IsPrimary).Select(branch => branch.City).FirstOrDefault()
                    ?? item.Branches.Select(branch => branch.City).FirstOrDefault(),
                State = item.Branches.Where(branch => branch.IsPrimary).Select(branch => branch.State).FirstOrDefault()
                    ?? item.Branches.Select(branch => branch.State).FirstOrDefault(),
                DoctorCount = db.DoctorClinicAffiliations.Count(link => link.ClinicId == item.Id),
                StaffCount = db.ClinicStaffMembers.Count(link => link.ClinicId == item.Id && link.IsActive)
            })
            .ToListAsync(cancellationToken);

        return rows.Select(item => new AdminClinicSummary(
            item.Id,
            item.Reference,
            item.Name,
            item.Status,
            item.PhoneNumber,
            item.Email,
            item.City,
            item.State,
            item.DoctorCount,
            item.StaffCount,
            item.UpdatedAtUtc)).ToList();
    }

    public async Task<AdminClinicDetails?> GetClinicDetailsAsync(Guid clinicId, CancellationToken cancellationToken)
    {
        var clinic = await db.Clinics.AsNoTracking()
            .Include(item => item.Branches)
            .SingleOrDefaultAsync(item => item.Id == clinicId, cancellationToken);
        if (clinic is null)
        {
            return null;
        }

        var doctorLinks = await db.DoctorClinicAffiliations.AsNoTracking()
            .Where(item => item.ClinicId == clinicId)
            .Join(db.Doctors.AsNoTracking(), link => link.DoctorId, doctor => doctor.Id, (link, doctor) => new { link, doctor })
            .Join(db.Users.AsNoTracking(), row => row.doctor.UserId, user => user.Id, (row, user) => new { row.link, row.doctor, user })
            .ToListAsync(cancellationToken);

        var staffLinks = await db.ClinicStaffMembers.AsNoTracking()
            .Where(item => item.ClinicId == clinicId)
            .Join(db.Users.AsNoTracking(), link => link.UserId, user => user.Id, (link, user) => new { link, user })
            .ToListAsync(cancellationToken);
        var staffUserIds = staffLinks.Select(item => item.user.Id).ToArray();
        var staffPatients = await db.Patients.AsNoTracking()
            .Where(item => staffUserIds.Contains(item.UserId))
            .ToDictionaryAsync(item => item.UserId, cancellationToken);

        var doctors = doctorLinks.Select(item => new AdminClinicMember(
            item.user.Id,
            item.doctor.Id,
            DisplayName(item.doctor, item.user),
            item.user.PhoneNumber ?? string.Empty,
            RoleNames.Doctor,
            null,
            true)).ToList();

        var staff = staffLinks.Select(item => new AdminClinicMember(
            item.user.Id,
            null,
            staffPatients.TryGetValue(item.user.Id, out var patient) && !string.IsNullOrWhiteSpace(patient.FullName)
                ? patient.FullName!
                : item.user.UserName ?? "Staff",
            item.user.PhoneNumber ?? string.Empty,
            item.link.Role,
            null,
            item.link.IsActive)).ToList();

        return new AdminClinicDetails(
            clinic.Id,
            clinic.Reference,
            clinic.Name,
            clinic.Type?.ToString(),
            clinic.Status.ToString(),
            clinic.PhoneNumber,
            clinic.Email,
            clinic.Website,
            clinic.Description,
            clinic.RegistrationNumber,
            clinic.OwnerUserId,
            clinic.Branches.Select(branch => new AdminClinicBranch(
                branch.Id,
                branch.Name,
                branch.AddressLine,
                branch.AddressLine2,
                branch.Landmark,
                branch.Locality,
                branch.City,
                branch.District,
                branch.State,
                branch.PostalCode,
                branch.Country,
                branch.Latitude,
                branch.Longitude,
                branch.GooglePlaceId,
                branch.FormattedAddress,
                branch.PhoneNumber,
                branch.IsPrimary,
                branch.IsActive,
                branch.Latitude is not null && branch.Longitude is not null)).ToList(),
            doctors,
            staff,
            clinic.CreatedAtUtc,
            clinic.UpdatedAtUtc);
    }

    public Task<DoctorClinicAffiliation?> FindAffiliationAsync(Guid affiliationId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.DoctorClinicAffiliations : db.DoctorClinicAffiliations.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == affiliationId, cancellationToken);
    }

    public async Task AffiliateDoctorAsync(Guid clinicId, Guid doctorId, Guid? branchId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var exists = await db.DoctorClinicAffiliations.AnyAsync(
            item => item.DoctorId == doctorId && item.ClinicId == clinicId,
            cancellationToken);
        if (exists)
        {
            return;
        }

        var hasPrimary = await db.DoctorClinicAffiliations.AnyAsync(item => item.DoctorId == doctorId && item.IsPrimary, cancellationToken);
        db.DoctorClinicAffiliations.Add(DoctorClinicAffiliation.Create(
            doctorId,
            clinicId,
            branchId,
            AssociationKind.Employed,
            isPrimary: !hasPrimary,
            now));
    }

    public async Task ActivateDoctorDiscoveryAsync(Guid doctorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var doctor = await db.Doctors
            .Include(item => item.Specializations)
            .Include(item => item.Registrations)
            .SingleOrDefaultAsync(item => item.Id == doctorId, cancellationToken)
            ?? throw DomainException.NotFound("doctor_not_found", "That doctor was not found.");

        if (doctor.Status != VerificationStatus.Approved)
        {
            doctor.ProvisionApproved(now);
        }
        else
        {
            doctor.SetAcceptingPatients(true, now);
        }

        var clinicIds = await db.DoctorClinicAffiliations
            .Where(item => item.DoctorId == doctorId)
            .Select(item => item.ClinicId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (clinicIds.Count == 0)
        {
            throw DomainException.Validation(
                "doctor_clinic_required",
                "Affiliate the doctor to an approved clinic before enabling patient search.");
        }

        foreach (var clinicId in clinicIds)
        {
            await DoctorPublicDiscoveryHelper.EnsurePublishedClinicScheduleAsync(
                db,
                doctorId,
                clinicId,
                preferredBranchId: null,
                now,
                cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await DoctorPublicDiscoveryHelper.EnsureDiscoverableAsync(db, doctorId, cancellationToken);
    }

    public async Task RemoveDoctorAffiliationAsync(Guid clinicId, Guid doctorId, CancellationToken cancellationToken)
    {
        var rows = await db.DoctorClinicAffiliations
            .Where(item => item.ClinicId == clinicId && item.DoctorId == doctorId)
            .ToListAsync(cancellationToken);
        db.DoctorClinicAffiliations.RemoveRange(rows);
    }

    public async Task AssignStaffAsync(Guid clinicId, Guid userId, Guid? branchId, string roleName, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await db.ClinicStaffMembers
            .SingleOrDefaultAsync(item => item.ClinicId == clinicId && item.UserId == userId, cancellationToken);
        if (existing is not null)
        {
            existing.Reactivate(branchId, roleName, now);
            return;
        }

        db.ClinicStaffMembers.Add(ClinicStaffMember.Assign(clinicId, userId, branchId, roleName, now));
    }

    public async Task DeactivateStaffAsync(Guid clinicId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await db.ClinicStaffMembers
            .SingleOrDefaultAsync(item => item.ClinicId == clinicId && item.UserId == userId, cancellationToken);
        existing?.Deactivate(now);

        var coverage = await db.ReceptionistDoctorAssignments
            .Where(item => item.ClinicId == clinicId && item.ReceptionistUserId == userId && item.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var row in coverage)
        {
            row.Deactivate(now);
        }
    }

    public async Task AssignReceptionistDoctorsAsync(
        Guid clinicId,
        Guid receptionistUserId,
        IReadOnlyList<Guid> doctorIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        foreach (var doctorId in doctorIds.Distinct())
        {
            var existing = await db.ReceptionistDoctorAssignments.SingleOrDefaultAsync(
                item =>
                    item.ClinicId == clinicId
                    && item.ReceptionistUserId == receptionistUserId
                    && item.DoctorId == doctorId,
                cancellationToken);
            if (existing is not null)
            {
                if (!existing.IsActive)
                {
                    existing.Reactivate(now);
                }

                continue;
            }

            db.ReceptionistDoctorAssignments.Add(
                ReceptionistDoctorAssignment.Assign(clinicId, receptionistUserId, doctorId, now));
        }
    }

    public Task<bool> DoctorExistsAsync(Guid doctorId, CancellationToken cancellationToken) =>
        db.Doctors.AnyAsync(item => item.Id == doctorId, cancellationToken);

    public Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(item => item.Id == userId, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw DomainException.Conflict("clinic_conflict", "That clinic change could not be saved. Try again.");
        }
    }

    private static string DisplayName(Doctor doctor, ApplicationUser user)
    {
        if (!string.IsNullOrWhiteSpace(doctor.DisplayName))
        {
            return doctor.DisplayName;
        }

        var composed = $"{doctor.FirstName} {doctor.LastName}".Trim();
        if (!string.IsNullOrWhiteSpace(composed))
        {
            return composed;
        }

        return user.UserName ?? "Doctor";
    }
}
