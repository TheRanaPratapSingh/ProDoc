namespace ProDoc.Application.Appointments;

public sealed record AppointmentDetails(
    Guid Id,
    string? Reference,
    string Status,
    string PatientName,
    Guid? FamilyMemberId,
    Guid DoctorId,
    string DoctorName,
    Guid ClinicId,
    string ClinicName,
    Guid BranchId,
    string BranchName,
    string Address,
    decimal? Latitude,
    decimal? Longitude,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string TimeZoneId,
    decimal ConsultationFee,
    string Currency,
    string PaymentMethod,
    string PaymentStatus,
    decimal PaidAmount,
    bool RequiresApproval,
    bool IsWalkIn,
    DateTimeOffset? HoldExpiresAtUtc,
    string? Reason,
    string? Notes,
    string? DecisionNote,
    string CancellationPolicy);

public sealed record AppointmentSummary(
    Guid Id,
    string? Reference,
    string Status,
    Guid DoctorId,
    string DoctorName,
    Guid ClinicId,
    string ClinicName,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    decimal ConsultationFee,
    string Currency);

public sealed record FamilyMemberDetails(
    Guid Id,
    string FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    string Relationship);
