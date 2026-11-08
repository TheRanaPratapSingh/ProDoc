using ProDoc.Domain.Appointments;
using ProDoc.Domain.Governance;

namespace ProDoc.Application.Appointments;

public interface IAppointmentStore
{
    Task<Appointment?> FindAsync(Guid id, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<Appointment>> ListForAccountAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Appointment>> ListForDoctorAsync(Guid doctorId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<IReadOnlyList<Appointment>> ListForClinicsAsync(IReadOnlyCollection<Guid> clinicIds, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<IReadOnlyList<Appointment>> ListForAdminAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>
    /// Directory-style clinic visit lookup for reception patient search.
    /// Not calendar-range bound; limited by <paramref name="take"/> and ordered by most recent visit.
    /// </summary>
    Task<IReadOnlyList<Appointment>> SearchClinicPatientVisitsAsync(
        Guid clinicId,
        Guid? branchId,
        string? query,
        Guid? accountUserId,
        int take,
        CancellationToken cancellationToken);

    Task<string?> FindPatientNameAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<FamilyMember>> ListFamilyAsync(Guid userId, CancellationToken cancellationToken);

    Task<FamilyMember?> FindFamilyAsync(Guid id, Guid userId, CancellationToken cancellationToken);

    Task<int> CountFamilyAsync(Guid userId, CancellationToken cancellationToken);

    void AddFamily(FamilyMember member);

    void RemoveFamily(FamilyMember member);

    Task AddHoldAsync(Appointment appointment, CancellationToken cancellationToken);

    /// <summary>
    /// Active bookings for the same account + doctor on a clinic-local calendar day.
    /// Excludes cancelled/completed/expired and expired holds.
    /// </summary>
    Task<IReadOnlyList<Appointment>> ListActiveForDuplicateCheckAsync(
        Guid accountUserId,
        Guid doctorId,
        DateOnly clinicLocalDate,
        DateTimeOffset now,
        Guid? exceptAppointmentId,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(AuditLog? audit, CancellationToken cancellationToken);

    Task SaveMoveAsync(Appointment appointment, AuditLog? audit, CancellationToken cancellationToken);

    Task<AppointmentShareLink?> FindShareLinkByHashAsync(string tokenHash, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<AppointmentShareLink>> ListShareLinksAsync(Guid appointmentId, CancellationToken cancellationToken);

    void AddShareLink(AppointmentShareLink link);
}
