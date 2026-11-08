using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using ProDoc.Application.Queue;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Providers;
using ProDoc.Domain.Queue;
using ProDoc.Infrastructure.Identity;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.Queue;

public sealed class QueueStore(ProDocDbContext db, UserManager<ApplicationUser> users) : IQueueStore
{
    public Task<ClinicStaffMember?> FindStaffAsync(Guid clinicId, Guid userId, CancellationToken cancellationToken) =>
        db.ClinicStaffMembers.SingleOrDefaultAsync(item => item.ClinicId == clinicId && item.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<ClinicStaffMember>> ListStaffAsync(Guid clinicId, CancellationToken cancellationToken) =>
        await db.ClinicStaffMembers.AsNoTracking().Where(item => item.ClinicId == clinicId).OrderBy(item => item.CreatedAtUtc).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ClinicStaffMember>> ListStaffForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.ClinicStaffMembers.AsNoTracking().Where(item => item.UserId == userId && item.IsActive).ToListAsync(cancellationToken);

    public void AddStaff(ClinicStaffMember member) => db.ClinicStaffMembers.Add(member);

    public Task<Clinic?> FindClinicAsync(Guid clinicId, CancellationToken cancellationToken) =>
        db.Clinics.AsNoTracking().Include(clinic => clinic.Branches).SingleOrDefaultAsync(clinic => clinic.Id == clinicId, cancellationToken);

    public async Task<IReadOnlyList<(Guid DoctorId, string DisplayName, int YearsOfExperience)>> ListClinicDoctorsAsync(
        Guid clinicId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var rows = await db.DoctorClinicAffiliations.AsNoTracking()
            .Where(item => item.ClinicId == clinicId && (item.BranchId == null || item.BranchId == branchId))
            .Join(db.Doctors.AsNoTracking(), affiliation => affiliation.DoctorId, doctor => doctor.Id, (affiliation, doctor) => doctor)
            .Distinct()
            .Select(doctor => new { doctor.Id, doctor.DisplayName, doctor.YearsOfExperience })
            .ToListAsync(cancellationToken);
        return rows.Select(row => (row.Id, row.DisplayName ?? "Doctor", row.YearsOfExperience)).ToArray();
    }

    public async Task<IReadOnlyList<Guid>> ListReceptionistAssignedDoctorIdsAsync(
        Guid receptionistUserId,
        Guid clinicId,
        CancellationToken cancellationToken)
    {
        return await db.ReceptionistDoctorAssignments.AsNoTracking()
            .Where(item =>
                item.ReceptionistUserId == receptionistUserId
                && item.ClinicId == clinicId
                && item.IsActive)
            .Select(item => item.DoctorId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid?> FindUserIdByPhoneAsync(string normalizedPhone, CancellationToken cancellationToken)
    {
        var user = await users.Users.AsNoTracking().SingleOrDefaultAsync(item => item.NormalizedUserName == normalizedPhone, cancellationToken);
        return user?.Id;
    }

    public async Task GrantRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString())
            ?? throw DomainException.NotFound("account_not_found", "Account was not found.");
        if (await users.IsInRoleAsync(user, roleName))
        {
            return;
        }

        var result = await users.AddToRoleAsync(user, roleName);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Could not grant the {roleName} role.");
        }
    }

    public Task<ClinicQueueSettings?> FindSettingsAsync(Guid clinicId, Guid branchId, CancellationToken cancellationToken) =>
        db.ClinicQueueSettings.SingleOrDefaultAsync(item => item.ClinicId == clinicId && item.BranchId == branchId, cancellationToken);

    public void AddSettings(ClinicQueueSettings settings) => db.ClinicQueueSettings.Add(settings);

    public Task<ClinicQueueSession?> FindSessionAsync(Guid clinicId, Guid branchId, Guid doctorId, DateOnly date, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.ClinicQueueSessions : db.ClinicQueueSessions.AsNoTracking();
        return query.SingleOrDefaultAsync(
            item => item.ClinicId == clinicId && item.BranchId == branchId && item.DoctorId == doctorId && item.OperatingDate == date,
            cancellationToken);
    }

    public void AddSession(ClinicQueueSession session) => db.ClinicQueueSessions.Add(session);

    public Task<QueueTokenCounter?> FindCounterAsync(
        Guid clinicId,
        Guid branchId,
        Guid doctorId,
        DateOnly date,
        CancellationToken cancellationToken,
        int rangeStartMinutes = QueueTokenCounter.DayWideRangeMinutes) =>
        db.QueueTokenCounters.SingleOrDefaultAsync(
            item => item.ClinicId == clinicId
                && item.BranchId == branchId
                && item.DoctorId == doctorId
                && item.OperatingDate == date
                && item.RangeStartMinutes == rangeStartMinutes,
            cancellationToken);

    public void AddCounter(QueueTokenCounter counter) => db.QueueTokenCounters.Add(counter);

    public Task<QueueEntry?> FindEntryAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.QueueEntries : db.QueueEntries.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public Task<QueueEntry?> FindActiveByAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken) =>
        db.QueueEntries.SingleOrDefaultAsync(
            item => item.AppointmentId == appointmentId && (item.Status == QueueEntryStatus.Waiting || item.Status == QueueEntryStatus.Called
                || item.Status == QueueEntryStatus.InConsultation || item.Status == QueueEntryStatus.OnHold),
            cancellationToken);

    public async Task<IReadOnlyList<QueueEntry>> ListDoctorQueueAsync(Guid doctorId, Guid branchId, DateOnly date, CancellationToken cancellationToken) =>
        await db.QueueEntries.AsNoTracking()
            .Where(item => item.DoctorId == doctorId && item.BranchId == branchId && item.OperatingDate == date)
            .OrderBy(item => item.QueuePosition)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<QueueEntry>> ListBranchQueueAsync(Guid branchId, DateOnly date, CancellationToken cancellationToken) =>
        await db.QueueEntries.AsNoTracking()
            .Where(item => item.BranchId == branchId && item.OperatingDate == date)
            .OrderBy(item => item.DoctorId)
            .ThenBy(item => item.QueuePosition)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<QueueEntry>> ListPatientQueueAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.QueueEntries.AsNoTracking()
            .Where(item => item.AccountUserId == userId && item.Status != QueueEntryStatus.Cancelled && item.Status != QueueEntryStatus.Completed)
            .OrderByDescending(item => item.CheckInAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

    public async Task<int> CountActiveAheadAsync(QueueEntry entry, CancellationToken cancellationToken) =>
        await db.QueueEntries.CountAsync(
            item => item.DoctorId == entry.DoctorId
                && item.BranchId == entry.BranchId
                && item.OperatingDate == entry.OperatingDate
                && item.QueuePosition < entry.QueuePosition
                && (item.Status == QueueEntryStatus.Waiting || item.Status == QueueEntryStatus.Called || item.Status == QueueEntryStatus.InConsultation || item.Status == QueueEntryStatus.OnHold),
            cancellationToken);

    public async Task<double?> AverageConsultationMinutesAsync(Guid doctorId, Guid branchId, CancellationToken cancellationToken)
    {
        var completed = await db.QueueEntries.AsNoTracking()
            .Where(item => item.DoctorId == doctorId && item.BranchId == branchId && item.Status == QueueEntryStatus.Completed
                && item.ConsultationStartedAtUtc != null && item.ConsultationCompletedAtUtc != null)
            .OrderByDescending(item => item.ConsultationCompletedAtUtc)
            .Take(30)
            .Select(item => new { item.ConsultationStartedAtUtc, item.ConsultationCompletedAtUtc })
            .ToListAsync(cancellationToken);
        if (completed.Count == 0)
        {
            return null;
        }

        return completed.Average(item => (item.ConsultationCompletedAtUtc!.Value - item.ConsultationStartedAtUtc!.Value).TotalMinutes);
    }

    public void AddEntry(QueueEntry entry) => db.QueueEntries.Add(entry);

    public void AddHistory(QueueStatusHistory history) => db.QueueStatusHistory.Add(history);

    public Task<AppointmentCheckInToken?> FindCheckInTokenByHashAsync(string hash, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.AppointmentCheckInTokens : db.AppointmentCheckInTokens.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.TokenHash == hash, cancellationToken);
    }

    public async Task<IReadOnlyList<AppointmentCheckInToken>> ListActiveTokensAsync(Guid appointmentId, CancellationToken cancellationToken) =>
        await db.AppointmentCheckInTokens
            .Where(item => item.AppointmentId == appointmentId && item.UsedAtUtc == null && item.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

    public void AddCheckInToken(AppointmentCheckInToken token) => db.AppointmentCheckInTokens.Add(token);

    public void AddWalkIn(WalkInRecord record) => db.WalkInRecords.Add(record);

    public async Task<IReadOnlyList<WalkInRecord>> ListWalkInsAsync(Guid clinicId, Guid branchId, DateOnly date, CancellationToken cancellationToken)
    {
        var start = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddHours(-14);
        var end = start.AddDays(2);
        return await db.WalkInRecords.AsNoTracking()
            .Where(item => item.ClinicId == clinicId && item.BranchId == branchId && item.CreatedAtUtc >= start && item.CreatedAtUtc < end)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);
    }

    public Task<Appointment?> FindAppointmentAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.Appointments : db.Appointments.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Appointment>> SearchAppointmentsAsync(
        Guid clinicId,
        Guid branchId,
        string? reference,
        string? patientName,
        string? phone,
        Guid? doctorId,
        DateOnly? date,
        CancellationToken cancellationToken)
    {
        var query = db.Appointments.AsNoTracking().Where(item => item.ClinicId == clinicId && item.BranchId == branchId
            && (item.Status == AppointmentStatus.Confirmed || item.Status == AppointmentStatus.CheckedIn));
        if (!string.IsNullOrWhiteSpace(reference))
        {
            var value = reference.Trim();
            query = query.Where(item => item.Reference == value);
        }

        if (!string.IsNullOrWhiteSpace(patientName))
        {
            var value = patientName.Trim();
            query = query.Where(item => item.PatientName.Contains(value));
        }

        if (doctorId is Guid doctor)
        {
            query = query.Where(item => item.DoctorId == doctor);
        }

        if (date is DateOnly day)
        {
            var start = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(-1);
            var end = start.AddDays(3);
            query = query.Where(item => item.StartsAtUtc >= start && item.StartsAtUtc <= end);
        }

        Guid? phoneUserId = null;
        if (!string.IsNullOrWhiteSpace(phone))
        {
            var parsed = PhoneNumber.ParseIndianMobile(phone);
            phoneUserId = await FindUserIdByPhoneAsync(parsed.E164.ToUpperInvariant(), cancellationToken);
            if (phoneUserId is null)
            {
                return [];
            }

            query = query.Where(item => item.AccountUserId == phoneUserId);
        }

        var rows = await query.OrderBy(item => item.StartsAtUtc).Take(50).ToListAsync(cancellationToken);
        if (date is DateOnly filterDay)
        {
            rows = rows.Where(item => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(item.StartsAtUtc, Zone(item.TimeZoneId)).DateTime) == filterDay).ToList();
        }

        return rows;
    }

    public Task NotifyAsync(UserNotification notification, CancellationToken cancellationToken)
    {
        db.UserNotifications.Add(notification);
        return Task.CompletedTask;
    }

    public Task<bool> HasRecentNotificationAsync(Guid userId, string kind, Guid? subjectId, DateTimeOffset since, CancellationToken cancellationToken) =>
        db.UserNotifications.AnyAsync(
            item => item.UserId == userId && item.Kind == kind && item.SubjectId == subjectId && item.CreatedAtUtc >= since,
            cancellationToken);

    public async Task<IReadOnlyList<UserNotification>> ListNotificationsAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.UserNotifications.AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

    public Task AddAuditAsync(AuditLog log, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(log);
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
            throw DomainException.Conflict("concurrency_conflict", "This queue was updated. Reload it and try again.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Number is 2601 or 2627)
        {
            throw DomainException.Conflict("queue_conflict", "That queue change collided with another update. Try again.");
        }
    }

    public Task SaveTokenIssueAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<QueueEntry>> ListEntriesBetweenAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await db.QueueEntries.AsNoTracking()
            .Where(item => item.OperatingDate >= from && item.OperatingDate <= to)
            .OrderBy(item => item.OperatingDate)
            .ThenBy(item => item.ClinicId)
            .Take(2000)
            .ToListAsync(cancellationToken);

    public static string HashToken(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    private static TimeZoneInfo Zone(string timeZoneId) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone) ? zone : TimeZoneInfo.Utc;
}
