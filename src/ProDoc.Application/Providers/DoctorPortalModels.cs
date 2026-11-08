using ProDoc.Application.Appointments;
using ProDoc.Application.Billing;
using ProDoc.Application.Reviews;

namespace ProDoc.Application.Providers;

public sealed record DoctorClinicOption(
    Guid ClinicId,
    string Name,
    string Status,
    Guid? BranchId,
    string? BranchName,
    string Kind,
    bool IsPrimary);

public sealed record DoctorAppointmentCounts(
    int Today,
    int Upcoming,
    int Completed,
    int Pending,
    int Cancelled,
    int Waiting,
    int InConsultation,
    int DistinctPatients);

public sealed record DoctorConsultationSummary(
    int InProgress,
    int CompletedToday,
    int WithPrescriptionToday);

public sealed record DoctorEarningsSummary(
    bool PaymentsEnabled,
    decimal TotalGross,
    decimal TodayGross,
    decimal MonthGross,
    decimal CommissionDeducted,
    decimal NetPayable,
    decimal PendingSettlement);

public sealed record DoctorAvailabilitySummary(
    bool IsAvailableToday,
    string Status,
    string? StartsAt,
    string? EndsAt,
    string? BreakStartsAt,
    string? BreakEndsAt,
    string? ClosureReason,
    Guid? ScheduleId,
    int SlotDurationMinutes,
    Guid? ClosureId,
    string? TimingsLabel);

public sealed record DoctorDashboard(
    Guid DoctorId,
    string DisplayName,
    string? SpecializationName,
    Guid? PhotoDocumentId,
    string VerificationBadge,
    string VerificationStatus,
    int ProfileCompletionPercent,
    Guid? ClinicId,
    string? ClinicName,
    int UnreadNotifications,
    DoctorAppointmentCounts Appointments,
    DoctorConsultationSummary Consultations,
    DoctorEarningsSummary Earnings,
    DoctorAvailabilitySummary? Availability,
    RatingSummary? Rating,
    IReadOnlyList<DoctorClinicOption> Clinics,
    IReadOnlyList<AppointmentDetails> TodayPreview,
    AppointmentDetails? NextPatient);

public sealed record DoctorEarningsDashboard(
    Guid DoctorId,
    Guid? ClinicId,
    bool PaymentsEnabled,
    decimal TotalGross,
    decimal TodayGross,
    decimal MonthGross,
    decimal CommissionDeducted,
    decimal NetPayable,
    decimal PendingSettlement,
    IReadOnlyList<CommissionSnapshotDetails> RecentCommissions,
    IReadOnlyList<SettlementDetails> RecentSettlements);

public sealed record DoctorPatientRow(
    Guid? AccountUserId,
    string PatientName,
    int VisitCount,
    DateTimeOffset? LastVisitAtUtc,
    string? LastStatus,
    Guid? LastAppointmentId);

public sealed record DoctorPrescriptionRow(
    Guid AppointmentId,
    string? AppointmentReference,
    string PatientName,
    Guid? PrescriptionId,
    string? PrescriptionNumber,
    string? PrescriptionStatus,
    string? Diagnosis,
    DateTimeOffset StartsAtUtc,
    Guid ClinicId,
    string ClinicName);
