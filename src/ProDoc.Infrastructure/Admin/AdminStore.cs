using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProDoc.Application.Admin;
using ProDoc.Domain.Admin;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Billing;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Providers;
using ProDoc.Domain.Queue;
using ProDoc.Infrastructure.Identity;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.Admin;

public sealed class AdminStore(ProDocDbContext db, UserManager<ApplicationUser> users) : IAdminStore
{
    public async Task<AdminDashboardMetrics> GetDashboardAsync(
        DateOnly? from,
        DateOnly? to,
        string? state,
        string? city,
        Guid? clinicId,
        string? appointmentStatus,
        CancellationToken cancellationToken)
    {
        var fromUtc = from is null ? (DateTimeOffset?)null : new DateTimeOffset(from.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toUtc = to is null ? (DateTimeOffset?)null : new DateTimeOffset(to.Value.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        var clinicQuery = db.Clinics.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(state) || !string.IsNullOrWhiteSpace(city) || clinicId is not null)
        {
            var branchFilter = db.ClinicBranches.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(state))
            {
                branchFilter = branchFilter.Where(item => item.State == state);
            }

            if (!string.IsNullOrWhiteSpace(city))
            {
                branchFilter = branchFilter.Where(item => item.City == city);
            }

            var clinicIds = await branchFilter.Select(item => item.ClinicId).Distinct().ToListAsync(cancellationToken);
            if (clinicId is not null)
            {
                clinicIds = clinicIds.Contains(clinicId.Value) || (!state.HasContent() && !city.HasContent())
                    ? [clinicId.Value]
                    : clinicIds.Where(id => id == clinicId.Value).ToList();
                if (clinicIds.Count == 0 && !state.HasContent() && !city.HasContent())
                {
                    clinicIds = [clinicId.Value];
                }
            }

            clinicQuery = clinicQuery.Where(item => clinicIds.Contains(item.Id));
        }

        var clinics = await clinicQuery.ToListAsync(cancellationToken);
        var scopedClinicIds = clinics.Select(item => item.Id).ToHashSet();

        var appointments = db.Appointments.AsNoTracking().AsQueryable();
        if (scopedClinicIds.Count > 0 && (state.HasContent() || city.HasContent() || clinicId is not null))
        {
            appointments = appointments.Where(item => scopedClinicIds.Contains(item.ClinicId));
        }

        if (fromUtc is not null)
        {
            appointments = appointments.Where(item => item.CreatedAtUtc >= fromUtc);
        }

        if (toUtc is not null)
        {
            appointments = appointments.Where(item => item.CreatedAtUtc <= toUtc);
        }

        if (!string.IsNullOrWhiteSpace(appointmentStatus)
            && Enum.TryParse<AppointmentStatus>(appointmentStatus, true, out var status))
        {
            appointments = appointments.Where(item => item.Status == status);
        }

        var appointmentRows = await appointments.Select(item => new { item.Status, item.ConsultationFee, item.CreatedAtUtc, item.IsWalkIn }).ToListAsync(cancellationToken);
        var snapshots = db.CommissionSnapshots.AsNoTracking().AsQueryable();
        if (scopedClinicIds.Count > 0 && (state.HasContent() || city.HasContent() || clinicId is not null))
        {
            snapshots = snapshots.Where(item => scopedClinicIds.Contains(item.ClinicId));
        }

        if (fromUtc is not null)
        {
            snapshots = snapshots.Where(item => item.CapturedAtUtc >= fromUtc);
        }

        if (toUtc is not null)
        {
            snapshots = snapshots.Where(item => item.CapturedAtUtc <= toUtc);
        }

        var snapshotRows = await snapshots.ToListAsync(cancellationToken);
        var refunds = await db.Refunds.AsNoTracking()
            .Where(item => item.Status == RefundStatus.Completed)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

        var monthAppointments = appointmentRows
            .GroupBy(item => item.CreatedAtUtc.ToString("yyyy-MM"))
            .OrderBy(group => group.Key)
            .Select(group => new NamedCount(group.Key, group.Count()))
            .ToArray();
        var monthRevenue = snapshotRows
            .GroupBy(item => item.CapturedAtUtc.ToString("yyyy-MM"))
            .OrderBy(group => group.Key)
            .Select(group => new NamedAmount(group.Key, group.Sum(item => item.CommissionAmount)))
            .ToArray();

        var patients = await db.Patients.AsNoTracking()
            .Select(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var doctors = await db.Doctors.AsNoTracking()
            .Select(item => new { item.CreatedAtUtc, item.Status })
            .ToListAsync(cancellationToken);

        var patientGrowth = patients
            .GroupBy(item => item.ToString("yyyy-MM"))
            .OrderBy(group => group.Key)
            .Select(group => new NamedCount(group.Key, group.Count()))
            .ToArray();
        var providerGrowth = doctors
            .GroupBy(item => item.CreatedAtUtc.ToString("yyyy-MM"))
            .OrderBy(group => group.Key)
            .Select(group => new NamedCount(group.Key, group.Count()))
            .ToArray();

        return new AdminDashboardMetrics(
            patients.Count,
            doctors.Count(item => item.Status == VerificationStatus.Approved),
            await db.Clinics.AsNoTracking().CountAsync(cancellationToken),
            await db.ClinicBranches.AsNoTracking().CountAsync(cancellationToken),
            doctors.Count(item => item.Status == VerificationStatus.Submitted),
            await db.Clinics.AsNoTracking().CountAsync(item => item.Status == VerificationStatus.Submitted, cancellationToken),
            appointmentRows.Count,
            appointmentRows.Count(item => item.Status == AppointmentStatus.Completed),
            appointmentRows.Count(item => item.Status == AppointmentStatus.Cancelled),
            await db.QueueEntries.AsNoTracking().CountAsync(
                item => item.Status == QueueEntryStatus.Waiting
                    || item.Status == QueueEntryStatus.Called
                    || item.Status == QueueEntryStatus.InConsultation
                    || item.Status == QueueEntryStatus.OnHold,
                cancellationToken),
            appointmentRows.Where(item => !item.IsWalkIn).Sum(item => item.ConsultationFee),
            snapshotRows.Sum(item => item.CommissionAmount),
            snapshotRows.Where(item => item.SettlementBatchId == null).Sum(item => item.ClinicPayableAmount),
            refunds,
            monthAppointments,
            monthRevenue,
            patientGrowth,
            providerGrowth);
    }

    public async Task<IReadOnlyList<AdminUserSummary>> SearchUsersAsync(
        string? query,
        string? role,
        bool? suspended,
        Guid? clinicId,
        int take,
        CancellationToken cancellationToken)
    {
        HashSet<Guid>? clinicUserIds = null;
        if (clinicId is Guid scopedClinic)
        {
            var staffIds = await db.ClinicStaffMembers.AsNoTracking()
                .Where(item => item.ClinicId == scopedClinic && item.IsActive)
                .Select(item => item.UserId)
                .ToListAsync(cancellationToken);
            var doctorUserIds = await db.DoctorClinicAffiliations.AsNoTracking()
                .Where(item => item.ClinicId == scopedClinic)
                .Join(db.Doctors.AsNoTracking(), link => link.DoctorId, doctor => doctor.Id, (_, doctor) => doctor.UserId)
                .ToListAsync(cancellationToken);
            var ownerIds = await db.Clinics.AsNoTracking()
                .Where(item => item.Id == scopedClinic)
                .Select(item => item.OwnerUserId)
                .ToListAsync(cancellationToken);
            clinicUserIds = staffIds.Concat(doctorUserIds).Concat(ownerIds).ToHashSet();
        }

        var usersQuery =
            from user in db.Users.AsNoTracking()
            join patient in db.Patients.AsNoTracking() on user.Id equals patient.UserId into patients
            from patient in patients.DefaultIfEmpty()
            where user.PermanentlyDeletedAtUtc == null
            select new { user, patient };

        if (clinicUserIds is not null)
        {
            usersQuery = usersQuery.Where(item => clinicUserIds.Contains(item.user.Id));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            usersQuery = usersQuery.Where(item =>
                item.user.PhoneNumber!.Contains(q)
                || (item.user.Email != null && item.user.Email.Contains(q))
                || (item.patient != null && item.patient.FullName != null && item.patient.FullName.Contains(q)));
        }

        if (suspended == true)
        {
            usersQuery = usersQuery.Where(item => item.user.SuspendedAtUtc != null);
        }
        else if (suspended == false)
        {
            usersQuery = usersQuery.Where(item => item.user.SuspendedAtUtc == null);
        }

        var rows = await usersQuery.OrderByDescending(item => item.user.CreatedAtUtc).Take(Math.Max(take * 3, take)).ToListAsync(cancellationToken);
        var result = new List<AdminUserSummary>();
        foreach (var row in rows)
        {
            var roles = await users.GetRolesAsync(row.user);
            if (!string.IsNullOrWhiteSpace(role) && !roles.Contains(role, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var doctor = await db.Doctors.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == row.user.Id, cancellationToken);
            var clinic = await db.Clinics.AsNoTracking().FirstOrDefaultAsync(item => item.OwnerUserId == row.user.Id, cancellationToken);
            result.Add(new AdminUserSummary(
                row.user.Id,
                row.user.PhoneNumber ?? string.Empty,
                row.patient?.FullName,
                roles.ToArray(),
                row.user.CreatedAtUtc,
                row.user.SuspendedAtUtc,
                row.patient?.DeletionRequestedAtUtc,
                doctor?.Id,
                doctor?.Status.ToString(),
                clinic?.Id,
                clinic?.Status.ToString()));
            if (result.Count >= take)
            {
                break;
            }
        }

        return result;
    }

    public async Task<AdminUserDetails?> GetUserDetailsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null || user.PermanentlyDeletedAtUtc is not null)
        {
            return null;
        }

        var patient = await db.Patients.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        var roles = await users.GetRolesAsync(user);
        var doctor = await db.Doctors.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        string? specialization = null;
        string? qualifications = null;
        string? registration = null;
        if (doctor is not null)
        {
            var primary = await db.Set<DoctorSpecialization>().AsNoTracking()
                .Where(item => item.DoctorId == doctor.Id && item.IsPrimary)
                .Join(db.Specializations.AsNoTracking(), link => link.SpecializationId, spec => spec.Id, (_, spec) => spec.Name)
                .FirstOrDefaultAsync(cancellationToken);
            specialization = primary;
            qualifications = await db.Set<DoctorQualification>().AsNoTracking()
                .Where(item => item.DoctorId == doctor.Id)
                .Select(item => item.Name)
                .FirstOrDefaultAsync(cancellationToken);
            registration = await db.Set<DoctorRegistration>().AsNoTracking()
                .Where(item => item.DoctorId == doctor.Id && item.IsCurrent)
                .Select(item => item.Number)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var staffClinics = await db.ClinicStaffMembers.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Join(db.Clinics.AsNoTracking(), link => link.ClinicId, clinic => clinic.Id, (link, clinic) => new AdminUserClinicLink(
                clinic.Id,
                clinic.Name ?? clinic.Reference,
                link.Role,
                link.IsActive))
            .ToListAsync(cancellationToken);

        var ownedClinics = await db.Clinics.AsNoTracking()
            .Where(item => item.OwnerUserId == userId)
            .Select(item => new AdminUserClinicLink(item.Id, item.Name ?? item.Reference, RoleNames.ClinicAdmin, true))
            .ToListAsync(cancellationToken);

        var doctorClinics = doctor is null
            ? []
            : await db.DoctorClinicAffiliations.AsNoTracking()
                .Where(item => item.DoctorId == doctor.Id)
                .Join(db.Clinics.AsNoTracking(), link => link.ClinicId, clinic => clinic.Id, (_, clinic) => new AdminUserClinicLink(
                    clinic.Id,
                    clinic.Name ?? clinic.Reference,
                    RoleNames.Doctor,
                    true))
                .ToListAsync(cancellationToken);

        var assignedDoctors = await db.ReceptionistDoctorAssignments.AsNoTracking()
            .Where(item => item.ReceptionistUserId == userId && item.IsActive)
            .Join(db.Doctors.AsNoTracking(), link => link.DoctorId, doc => doc.Id, (link, doc) => new { link, doc })
            .Join(db.Clinics.AsNoTracking(), row => row.link.ClinicId, clinic => clinic.Id, (row, clinic) => new AdminUserDoctorLink(
                row.doc.Id,
                row.doc.DisplayName ?? ((row.doc.FirstName ?? "") + " " + (row.doc.LastName ?? "")).Trim(),
                clinic.Id,
                clinic.Name ?? clinic.Reference))
            .ToListAsync(cancellationToken);

        var clinics = staffClinics
            .Concat(ownedClinics)
            .Concat(doctorClinics)
            .GroupBy(item => new { item.ClinicId, item.Role })
            .Select(group => group.First())
            .ToList();

        return new AdminUserDetails(
            user.Id,
            user.PhoneNumber ?? string.Empty,
            patient?.FullName,
            user.Email,
            roles.ToArray(),
            user.CreatedAtUtc,
            user.SuspendedAtUtc,
            patient?.DeletionRequestedAtUtc,
            doctor?.Id,
            doctor?.Status.ToString(),
            specialization,
            qualifications,
            registration,
            doctor?.YearsOfExperience,
            clinics,
            assignedDoctors);
    }

    public async Task PermanentDeleteUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString())
            ?? throw DomainException.NotFound("account_not_found", "That account was not found.");
        if (user.PermanentlyDeletedAtUtc is not null)
        {
            throw DomainException.NotFound("account_not_found", "That account was not found.");
        }

        var roles = await users.GetRolesAsync(user);
        if (roles.Contains(RoleNames.PlatformAdmin))
        {
            throw DomainException.Validation("cannot_delete_admin", "Platform administrators cannot be deleted here.");
        }

        var originalPhone = user.PhoneNumber;

        await DeactivateClinicAccessAsync(userId, now, cancellationToken);

        var patient = await db.Patients.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        patient?.AnonymizeForPermanentDeletion(now);

        var doctor = await db.Doctors.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        doctor?.AnonymizeForPermanentDeletion(now);

        if (!string.IsNullOrWhiteSpace(originalPhone))
        {
            var invites = await db.StaffInvitations
                .Where(item => item.PhoneE164 == originalPhone
                    && item.Status != StaffInvitationStatus.Accepted
                    && item.Status != StaffInvitationStatus.Cancelled)
                .ToListAsync(cancellationToken);
            foreach (var invite in invites)
            {
                invite.Cancel(now);
            }
        }

        var devices = await db.PushDevices.Where(item => item.UserId == userId && item.IsActive).ToListAsync(cancellationToken);
        foreach (var device in devices)
        {
            device.Deactivate(now);
        }

        if (roles.Count > 0)
        {
            var roleResult = await users.RemoveFromRolesAsync(user, roles);
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException("Could not revoke roles for permanent deletion.");
            }
        }

        var tombstone = $"deleted_{userId:N}";
        user.UserName = tombstone;
        user.NormalizedUserName = tombstone.ToUpperInvariant();
        user.Email = null;
        user.NormalizedEmail = null;
        user.EmailConfirmed = false;
        user.PhoneNumber = null;
        user.PhoneNumberConfirmed = false;
        user.PasswordHash = null;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        user.SuspendedAtUtc = now;
        user.SuspensionReason = ApplicationUser.PermanentDeletionReason;
        user.PermanentlyDeletedAtUtc = now;

        var updateResult = await users.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            throw new InvalidOperationException("Could not scrub credentials for permanent deletion.");
        }
    }

    public async Task SuspendUserAsync(Guid userId, string? reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString())
            ?? throw DomainException.NotFound("account_not_found", "That account was not found.");
        if (user.PermanentlyDeletedAtUtc is not null)
        {
            throw DomainException.NotFound("account_not_found", "That account was not found.");
        }

        var roles = await users.GetRolesAsync(user);
        if (roles.Contains(RoleNames.PlatformAdmin))
        {
            throw DomainException.Validation("cannot_suspend_admin", "Platform administrators cannot be deactivated here.");
        }

        if (user.SuspendedAtUtc is not null)
        {
            throw DomainException.Conflict("account_already_inactive", "That account is already inactive.");
        }

        var normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? ApplicationUser.AccountDeactivationReason
            : reason.Trim()[..Math.Min(reason.Trim().Length, 240)];

        user.SuspendedAtUtc = now;
        user.SuspensionReason = normalizedReason;
        await users.UpdateAsync(user);

        await DeactivateClinicAccessAsync(userId, now, cancellationToken);

        var doctor = await db.Doctors.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        doctor?.SuspendForAccountDeactivation(ApplicationUser.AccountDeactivationReason, now);
    }

    public async Task ReactivateUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString())
            ?? throw DomainException.NotFound("account_not_found", "That account was not found.");
        if (user.PermanentlyDeletedAtUtc is not null)
        {
            throw DomainException.NotFound("account_not_found", "That account was not found.");
        }

        if (user.SuspendedAtUtc is null)
        {
            throw DomainException.Conflict("account_already_active", "That account is already active.");
        }

        user.SuspendedAtUtc = null;
        user.SuspensionReason = null;
        await users.UpdateAsync(user);

        var staff = await db.ClinicStaffMembers.Where(item => item.UserId == userId && !item.IsActive).ToListAsync(cancellationToken);
        foreach (var row in staff)
        {
            row.Reactivate(row.BranchId, row.Role, now);
        }

        var coverage = await db.ReceptionistDoctorAssignments
            .Where(item => item.ReceptionistUserId == userId && !item.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var row in coverage)
        {
            row.Reactivate(now);
        }

        var doctor = await db.Doctors.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        doctor?.ReinstateFromAccountDeactivation(ApplicationUser.AccountDeactivationReason, now);
    }

    private async Task DeactivateClinicAccessAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var staff = await db.ClinicStaffMembers.Where(item => item.UserId == userId && item.IsActive).ToListAsync(cancellationToken);
        foreach (var row in staff)
        {
            row.Deactivate(now);
        }

        var coverage = await db.ReceptionistDoctorAssignments
            .Where(item => item.ReceptionistUserId == userId && item.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var row in coverage)
        {
            row.Deactivate(now);
        }
    }

    public async Task RevokeRefreshTokensAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var tokens = await db.RefreshTokens.Where(item => item.UserId == userId && item.RevokedAtUtc == null).ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.Revoke(now, null);
        }
    }

    public async Task GrantRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString())
            ?? throw DomainException.NotFound("account_not_found", "That account was not found.");
        if (!await users.IsInRoleAsync(user, roleName))
        {
            var result = await users.AddToRoleAsync(user, roleName);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Could not grant role {roleName}.");
            }
        }
    }

    public async Task RevokeRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString())
            ?? throw DomainException.NotFound("account_not_found", "That account was not found.");
        if (await users.IsInRoleAsync(user, roleName))
        {
            var result = await users.RemoveFromRoleAsync(user, roleName);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Could not revoke role {roleName}.");
            }
        }
    }

    public async Task<IReadOnlyList<AuditEntryDetails>> ListAuditAsync(
        string? action,
        Guid? actorUserId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int take,
        CancellationToken cancellationToken)
    {
        var query = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(item => item.Action.Contains(action));
        }

        if (actorUserId is not null)
        {
            query = query.Where(item => item.ActorUserId == actorUserId);
        }

        if (from is not null)
        {
            query = query.Where(item => item.OccurredAtUtc >= from);
        }

        if (to is not null)
        {
            query = query.Where(item => item.OccurredAtUtc <= to);
        }

        return await query.OrderByDescending(item => item.OccurredAtUtc)
            .Take(take)
            .Select(item => new AuditEntryDetails(item.Id, item.ActorUserId, item.Action, item.OccurredAtUtc, item.IpAddress))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProviderDirectoryItem>> ListProvidersAsync(
        string kind,
        string? status,
        string? city,
        string? state,
        int take,
        CancellationToken cancellationToken)
    {
        if (string.Equals(kind, "clinic", StringComparison.OrdinalIgnoreCase))
        {
            var clinics = db.Clinics.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<VerificationStatus>(status, true, out var clinicStatus))
            {
                clinics = clinics.Where(item => item.Status == clinicStatus);
            }

            var rows = await clinics.Include(item => item.Branches).OrderByDescending(item => item.UpdatedAtUtc).Take(take).ToListAsync(cancellationToken);
            var result = new List<ProviderDirectoryItem>();
            foreach (var clinic in rows)
            {
                var branch = clinic.Branches.FirstOrDefault(item => item.IsPrimary) ?? clinic.Branches.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(city) && !string.Equals(branch?.City, city, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(state) && !string.Equals(branch?.State, state, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(new ProviderDirectoryItem(clinic.Id, "Clinic", clinic.Name ?? "Clinic", clinic.Status.ToString(), branch?.City, branch?.State, clinic.UpdatedAtUtc));
            }

            return result;
        }

        var doctors = db.Doctors.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<VerificationStatus>(status, true, out var doctorStatus))
        {
            doctors = doctors.Where(item => item.Status == doctorStatus);
        }

        var doctorRows = await doctors.OrderByDescending(item => item.UpdatedAtUtc).Take(take).ToListAsync(cancellationToken);
        return doctorRows
            .Select(item => new ProviderDirectoryItem(
                item.Id,
                "Doctor",
                string.IsNullOrWhiteSpace(item.DisplayName)
                    ? $"{item.FirstName} {item.LastName}".Trim()
                    : item.DisplayName!,
                item.Status.ToString(),
                null,
                null,
                item.UpdatedAtUtc))
            .ToArray();
    }

    public Task<SupportTicket?> FindTicketAsync(Guid ticketId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.SupportTickets.AsQueryable() : db.SupportTickets.AsNoTracking();
        return query.Include(item => item.Notes).SingleOrDefaultAsync(item => item.Id == ticketId, cancellationToken);
    }

    public async Task<IReadOnlyList<SupportTicket>> ListTicketsAsync(Guid? createdByUserId, SupportTicketStatus? status, int take, CancellationToken cancellationToken)
    {
        var query = db.SupportTickets.AsNoTracking().Include(item => item.Notes).AsQueryable();
        if (createdByUserId is not null)
        {
            query = query.Where(item => item.CreatedByUserId == createdByUserId);
        }

        if (status is not null)
        {
            query = query.Where(item => item.Status == status);
        }

        return await query.OrderByDescending(item => item.UpdatedAtUtc).Take(take).ToListAsync(cancellationToken);
    }

    public void AddTicket(SupportTicket ticket) => db.SupportTickets.Add(ticket);

    public void AddTicketNote(SupportTicketNote note) => db.SupportTicketNotes.Add(note);

    public async Task<string> NextTicketNumberAsync(CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"PRODOC-{year}-";
        var latest = await db.SupportTickets.AsNoTracking()
            .Where(item => item.Number.StartsWith(prefix))
            .OrderByDescending(item => item.Number)
            .Select(item => item.Number)
            .FirstOrDefaultAsync(cancellationToken);
        var next = 1;
        if (latest is not null && latest.Length > prefix.Length && int.TryParse(latest[prefix.Length..], out var parsed))
        {
            next = parsed + 1;
        }

        return $"{prefix}{next:D6}";
    }

    public async Task<IReadOnlyList<PlatformSetting>> ListSettingsAsync(CancellationToken cancellationToken) =>
        await db.PlatformSettings.AsNoTracking().OrderBy(item => item.Key).ToListAsync(cancellationToken);

    public Task<PlatformSetting?> FindSettingAsync(string key, bool tracking, CancellationToken cancellationToken)
    {
        var normalized = PlatformSetting.NormalizeKey(key);
        var query = tracking ? db.PlatformSettings.AsQueryable() : db.PlatformSettings.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Key == normalized, cancellationToken);
    }

    public void AddSetting(PlatformSetting setting) => db.PlatformSettings.Add(setting);

    public async Task<IReadOnlyList<FeatureFlag>> ListFlagsAsync(CancellationToken cancellationToken) =>
        await db.FeatureFlags.AsNoTracking().OrderBy(item => item.Key).ToListAsync(cancellationToken);

    public Task<FeatureFlag?> FindFlagAsync(string key, bool tracking, CancellationToken cancellationToken)
    {
        var normalized = PlatformSetting.NormalizeKey(key);
        var query = tracking ? db.FeatureFlags.AsQueryable() : db.FeatureFlags.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Key == normalized, cancellationToken);
    }

    public void AddFlag(FeatureFlag flag) => db.FeatureFlags.Add(flag);

    public async Task<IReadOnlyList<FinanceReportRow>> FinanceReportAsync(
        DateOnly? from,
        DateOnly? to,
        string? state,
        string? city,
        Guid? clinicId,
        CancellationToken cancellationToken)
    {
        var fromUtc = from is null ? (DateTimeOffset?)null : new DateTimeOffset(from.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toUtc = to is null ? (DateTimeOffset?)null : new DateTimeOffset(to.Value.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
        var query =
            from snapshot in db.CommissionSnapshots.AsNoTracking()
            join clinic in db.Clinics.AsNoTracking() on snapshot.ClinicId equals clinic.Id
            select new { snapshot, clinic };

        if (clinicId is not null)
        {
            query = query.Where(item => item.snapshot.ClinicId == clinicId);
        }

        if (fromUtc is not null)
        {
            query = query.Where(item => item.snapshot.CapturedAtUtc >= fromUtc);
        }

        if (toUtc is not null)
        {
            query = query.Where(item => item.snapshot.CapturedAtUtc <= toUtc);
        }

        var rows = await query.OrderByDescending(item => item.snapshot.CapturedAtUtc).Take(2000).ToListAsync(cancellationToken);
        var result = new List<FinanceReportRow>();
        foreach (var row in rows)
        {
            var branch = await db.ClinicBranches.AsNoTracking()
                .Where(item => item.ClinicId == row.clinic.Id)
                .OrderByDescending(item => item.IsPrimary)
                .FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(city) && !string.Equals(branch?.City, city, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(state) && !string.Equals(branch?.State, state, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(new FinanceReportRow(
                row.snapshot.AppointmentId,
                row.snapshot.ClinicId,
                row.clinic.Name,
                branch?.City,
                branch?.State,
                row.snapshot.ConsultationFee,
                row.snapshot.CommissionAmount,
                row.snapshot.ClinicPayableAmount,
                row.snapshot.CapturedAtUtc,
                row.snapshot.IsWalkInExcluded));
        }

        return result;
    }

    public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(audit);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}

internal static class AdminStoreText
{
    public static bool HasContent(this string? value) => !string.IsNullOrWhiteSpace(value);
}
