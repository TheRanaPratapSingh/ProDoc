namespace ProDoc.Application.Notifications;

public sealed record NotificationInboxItem(
    Guid Id,
    string Kind,
    string Title,
    string Body,
    DateTimeOffset CreatedAtUtc,
    bool IsRead,
    Guid? SubjectId,
    string DeepLink);

public sealed record NotificationUnreadCount(int Count);

public sealed record AppointmentPartyNotificationContext(
    Guid AppointmentId,
    Guid PatientUserId,
    Guid DoctorId,
    Guid ClinicId,
    Guid BranchId,
    string PatientName,
    string DoctorName,
    string ClinicName,
    string? Reference,
    string? TokenLabel,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string TimeZoneId,
    string Status,
    string BookingMode,
    DateTimeOffset? PreviousStartsAtUtc = null,
    DateTimeOffset? PreviousEndsAtUtc = null,
    string? CancellationReason = null);
