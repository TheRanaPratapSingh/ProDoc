namespace ProDoc.Application.Appointments;

/// <summary>Redacted appointment summary safe for family sharing (no clinical notes).</summary>
public sealed record SharedAppointmentSummary(
    string BookingReference,
    string Status,
    string PatientName,
    string DoctorName,
    string? Specialty,
    string ClinicName,
    string? BranchName,
    string? Address,
    string? MapsUrl,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string TimeZoneId,
    string BookingMode,
    string? TokenLabel,
    int? TokenNumber,
    DateTimeOffset? LinkExpiresAtUtc);

public sealed record AppointmentShareLinkDetails(
    Guid ShareId,
    Guid AppointmentId,
    string ShareUrl,
    string RawToken,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    bool IsActive);

public sealed record AppointmentShareLinkStatus(
    Guid ShareId,
    Guid AppointmentId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? RevokedAtUtc,
    bool IsActive,
    int AccessCount);
