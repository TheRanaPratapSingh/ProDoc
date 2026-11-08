using Microsoft.EntityFrameworkCore;
using ProDoc.Application.Clinical;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Clinical;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Providers;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.Clinical;

public sealed class FollowUpStore(ProDocDbContext db) : IFollowUpStore
{
    public Task<Doctor?> FindDoctorByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Doctors.Include(doctor => doctor.Registrations).SingleOrDefaultAsync(doctor => doctor.UserId == userId, cancellationToken);

    public Task<Doctor?> FindDoctorAsync(Guid doctorId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.Doctors.AsQueryable() : db.Doctors.AsNoTracking();
        return query.Include(doctor => doctor.Registrations).SingleOrDefaultAsync(doctor => doctor.Id == doctorId, cancellationToken);
    }

    public Task<Clinic?> FindClinicAsync(Guid clinicId, CancellationToken cancellationToken) =>
        db.Clinics.AsNoTracking().SingleOrDefaultAsync(clinic => clinic.Id == clinicId, cancellationToken);

    public Task<ClinicBranch?> FindBranchAsync(Guid branchId, CancellationToken cancellationToken) =>
        db.ClinicBranches.AsNoTracking().SingleOrDefaultAsync(branch => branch.Id == branchId, cancellationToken);

    public Task<Appointment?> FindAppointmentAsync(Guid appointmentId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.Appointments.AsQueryable() : db.Appointments.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == appointmentId, cancellationToken);
    }

    public Task<ConsultationRecord?> FindConsultationAsync(Guid consultationId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.ConsultationRecords.AsQueryable() : db.ConsultationRecords.AsNoTracking();
        return query.Include(item => item.Diagnoses).SingleOrDefaultAsync(item => item.Id == consultationId, cancellationToken);
    }

    public async Task<string?> FindDoctorDisplayNameAsync(Guid doctorId, CancellationToken cancellationToken)
    {
        var doctor = await db.Doctors.AsNoTracking().SingleOrDefaultAsync(item => item.Id == doctorId, cancellationToken);
        if (doctor is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(doctor.DisplayName))
        {
            return doctor.DisplayName;
        }

        return $"Dr. {doctor.FirstName} {doctor.LastName}".Trim();
    }

    public async Task<string?> FindClinicNameAsync(Guid clinicId, CancellationToken cancellationToken) =>
        await db.Clinics.AsNoTracking()
            .Where(item => item.Id == clinicId)
            .Select(item => (string?)item.Name)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<PatientFollowUp?> FindAsync(Guid followUpId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.PatientFollowUps.AsQueryable() : db.PatientFollowUps.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == followUpId, cancellationToken);
    }

    public Task<PatientFollowUp?> FindOpenForSourceConsultationAsync(Guid consultationId, CancellationToken cancellationToken) =>
        db.PatientFollowUps
            .Where(item => item.SourceConsultationId == consultationId
                && (item.Status == FollowUpStatus.Recommended || item.Status == FollowUpStatus.AppointmentBooked))
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PatientFollowUp>> ListForDoctorAsync(
        Guid doctorId,
        DateOnly? from,
        DateOnly? to,
        string? patientQuery,
        string? status,
        Guid? clinicId,
        int take,
        CancellationToken cancellationToken)
    {
        var query = db.PatientFollowUps.AsNoTracking().Where(item => item.DoctorId == doctorId);
        query = ApplyFilters(query, from, to, patientQuery, status, clinicId);
        return await query
            .OrderBy(item => item.RecommendedDate)
            .ThenByDescending(item => item.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PatientFollowUp>> ListForPatientAsync(Guid patientUserId, CancellationToken cancellationToken) =>
        await db.PatientFollowUps.AsNoTracking()
            .Where(item => item.PatientUserId == patientUserId)
            .OrderBy(item => item.RecommendedDate)
            .ThenByDescending(item => item.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PatientFollowUp>> ListForReceptionAsync(
        IReadOnlyList<Guid> doctorIds,
        IReadOnlyList<Guid> clinicIds,
        DateOnly? from,
        DateOnly? to,
        string? patientQuery,
        string? status,
        int take,
        CancellationToken cancellationToken)
    {
        var query = db.PatientFollowUps.AsNoTracking().Where(item => clinicIds.Contains(item.ClinicId));
        if (doctorIds.Count > 0)
        {
            query = query.Where(item => doctorIds.Contains(item.DoctorId));
        }

        query = ApplyFilters(query, from, to, patientQuery, status, clinicId: null);
        return await query
            .OrderBy(item => item.RecommendedDate)
            .ThenByDescending(item => item.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FollowUpReminder>> ListRemindersAsync(Guid followUpId, CancellationToken cancellationToken) =>
        await db.FollowUpReminders.AsNoTracking()
            .Where(item => item.FollowUpId == followUpId)
            .OrderBy(item => item.ScheduledAtUtc)
            .ToListAsync(cancellationToken);

    public Task<FollowUpReminder?> FindReminderByKeyAsync(string idempotencyKey, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.FollowUpReminders.AsQueryable() : db.FollowUpReminders.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public void Add(PatientFollowUp followUp) => db.PatientFollowUps.Add(followUp);

    public void AddReminder(FollowUpReminder reminder) => db.FollowUpReminders.Add(reminder);

    public async Task CancelPendingRemindersAsync(
        Guid followUpId,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var rows = await db.FollowUpReminders
            .Where(item => item.FollowUpId == followUpId && item.DeliveryStatus == FollowUpReminderDeliveryStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            row.Cancel(reason, now);
        }
    }

    public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(audit);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    private static IQueryable<PatientFollowUp> ApplyFilters(
        IQueryable<PatientFollowUp> query,
        DateOnly? from,
        DateOnly? to,
        string? patientQuery,
        string? status,
        Guid? clinicId)
    {
        if (from is DateOnly fromDate)
        {
            query = query.Where(item => item.RecommendedDate >= fromDate);
        }

        if (to is DateOnly toDate)
        {
            query = query.Where(item => item.RecommendedDate <= toDate);
        }

        if (clinicId is Guid clinic)
        {
            query = query.Where(item => item.ClinicId == clinic);
        }

        if (!string.IsNullOrWhiteSpace(patientQuery))
        {
            var term = patientQuery.Trim().ToLowerInvariant();
            query = query.Where(item => item.PatientName.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim();
            if (Enum.TryParse<FollowUpStatus>(normalized.Replace(" ", string.Empty), true, out var stored))
            {
                query = query.Where(item => item.Status == stored);
            }
            else if (normalized.Equals("Due", StringComparison.OrdinalIgnoreCase)
                     || normalized.Equals("Overdue", StringComparison.OrdinalIgnoreCase)
                     || normalized.Equals("Appointment Booked", StringComparison.OrdinalIgnoreCase)
                     || normalized.Equals("Recommended", StringComparison.OrdinalIgnoreCase)
                     || normalized.Equals("Completed", StringComparison.OrdinalIgnoreCase)
                     || normalized.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                // Display statuses Due/Overdue are resolved in the service layer after fetch;
                // filter open rows broadly when a display status is requested.
                if (normalized.Equals("Due", StringComparison.OrdinalIgnoreCase)
                    || normalized.Equals("Overdue", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(item =>
                        item.Status == FollowUpStatus.Recommended || item.Status == FollowUpStatus.AppointmentBooked);
                }
                else if (normalized.Equals("Appointment Booked", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(item => item.Status == FollowUpStatus.AppointmentBooked);
                }
                else if (normalized.Equals("Recommended", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(item => item.Status == FollowUpStatus.Recommended);
                }
                else if (normalized.Equals("Completed", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(item => item.Status == FollowUpStatus.Completed);
                }
                else if (normalized.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(item => item.Status == FollowUpStatus.Cancelled);
                }
            }
        }

        return query;
    }
}
