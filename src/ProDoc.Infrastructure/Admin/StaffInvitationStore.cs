using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Admin;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Patients;
using ProDoc.Domain.Providers;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.Admin;

public sealed class StaffInvitationStore(ProDocDbContext db) : IStaffInvitationStore
{
    public Task<StaffInvitation?> FindPendingByPhoneAsync(string phoneE164, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.StaffInvitations
            .Where(item =>
                item.PhoneE164 == phoneE164
                && item.Status == StaffInvitationStatus.Pending
                && item.ExpiresAtUtc > now)
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<StaffInvitation?> FindByIdAsync(Guid invitationId, CancellationToken cancellationToken) =>
        db.StaffInvitations.SingleOrDefaultAsync(item => item.Id == invitationId, cancellationToken);

    public async Task<IReadOnlyList<StaffInvitationDetails>> ListAsync(string? status, int take, CancellationToken cancellationToken)
    {
        var query = db.StaffInvitations.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<StaffInvitationStatus>(status, true, out var parsed))
        {
            query = query.Where(item => item.Status == parsed);
        }

        var rows = await query
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
        return rows.Select(MapInvitation).ToList();
    }

    private static StaffInvitationDetails MapInvitation(StaffInvitation item)
    {
        var payload = StaffInvitePayload.Parse(item.PayloadJson);
        return new StaffInvitationDetails(
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
            item.AcceptedAtUtc,
            payload?.SpecializationId,
            payload?.YearsOfExperience,
            payload?.Qualifications,
            payload?.RegistrationNumber,
            payload?.Designation,
            payload?.ClinicIds,
            payload?.DoctorIds);
    }

    public Task AddAsync(StaffInvitation invitation, CancellationToken cancellationToken)
    {
        db.StaffInvitations.Add(invitation);
        return Task.CompletedTask;
    }

    public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(audit);
        return Task.CompletedTask;
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

    public async Task EnsurePatientProfileAsync(Guid userId, string? fullName, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var patient = await db.Patients.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (patient is null)
        {
            patient = Patient.Register(userId, now);
            db.Patients.Add(patient);
        }
        else
        {
            // Staff invite acceptance must not leave deletion-pending accounts hidden from discovery.
            patient.ClearDeletionRequest(now);
        }

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            patient.UpdateProfile(fullName.Trim(), patient.PreferredLanguage, now);
        }
    }

    public async Task AssignClinicStaffAsync(
        Guid clinicId,
        Guid userId,
        Guid? branchId,
        string roleName,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (roleName is not (RoleNames.Receptionist or RoleNames.ClinicStaff))
        {
            return;
        }

        var existing = await db.ClinicStaffMembers
            .SingleOrDefaultAsync(item => item.ClinicId == clinicId && item.UserId == userId, cancellationToken);
        if (existing is not null)
        {
            if (!existing.IsActive)
            {
                existing.Reactivate(branchId, roleName, now);
            }

            return;
        }

        db.ClinicStaffMembers.Add(ClinicStaffMember.Assign(clinicId, userId, branchId, roleName, now));
    }

    public async Task EnsureDoctorDraftAsync(Guid userId, string? fullName, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await FindDoctorByUserIdAsync(userId, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        var doctor = Doctor.Begin(userId, Languages.English, now);
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            var parts = fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            doctor.UpdateBasics(
                parts[0],
                parts.Length > 1 ? parts[1] : null,
                null,
                null,
                null,
                Languages.English,
                now);
        }

        db.Doctors.Add(doctor);
    }

    public async Task ApplyDoctorInviteProfileAsync(
        Guid userId,
        string? fullName,
        Guid? specializationId,
        int? yearsOfExperience,
        string? qualifications,
        string? registrationNumber,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // EnsureDoctorDraftAsync may have only staged the row in the change tracker —
        // resolve via Local first so invite verification does not miss an unsaved draft.
        await EnsureDoctorDraftAsync(userId, fullName, now, cancellationToken);
        var doctor = await FindDoctorByUserIdAsync(userId, cancellationToken)
            ?? throw DomainException.NotFound("doctor_not_found", "Doctor profile was not created for this invitation.");

        var experience = yearsOfExperience ?? doctor.YearsOfExperience;
        if (experience is < 0 or > 70)
        {
            throw DomainException.Validation(
                "experience_invalid",
                "Years of experience must be between 0 and 70.");
        }

        var spoken = doctor.Languages.Select(item => item.Code).ToArray();
        var expertise = doctor.Expertise.Select(item => item.Label).ToArray();

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            var parts = fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            doctor.UpdateBasics(
                parts[0],
                parts.Length > 1 ? parts[1] : doctor.LastName,
                doctor.DateOfBirth,
                doctor.Gender,
                doctor.Email,
                doctor.PreferredLanguage,
                now);
            doctor.UpdateProfessional(fullName.Trim(), doctor.Biography, experience, spoken, expertise, now);
        }
        else
        {
            doctor.UpdateProfessional(doctor.DisplayName, doctor.Biography, experience, spoken, expertise, now);
        }

        if (specializationId is not Guid specialization || specialization == Guid.Empty)
        {
            throw DomainException.Validation(
                "invite_specialization_required",
                "Choose a specialization for the doctor.");
        }

        var specializationExists = await db.Specializations.AnyAsync(
            item => item.Id == specialization && item.IsActive,
            cancellationToken);
        if (!specializationExists)
        {
            throw DomainException.Validation(
                "specialization_invalid",
                "Choose an active specialization for the doctor.");
        }

        doctor.SetSpecializations([specialization], specialization, now);

        if (!string.IsNullOrWhiteSpace(qualifications))
        {
            var year = now.Year - 5;
            var already = doctor.Qualifications.Any(item =>
                item.Name.Equals(qualifications.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!already)
            {
                doctor.AddQualification(qualifications.Trim(), null, year, now);
            }
        }

        if (string.IsNullOrWhiteSpace(registrationNumber))
        {
            throw DomainException.Validation(
                "invite_registration_required",
                "Enter the doctor's medical registration number so the profile can be activated for patient search.");
        }

        doctor.UpdateRegistration(
            registrationNumber.Trim(),
            "Medical Council",
            "India",
            "permanent",
            DateOnly.FromDateTime(now.UtcDateTime.AddYears(-1)),
            null,
            now);
    }

    public async Task AffiliateDoctorToClinicsAsync(
        Guid userId,
        IReadOnlyList<Guid> clinicIds,
        Guid? branchId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var doctor = await FindDoctorByUserIdAsync(userId, cancellationToken);
        if (doctor is null)
        {
            return;
        }

        // Invite acceptance may call this more than once before SaveChanges; check Local
        // so a staged (unsaved) affiliation is not inserted twice.
        var linkedClinicIds = db.DoctorClinicAffiliations.Local
            .Where(item => item.DoctorId == doctor.Id)
            .Select(item => item.ClinicId)
            .ToHashSet();
        foreach (var clinicId in await db.DoctorClinicAffiliations
            .Where(item => item.DoctorId == doctor.Id)
            .Select(item => item.ClinicId)
            .ToListAsync(cancellationToken))
        {
            linkedClinicIds.Add(clinicId);
        }

        var hasPrimary = db.DoctorClinicAffiliations.Local.Any(item => item.DoctorId == doctor.Id && item.IsPrimary)
            || await db.DoctorClinicAffiliations.AnyAsync(
                item => item.DoctorId == doctor.Id && item.IsPrimary,
                cancellationToken);

        foreach (var clinicId in clinicIds.Distinct())
        {
            if (!linkedClinicIds.Add(clinicId))
            {
                continue;
            }

            db.DoctorClinicAffiliations.Add(DoctorClinicAffiliation.Create(
                doctor.Id,
                clinicId,
                branchId,
                AssociationKind.Employed,
                isPrimary: !hasPrimary,
                now));
            hasPrimary = true;
        }
    }

    public async Task<Guid> ActivateDoctorForPublicDiscoveryAsync(
        Guid userId,
        IReadOnlyList<Guid> clinicIds,
        Guid? branchId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Prefer Local so an unsaved draft from EnsureDoctorDraftAsync is still found.
        var doctor = await FindDoctorByUserIdAsync(userId, cancellationToken)
            ?? throw DomainException.NotFound("doctor_not_found", "Doctor profile was not created for this invitation.");

        // Load collections needed for approval when the entity came from Local without Includes.
        await db.Entry(doctor).Collection(item => item.Specializations).LoadAsync(cancellationToken);
        await db.Entry(doctor).Collection(item => item.Registrations).LoadAsync(cancellationToken);

        if (clinicIds.Count == 0)
        {
            throw DomainException.Validation(
                "invite_clinic_required",
                "Assign at least one clinic for the doctor.");
        }

        // Ensure affiliations exist before publishing schedules (idempotent).
        await AffiliateDoctorToClinicsAsync(userId, clinicIds, branchId, now, cancellationToken);

        doctor.ProvisionApproved(now);

        foreach (var clinicId in clinicIds.Distinct())
        {
            await DoctorPublicDiscoveryHelper.EnsurePublishedClinicScheduleAsync(
                db,
                doctor.Id,
                clinicId,
                branchId,
                now,
                cancellationToken);
        }

        // Persist approval + schedules before asserting discoverability (queries hit the database).
        await db.SaveChangesAsync(cancellationToken);
        await DoctorPublicDiscoveryHelper.EnsureDiscoverableAsync(db, doctor.Id, cancellationToken);
        return doctor.Id;
    }

    public async Task<(Guid DoctorId, string Status, bool IsPubliclyDiscoverable)?> GetDoctorDiscoveryStateAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var doctor = await db.Doctors.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (doctor is null)
        {
            return null;
        }

        var discoverable = doctor.Status == VerificationStatus.Approved
            && doctor.IsAcceptingPatients
            && await (
                from schedule in db.ConsultationSchedules.AsNoTracking()
                where schedule.DoctorId == doctor.Id && schedule.IsPublished
                join clinic in db.Clinics.AsNoTracking() on schedule.ClinicId equals clinic.Id
                join branch in db.ClinicBranches.AsNoTracking() on schedule.BranchId equals branch.Id
                where clinic.Status == VerificationStatus.Approved
                    && branch.IsActive
                    && db.DoctorClinicAffiliations.Any(link => link.DoctorId == doctor.Id && link.ClinicId == clinic.Id)
                select schedule.Id).AnyAsync(cancellationToken);

        return (doctor.Id, doctor.Status.ToString(), discoverable);
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

    public async Task TransferClinicOwnershipAsync(Guid clinicId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var clinic = await db.Clinics.SingleOrDefaultAsync(item => item.Id == clinicId, cancellationToken)
            ?? throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
        clinic.TransferOwnership(userId, now);
    }

    public Task<bool> ClinicExistsAsync(Guid clinicId, CancellationToken cancellationToken) =>
        db.Clinics.AnyAsync(
            item => item.Id == clinicId
                && item.Status == VerificationStatus.Approved
                && item.Branches.Any(branch => branch.IsActive),
            cancellationToken);

    public Task<bool> DoctorExistsAsync(Guid doctorId, CancellationToken cancellationToken) =>
        db.Doctors.AnyAsync(item => item.Id == doctorId, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw DomainException.Conflict("invite_conflict", "That invitation could not be saved. Try again.");
        }
    }

    /// <summary>
    /// Resolves a doctor by user id from the change tracker first, then the database.
    /// Invite acceptance creates a draft and applies profile before SaveChanges.
    /// </summary>
    private async Task<Doctor?> FindDoctorByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var tracked = db.Doctors.Local.FirstOrDefault(item => item.UserId == userId);
        if (tracked is not null)
        {
            return tracked;
        }

        return await db.Doctors.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
    }
}
