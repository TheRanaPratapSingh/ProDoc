using Microsoft.EntityFrameworkCore;
using ProDoc.Application.Analytics;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Billing;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Providers;
using ProDoc.Domain.Queue;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.Analytics;

public sealed class AnalyticsStore(ProDocDbContext db) : IAnalyticsStore
{
    public Task<int> CountPatientsAsync(CancellationToken cancellationToken) =>
        db.Patients.AsNoTracking().CountAsync(cancellationToken);

    public Task<int> CountVerifiedDoctorsAsync(CancellationToken cancellationToken) =>
        db.Doctors.AsNoTracking().CountAsync(item => item.Status == VerificationStatus.Approved, cancellationToken);

    public Task<int> CountClinicsAsync(CancellationToken cancellationToken) =>
        db.Clinics.AsNoTracking().CountAsync(cancellationToken);

    public Task<int> CountNewPatientsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken) =>
        db.Patients.AsNoTracking().CountAsync(item => item.CreatedAtUtc >= fromUtc && item.CreatedAtUtc <= toUtc, cancellationToken);

    public Task<int> CountNewDoctorsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken) =>
        db.Doctors.AsNoTracking().CountAsync(item => item.CreatedAtUtc >= fromUtc && item.CreatedAtUtc <= toUtc, cancellationToken);

    public Task<IReadOnlyList<AppointmentFactRow>> ListAppointmentFactsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken) =>
        QueryFacts(fromUtc, toUtc, byStart: false, cancellationToken);

    public Task<IReadOnlyList<AppointmentFactRow>> ListAppointmentFactsByStartAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken) =>
        QueryFacts(fromUtc, toUtc, byStart: true, cancellationToken);

    public async Task<(decimal Gross, decimal Commission, decimal Refunds, decimal PendingPayouts)> SumFinanceAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var gross = await db.Appointments.AsNoTracking()
            .Where(item => item.StartsAtUtc >= fromUtc && item.StartsAtUtc <= toUtc && !item.IsWalkIn)
            .Where(item => item.Status != AppointmentStatus.Held && item.Status != AppointmentStatus.Rejected)
            .SumAsync(item => (decimal?)item.ConsultationFee, cancellationToken) ?? 0m;

        var commission = await db.CommissionSnapshots.AsNoTracking()
            .Where(item => item.CapturedAtUtc >= fromUtc && item.CapturedAtUtc <= toUtc)
            .SumAsync(item => (decimal?)item.CommissionAmount, cancellationToken) ?? 0m;

        var refunds = await db.Refunds.AsNoTracking()
            .Where(item => item.Status == RefundStatus.Completed
                && item.CompletedAtUtc != null
                && item.CompletedAtUtc >= fromUtc
                && item.CompletedAtUtc <= toUtc)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

        var pending = await db.CommissionSnapshots.AsNoTracking()
            .Where(item => item.SettlementBatchId == null)
            .SumAsync(item => (decimal?)item.ClinicPayableAmount, cancellationToken) ?? 0m;

        return (gross, commission, refunds, pending);
    }

    public async Task<IReadOnlyList<(DateOnly Day, decimal Commission)>> CommissionByDayAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var rows = await db.CommissionSnapshots.AsNoTracking()
            .Where(item => item.CapturedAtUtc >= fromUtc && item.CapturedAtUtc <= toUtc)
            .Select(item => new { item.CapturedAtUtc, item.CommissionAmount })
            .ToListAsync(cancellationToken);
        return rows
            .GroupBy(item => DateOnly.FromDateTime(item.CapturedAtUtc.UtcDateTime))
            .OrderBy(group => group.Key)
            .Select(group => (group.Key, group.Sum(item => item.CommissionAmount)))
            .ToArray();
    }

    public async Task<IReadOnlyList<(Guid DoctorId, string Name, string? PrimarySpecialization)>> ListDoctorsLiteAsync(
        CancellationToken cancellationToken)
    {
        var doctors = await db.Doctors.AsNoTracking()
            .Select(item => new { item.Id, item.DisplayName })
            .ToListAsync(cancellationToken);
        var specs = await ListDoctorSpecializationsAsync(cancellationToken);
        var primary = specs.GroupBy(item => item.DoctorId).ToDictionary(group => group.Key, group => group.First().SpecializationName);
        return doctors
            .Select(item => (item.Id, item.DisplayName ?? "Doctor", primary.TryGetValue(item.Id, out var name) ? name : null))
            .ToArray();
    }

    public async Task<IReadOnlyList<(Guid DoctorId, string SpecializationName)>> ListDoctorSpecializationsAsync(
        CancellationToken cancellationToken)
    {
        var rows = await (
            from link in db.Set<DoctorSpecialization>().AsNoTracking()
            join spec in db.Specializations.AsNoTracking() on link.SpecializationId equals spec.Id
            orderby link.IsPrimary descending, spec.Name
            select new { link.DoctorId, spec.Name }).ToListAsync(cancellationToken);
        return rows.Select(item => (item.DoctorId, item.Name)).ToArray();
    }

    public async Task<IReadOnlyList<QueueEntry>> ListQueueEntriesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await db.QueueEntries.AsNoTracking()
            .Where(item => item.OperatingDate >= from && item.OperatingDate <= to)
            .ToListAsync(cancellationToken);

    public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(audit);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    private async Task<IReadOnlyList<AppointmentFactRow>> QueryFacts(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        bool byStart,
        CancellationToken cancellationToken)
    {
        var query =
            from appointment in db.Appointments.AsNoTracking()
            join clinic in db.Clinics.AsNoTracking() on appointment.ClinicId equals clinic.Id
            join branch in db.ClinicBranches.AsNoTracking() on appointment.BranchId equals branch.Id
            join doctor in db.Doctors.AsNoTracking() on appointment.DoctorId equals doctor.Id
            select new { appointment, clinic, branch, doctor };

        query = byStart
            ? query.Where(item => item.appointment.StartsAtUtc >= fromUtc && item.appointment.StartsAtUtc <= toUtc)
            : query.Where(item => item.appointment.CreatedAtUtc >= fromUtc && item.appointment.CreatedAtUtc <= toUtc);

        var rows = await query.Select(item => new AppointmentFactRow(
            item.appointment.Id,
            item.appointment.ClinicId,
            item.clinic.Name ?? "Clinic",
            item.appointment.BranchId,
            item.branch.Name ?? "Branch",
            item.branch.City,
            item.appointment.DoctorId,
            item.doctor.DisplayName ?? "Doctor",
            item.appointment.AccountUserId,
            item.appointment.Status,
            item.appointment.StartsAtUtc,
            item.appointment.CreatedAtUtc,
            item.appointment.ConsultationFee,
            item.appointment.IsWalkIn)).ToListAsync(cancellationToken);
        return rows;
    }
}
