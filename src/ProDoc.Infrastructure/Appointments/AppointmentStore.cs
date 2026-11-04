using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProDoc.Application.Appointments;
using ProDoc.Application.Scheduling;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.Appointments;

public sealed class AppointmentStore(ProDocDbContext db) : IAppointmentStore, ISlotOccupancy
{
    public Task<Appointment?> FindAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.Appointments : db.Appointments.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Appointment>> ListForAccountAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Appointments.AsNoTracking()
            .Where(item => item.AccountUserId == userId && !item.IsWalkIn && item.Status != AppointmentStatus.Held && item.Status != AppointmentStatus.Expired)
            .OrderByDescending(item => item.StartsAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Appointment>> ListForDoctorAsync(Guid doctorId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await InRange(from, to).Where(item => item.DoctorId == doctorId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Appointment>> ListForClinicsAsync(IReadOnlyCollection<Guid> clinicIds, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await InRange(from, to).Where(item => clinicIds.Contains(item.ClinicId)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Appointment>> ListForAdminAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await InRange(from, to).OrderByDescending(item => item.StartsAtUtc).Take(100).ToListAsync(cancellationToken);

    public async Task<string?> FindPatientNameAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Patients.AsNoTracking().Where(patient => patient.UserId == userId).Select(patient => patient.FullName).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<FamilyMember>> ListFamilyAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.FamilyMembers.AsNoTracking().Where(member => member.AccountUserId == userId).OrderBy(member => member.FullName).ToListAsync(cancellationToken);

    public Task<FamilyMember?> FindFamilyAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
        db.FamilyMembers.SingleOrDefaultAsync(member => member.Id == id && member.AccountUserId == userId, cancellationToken);

    public Task<int> CountFamilyAsync(Guid userId, CancellationToken cancellationToken) =>
        db.FamilyMembers.CountAsync(member => member.AccountUserId == userId, cancellationToken);

    public void AddFamily(FamilyMember member) => db.FamilyMembers.Add(member);

    public void RemoveFamily(FamilyMember member) => db.FamilyMembers.Remove(member);

    public async Task AddHoldAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await ExpireSlotAsync(appointment.DoctorId, appointment.BranchId, appointment.StartsAtUtc, appointment.CreatedAtUtc, cancellationToken, appointment.Id);
            db.Appointments.Add(appointment);
            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    public async Task SaveChangesAsync(AuditLog? audit, CancellationToken cancellationToken)
    {
        if (audit is not null)
        {
            db.Set<AuditLog>().Add(audit);
        }

        await SaveAsync(cancellationToken);
    }

    public async Task SaveMoveAsync(Appointment appointment, AuditLog? audit, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await ExpireSlotAsync(appointment.DoctorId, appointment.BranchId, appointment.StartsAtUtc, appointment.UpdatedAtUtc, cancellationToken, appointment.Id);
            if (audit is not null)
            {
                db.Set<AuditLog>().Add(audit);
            }

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    public async Task<IReadOnlySet<TimeOnly>> OccupiedLocalStartsAsync(
        Guid doctorId,
        Guid branchId,
        DateOnly date,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var rows = await db.Appointments.AsNoTracking()
            .Where(item => item.DoctorId == doctorId && item.BranchId == branchId && item.StartsAtUtc >= now.AddDays(-1) && item.StartsAtUtc < now.AddDays(61))
            .Where(item => item.Status == AppointmentStatus.Requested || item.Status == AppointmentStatus.Confirmed
                || item.Status == AppointmentStatus.CheckedIn || item.Status == AppointmentStatus.Completed
                || (item.Status == AppointmentStatus.Held && item.HoldExpiresAtUtc > now))
            .ToListAsync(cancellationToken);
        return rows
            .Select(item => Local(item.StartsAtUtc, item.TimeZoneId))
            .Where(local => DateOnly.FromDateTime(local) == date)
            .Select(local => TimeOnly.FromDateTime(local))
            .ToHashSet();
    }

    private IQueryable<Appointment> InRange(DateOnly from, DateOnly to)
    {
        var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(-1);
        var end = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero).AddDays(1);
        return db.Appointments.AsNoTracking()
            .Where(item => item.StartsAtUtc >= start && item.StartsAtUtc <= end && item.Status != AppointmentStatus.Held && item.Status != AppointmentStatus.Expired);
    }

    private async Task ExpireSlotAsync(Guid doctorId, Guid branchId, DateTimeOffset startsAtUtc, DateTimeOffset now, CancellationToken cancellationToken, Guid exceptId)
    {
        var rows = await db.Appointments
            .Where(item => item.Id != exceptId && item.DoctorId == doctorId && item.BranchId == branchId && item.StartsAtUtc == startsAtUtc)
            .Where(item => item.Status == AppointmentStatus.Held || item.Status == AppointmentStatus.Requested)
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            row.Expire(now);
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw DomainException.Conflict("concurrency_conflict", "This appointment was updated. Reload it and try again.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Number is 2601 or 2627)
        {
            throw DomainException.Conflict("slot_taken", "That time was just taken. Choose another.");
        }
    }

    private static DateTime Local(DateTimeOffset instant, string timeZoneId)
    {
        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var resolved) ? resolved : TimeZoneInfo.Utc;
        return TimeZoneInfo.ConvertTime(instant, zone).DateTime;
    }
}
