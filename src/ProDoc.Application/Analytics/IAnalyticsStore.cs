using ProDoc.Domain.Governance;
using ProDoc.Domain.Queue;

namespace ProDoc.Application.Analytics;

public interface IAnalyticsStore
{
    Task<int> CountPatientsAsync(CancellationToken cancellationToken);

    Task<int> CountVerifiedDoctorsAsync(CancellationToken cancellationToken);

    Task<int> CountClinicsAsync(CancellationToken cancellationToken);

    Task<int> CountNewPatientsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);

    Task<int> CountNewDoctorsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<AppointmentFactRow>> ListAppointmentFactsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AppointmentFactRow>> ListAppointmentFactsByStartAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);

    Task<(decimal Gross, decimal Commission, decimal Refunds, decimal PendingPayouts)> SumFinanceAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<(DateOnly Day, decimal Commission)>> CommissionByDayAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<(Guid DoctorId, string Name, string? PrimarySpecialization)>> ListDoctorsLiteAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<(Guid DoctorId, string SpecializationName)>> ListDoctorSpecializationsAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<QueueEntry>> ListQueueEntriesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record AppointmentFactRow(
    Guid Id,
    Guid ClinicId,
    string ClinicName,
    Guid BranchId,
    string BranchName,
    string? City,
    Guid DoctorId,
    string DoctorName,
    Guid AccountUserId,
    Domain.Appointments.AppointmentStatus Status,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset CreatedAtUtc,
    decimal ConsultationFee,
    bool IsWalkIn);
