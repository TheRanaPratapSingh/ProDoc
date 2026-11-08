using ProDoc.Domain.Appointments;
using ProDoc.Domain.Clinical;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Providers;

namespace ProDoc.Application.Clinical;

public interface IFollowUpStore
{
    Task<Doctor?> FindDoctorByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<Doctor?> FindDoctorAsync(Guid doctorId, bool tracking, CancellationToken cancellationToken);

    Task<Clinic?> FindClinicAsync(Guid clinicId, CancellationToken cancellationToken);

    Task<ClinicBranch?> FindBranchAsync(Guid branchId, CancellationToken cancellationToken);

    Task<Appointment?> FindAppointmentAsync(Guid appointmentId, bool tracking, CancellationToken cancellationToken);

    Task<ConsultationRecord?> FindConsultationAsync(Guid consultationId, bool tracking, CancellationToken cancellationToken);

    Task<string?> FindDoctorDisplayNameAsync(Guid doctorId, CancellationToken cancellationToken);

    Task<string?> FindClinicNameAsync(Guid clinicId, CancellationToken cancellationToken);

    Task<PatientFollowUp?> FindAsync(Guid followUpId, bool tracking, CancellationToken cancellationToken);

    Task<PatientFollowUp?> FindOpenForSourceConsultationAsync(Guid consultationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PatientFollowUp>> ListForDoctorAsync(
        Guid doctorId,
        DateOnly? from,
        DateOnly? to,
        string? patientQuery,
        string? status,
        Guid? clinicId,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PatientFollowUp>> ListForPatientAsync(Guid patientUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PatientFollowUp>> ListForReceptionAsync(
        IReadOnlyList<Guid> doctorIds,
        IReadOnlyList<Guid> clinicIds,
        DateOnly? from,
        DateOnly? to,
        string? patientQuery,
        string? status,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FollowUpReminder>> ListRemindersAsync(Guid followUpId, CancellationToken cancellationToken);

    Task<FollowUpReminder?> FindReminderByKeyAsync(string idempotencyKey, bool tracking, CancellationToken cancellationToken);

    void Add(PatientFollowUp followUp);

    void AddReminder(FollowUpReminder reminder);

    Task CancelPendingRemindersAsync(Guid followUpId, string reason, DateTimeOffset now, CancellationToken cancellationToken);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
