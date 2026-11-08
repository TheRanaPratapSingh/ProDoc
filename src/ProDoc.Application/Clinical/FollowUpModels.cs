using ProDoc.Domain.Clinical;

namespace ProDoc.Application.Clinical;

public sealed record FollowUpDetails(
    Guid Id,
    Guid PatientUserId,
    Guid? FamilyMemberId,
    string PatientName,
    Guid DoctorId,
    string DoctorName,
    Guid ClinicId,
    string ClinicName,
    Guid BranchId,
    Guid? SourceAppointmentId,
    Guid? SourceConsultationId,
    DateOnly RecommendedDate,
    string? FollowUpNote,
    bool RequiresBooking,
    string Status,
    string DisplayStatus,
    Guid? LinkedAppointmentId,
    bool HasBookedAppointment,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string TimeZoneId,
    IReadOnlyList<FollowUpReminderDetails> Reminders);

public sealed record FollowUpReminderDetails(
    Guid Id,
    string ReminderType,
    DateTimeOffset ScheduledAtUtc,
    string DeliveryStatus,
    DateTimeOffset? SentAtUtc,
    string IdempotencyKey);

public sealed record FollowUpListFilter(
    DateOnly? From,
    DateOnly? To,
    string? PatientQuery,
    string? Status,
    Guid? ClinicId,
    Guid? DoctorId,
    int Take = 100);

public static class FollowUpReminderLabels
{
    public static string ForType(FollowUpReminderType type) =>
        type switch
        {
            FollowUpReminderType.ThreeDaysBefore => "3d",
            FollowUpReminderType.OneDayBefore => "1d",
            FollowUpReminderType.OnDueDate => "0d",
            _ => "custom"
        };

    public static FollowUpReminderType FromDaysBefore(int daysBefore) =>
        daysBefore switch
        {
            3 => FollowUpReminderType.ThreeDaysBefore,
            1 => FollowUpReminderType.OneDayBefore,
            0 => FollowUpReminderType.OnDueDate,
            _ => FollowUpReminderType.Custom
        };
}
