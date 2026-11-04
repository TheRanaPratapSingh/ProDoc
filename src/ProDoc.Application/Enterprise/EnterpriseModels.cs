namespace ProDoc.Application.Enterprise;

public sealed record OrganizationDetails(
    Guid Id,
    string Name,
    string Kind,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    int ClinicCount,
    int MemberCount);

public sealed record OrganizationMembershipDetails(
    Guid Id,
    Guid OrganizationId,
    Guid UserId,
    string? PhoneNumber,
    string Role,
    Guid? ClinicId,
    Guid? BranchId,
    bool IsActive,
    DateTimeOffset UpdatedAtUtc);

public sealed record OrganizationClinicDetails(
    Guid ClinicId,
    string? Name,
    string Status,
    Guid OwnerUserId,
    int BranchCount);

public sealed record OrganizationSettingDetails(string Key, string Value, string Source, Guid? BranchId);

public sealed record ConsultationResourceDetails(
    Guid Id,
    Guid ClinicId,
    Guid BranchId,
    string Name,
    string Kind,
    bool IsActive);

public sealed record ResourceAllocationDetails(
    Guid Id,
    Guid ResourceId,
    Guid? DoctorId,
    Guid? ScheduleId,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc);

public sealed record OrganizationSubscriptionDetails(
    Guid OrganizationId,
    string PlanCode,
    string Status,
    int SeatLimit,
    string? BillingEmail,
    DateTimeOffset? CurrentPeriodEndUtc,
    string Note);

public sealed record BranchPerformanceRow(
    Guid BranchId,
    string BranchName,
    Guid ClinicId,
    string? ClinicName,
    int Appointments,
    int Completed,
    int Cancelled,
    int NoShows,
    decimal GrossFees,
    decimal Commission,
    double? AverageWaitMinutes);

public sealed record DoctorUtilizationRow(
    Guid DoctorId,
    string DoctorName,
    int Appointments,
    int Completed,
    decimal GrossFees);

public sealed record OrganizationAnalytics(
    int AppointmentVolume,
    int CompletedAppointments,
    int CancelledAppointments,
    int NoShowAppointments,
    int UniquePatients,
    decimal GrossConsultationFees,
    decimal CommissionCaptured,
    double CancellationRate,
    double NoShowRate,
    IReadOnlyList<BranchPerformanceRow> Branches,
    IReadOnlyList<DoctorUtilizationRow> Doctors,
    IReadOnlyList<NamedCountPoint> DailyVolume);

public sealed record NamedCountPoint(string Period, int Count);
