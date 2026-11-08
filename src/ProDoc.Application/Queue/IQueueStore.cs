using ProDoc.Domain.Appointments;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Providers;
using ProDoc.Domain.Queue;

namespace ProDoc.Application.Queue;

public interface IQueueStore
{
    Task<ClinicStaffMember?> FindStaffAsync(Guid clinicId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ClinicStaffMember>> ListStaffAsync(Guid clinicId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ClinicStaffMember>> ListStaffForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<Clinic?> FindClinicAsync(Guid clinicId, CancellationToken cancellationToken);

    Task<IReadOnlyList<(Guid DoctorId, string DisplayName, int YearsOfExperience)>> ListClinicDoctorsAsync(Guid clinicId, Guid branchId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> ListReceptionistAssignedDoctorIdsAsync(Guid receptionistUserId, Guid clinicId, CancellationToken cancellationToken);

    void AddStaff(ClinicStaffMember member);

    Task<Guid?> FindUserIdByPhoneAsync(string normalizedPhone, CancellationToken cancellationToken);

    Task GrantRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken);

    Task<ClinicQueueSettings?> FindSettingsAsync(Guid clinicId, Guid branchId, CancellationToken cancellationToken);

    void AddSettings(ClinicQueueSettings settings);

    Task<ClinicQueueSession?> FindSessionAsync(Guid clinicId, Guid branchId, Guid doctorId, DateOnly date, bool tracking, CancellationToken cancellationToken);

    void AddSession(ClinicQueueSession session);

    Task<QueueTokenCounter?> FindCounterAsync(
        Guid clinicId,
        Guid branchId,
        Guid doctorId,
        DateOnly date,
        CancellationToken cancellationToken,
        int rangeStartMinutes = QueueTokenCounter.DayWideRangeMinutes);

    void AddCounter(QueueTokenCounter counter);

    Task<QueueEntry?> FindEntryAsync(Guid id, bool tracking, CancellationToken cancellationToken);

    Task<QueueEntry?> FindActiveByAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<QueueEntry>> ListDoctorQueueAsync(Guid doctorId, Guid branchId, DateOnly date, CancellationToken cancellationToken);

    Task<IReadOnlyList<QueueEntry>> ListBranchQueueAsync(Guid branchId, DateOnly date, CancellationToken cancellationToken);

    Task<IReadOnlyList<QueueEntry>> ListPatientQueueAsync(Guid userId, CancellationToken cancellationToken);

    Task<int> CountActiveAheadAsync(QueueEntry entry, CancellationToken cancellationToken);

    Task<double?> AverageConsultationMinutesAsync(Guid doctorId, Guid branchId, CancellationToken cancellationToken);

    void AddEntry(QueueEntry entry);

    void AddHistory(QueueStatusHistory history);

    Task<AppointmentCheckInToken?> FindCheckInTokenByHashAsync(string hash, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<AppointmentCheckInToken>> ListActiveTokensAsync(Guid appointmentId, CancellationToken cancellationToken);

    void AddCheckInToken(AppointmentCheckInToken token);

    void AddWalkIn(WalkInRecord record);

    Task<IReadOnlyList<WalkInRecord>> ListWalkInsAsync(Guid clinicId, Guid branchId, DateOnly date, CancellationToken cancellationToken);

    Task<Appointment?> FindAppointmentAsync(Guid id, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<Appointment>> SearchAppointmentsAsync(
        Guid clinicId,
        Guid branchId,
        string? reference,
        string? patientName,
        string? phone,
        Guid? doctorId,
        DateOnly? date,
        CancellationToken cancellationToken);

    Task NotifyAsync(UserNotification notification, CancellationToken cancellationToken);

    Task<bool> HasRecentNotificationAsync(Guid userId, string kind, Guid? subjectId, DateTimeOffset since, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserNotification>> ListNotificationsAsync(Guid userId, CancellationToken cancellationToken);

    Task AddAuditAsync(AuditLog log, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task SaveTokenIssueAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<QueueEntry>> ListEntriesBetweenAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

public interface IClinicAccess
{
    Task EnsureClinicOwnerAsync(Guid userId, Guid clinicId, CancellationToken cancellationToken);

    Task EnsureQueueAccessAsync(Guid userId, Guid clinicId, Guid branchId, CancellationToken cancellationToken);

    Task EnsureDoctorQueueAsync(Guid userId, Guid doctorId, Guid clinicId, Guid branchId, CancellationToken cancellationToken);

    /// <summary>
    /// Ensures the user may act for the doctor at the clinic branch (owner, affiliated doctor, or staff with coverage).
    /// </summary>
    Task EnsureDoctorCoverageAsync(Guid userId, Guid clinicId, Guid branchId, Guid doctorId, CancellationToken cancellationToken);

    /// <summary>
    /// True when the user is active clinic staff (any branch) for the clinic.
    /// </summary>
    Task<bool> HasClinicStaffAccessAsync(Guid userId, Guid clinicId, CancellationToken cancellationToken);

    /// <summary>
    /// Empty list = all affiliated doctors (legacy). Non-empty = only those doctor IDs.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListAssignedDoctorIdsAsync(Guid userId, Guid clinicId, CancellationToken cancellationToken);

    Task EnsureClinicStaffAccessAsync(Guid userId, Guid clinicId, CancellationToken cancellationToken);

    Task<bool> IsClinicOwnerAsync(Guid userId, Guid clinicId, CancellationToken cancellationToken);

    Task<bool> HasQueueStaffAccessAsync(Guid userId, Guid clinicId, Guid branchId, CancellationToken cancellationToken);
}

public interface IQueueRealtime
{
    Task PublishAsync(string group, string eventName, object payload, CancellationToken cancellationToken);
}
