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

    Task<string?> FindPatientNameAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<FamilyMember>> ListFamilyAsync(Guid userId, CancellationToken cancellationToken);

    Task<FamilyMember?> FindFamilyAsync(Guid id, Guid userId, CancellationToken cancellationToken);

    Task<int> CountFamilyAsync(Guid userId, CancellationToken cancellationToken);

    void AddFamily(FamilyMember member);

    void RemoveFamily(FamilyMember member);

    Task AddHoldAsync(Appointment appointment, CancellationToken cancellationToken);

    Task SaveChangesAsync(AuditLog? audit, CancellationToken cancellationToken);

    Task SaveMoveAsync(Appointment appointment, AuditLog? audit, CancellationToken cancellationToken);
}
