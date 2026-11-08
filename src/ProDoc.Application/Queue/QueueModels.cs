namespace ProDoc.Application.Queue;

public sealed record StaffMemberDetails(Guid Id, Guid UserId, string? PhoneNumber, string Role, Guid? BranchId, bool IsActive);

public sealed record QueueSettingsDetails(
    Guid ClinicId,
    Guid BranchId,
    string TokenPrefix,
    bool ScheduledPatientPriority,
    int EarlyCheckInMinutes,
    int LateCheckInMinutes,
    bool AllowWalkIns,
    bool AllowPriorityPatients);

public sealed record QueueEntryDetails(
    Guid Id,
    string TokenLabel,
    int TokenNumber,
    int QueuePosition,
    string Status,
    string DisplayName,
    Guid DoctorId,
    string DoctorName,
    Guid ClinicId,
    string ClinicName,
    Guid BranchId,
    string BranchName,
    Guid? AppointmentId,
    string? AppointmentReference,
    bool IsWalkIn,
    bool IsLate,
    DateTimeOffset CheckInAtUtc,
    DateTimeOffset? AppointmentStartsAtUtc,
    DateTimeOffset? CalledAtUtc,
    DateTimeOffset? ConsultationStartedAtUtc,
    int? PatientsAhead,
    int? EstimatedWaitMinutes,
    string? StatusMessage,
    DateTimeOffset UpdatedAtUtc,
    string? PaymentStatus = null,
    string? PaymentMethod = null);

public sealed record QueueSummary(
    int TotalToday,
    int Waiting,
    int Called,
    int InConsultation,
    int Completed,
    int WalkIns,
    QueueEntryDetails? Current,
    QueueEntryDetails? Next,
    int Skipped = 0,
    int Paid = 0,
    int Unpaid = 0);

public sealed record QueueDisplayBoard(
    string ClinicName,
    string BranchName,
    string DoctorName,
    string? NowConsulting,
    string? Next,
    string? PleaseBeReady,
    string Status,
    DateTimeOffset UpdatedAtUtc);

public sealed record CheckInTokenDetails(Guid AppointmentId, string Token, DateTimeOffset ExpiresAtUtc);

public sealed record WalkInDetails(
    Guid Id,
    Guid QueueEntryId,
    string TokenLabel,
    string PatientDisplayName,
    Guid DoctorId,
    string DoctorName,
    decimal ConsultationFee,
    string Currency,
    DateTimeOffset CreatedAtUtc);

public sealed record AppointmentSearchItem(
    Guid Id,
    string? Reference,
    string Status,
    string PatientName,
    Guid DoctorId,
    string DoctorName,
    DateTimeOffset StartsAtUtc,
    bool IsWalkIn,
    string? TokenLabel);

public sealed record AdminQueueSnapshot(
    Guid ClinicId,
    string ClinicName,
    Guid BranchId,
    string BranchName,
    Guid DoctorId,
    string DoctorName,
    DateOnly OperatingDate,
    int Waiting,
    int InConsultation,
    int Completed,
    int WalkIns,
    int Skipped,
    double? AverageWaitMinutes,
    double? AverageConsultationMinutes,
    string SessionStatus);

public sealed record NotificationDetails(
    Guid Id,
    string Kind,
    string Title,
    string Body,
    DateTimeOffset CreatedAtUtc,
    bool IsRead,
    Guid? SubjectId,
    string DeepLink);

public sealed record DeskClinic(
    Guid Id,
    string Name,
    IReadOnlyList<DeskBranch> Branches);

public sealed record DeskBranch(Guid Id, string Name, bool IsActive);

public sealed record DeskDoctor(Guid Id, string DisplayName, int YearsOfExperience = 0);
