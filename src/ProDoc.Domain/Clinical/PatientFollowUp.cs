using ProDoc.Domain.Common;

namespace ProDoc.Domain.Clinical;

/// <summary>
/// Lifecycle of a doctor-authored follow-up recommendation (not an appointment).
/// Due/Overdue are computed for display from <see cref="RecommendedDate"/> when still open.
/// </summary>
public enum FollowUpStatus
{
    Recommended = 0,
    AppointmentBooked = 1,
    Completed = 2,
    Cancelled = 3
}

public enum FollowUpReminderType
{
    ThreeDaysBefore = 0,
    OneDayBefore = 1,
    OnDueDate = 2,
    Custom = 3
}

public enum FollowUpReminderDeliveryStatus
{
    Pending = 0,
    Sent = 1,
    Skipped = 2,
    Failed = 3,
    Cancelled = 4
}

/// <summary>
/// Doctor-defined follow-up recommendation. Never auto-generated as medical advice.
/// A recommendation is not a confirmed appointment until the patient books via the booking engine.
/// </summary>
public sealed class PatientFollowUp
{
    private PatientFollowUp()
    {
    }

    public Guid Id { get; private set; }

    public Guid PatientUserId { get; private set; }

    public Guid? FamilyMemberId { get; private set; }

    public string PatientName { get; private set; } = string.Empty;

    public Guid DoctorId { get; private set; }

    public Guid ClinicId { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid? SourceAppointmentId { get; private set; }

    public Guid? SourceConsultationId { get; private set; }

    public DateOnly RecommendedDate { get; private set; }

    public string? FollowUpNote { get; private set; }

    /// <summary>When true, the patient is encouraged to book a new visit.</summary>
    public bool RequiresBooking { get; private set; } = true;

    public FollowUpStatus Status { get; private set; }

    public Guid? LinkedAppointmentId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public string TimeZoneId { get; private set; } = "Asia/Kolkata";

    public static PatientFollowUp Create(
        Guid patientUserId,
        Guid doctorId,
        Guid clinicId,
        Guid branchId,
        Guid createdByUserId,
        string patientName,
        DateOnly recommendedDate,
        DateOnly todayLocal,
        DateTimeOffset now,
        Guid? familyMemberId = null,
        Guid? sourceAppointmentId = null,
        Guid? sourceConsultationId = null,
        string? note = null,
        bool requiresBooking = true,
        string? timeZoneId = null)
    {
        if (patientUserId == Guid.Empty || doctorId == Guid.Empty || clinicId == Guid.Empty || branchId == Guid.Empty)
        {
            throw DomainException.Validation("follow_up_party_invalid", "Patient, doctor, and clinic are required.");
        }

        if (createdByUserId == Guid.Empty)
        {
            throw DomainException.Validation("follow_up_author_invalid", "An authorized doctor must create the follow-up.");
        }

        EnsureFutureDate(recommendedDate, todayLocal);

        var name = (patientName ?? string.Empty).Trim();
        if (name.Length is < 2 or > 120)
        {
            throw DomainException.Validation("patient_name_invalid", "Enter a patient name from 2 to 120 characters.");
        }

        return new PatientFollowUp
        {
            Id = Guid.NewGuid(),
            PatientUserId = patientUserId,
            FamilyMemberId = familyMemberId,
            PatientName = name,
            DoctorId = doctorId,
            ClinicId = clinicId,
            BranchId = branchId,
            SourceAppointmentId = sourceAppointmentId,
            SourceConsultationId = sourceConsultationId,
            RecommendedDate = recommendedDate,
            FollowUpNote = NormalizeNote(note),
            RequiresBooking = requiresBooking,
            Status = FollowUpStatus.Recommended,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            TimeZoneId = string.IsNullOrWhiteSpace(timeZoneId) ? "Asia/Kolkata" : timeZoneId.Trim()
        };
    }

    public void Update(
        DateOnly recommendedDate,
        DateOnly todayLocal,
        string? note,
        bool? requiresBooking,
        DateTimeOffset now)
    {
        EnsureEditable();
        EnsureFutureDate(recommendedDate, todayLocal);
        RecommendedDate = recommendedDate;
        FollowUpNote = NormalizeNote(note);
        if (requiresBooking is bool value)
        {
            RequiresBooking = value;
        }

        UpdatedAtUtc = now;
    }

    public void LinkAppointment(Guid appointmentId, DateTimeOffset now)
    {
        EnsureEditable();
        if (appointmentId == Guid.Empty)
        {
            throw DomainException.Validation("follow_up_appointment_invalid", "Choose a valid appointment to link.");
        }

        LinkedAppointmentId = appointmentId;
        Status = FollowUpStatus.AppointmentBooked;
        UpdatedAtUtc = now;
    }

    public void MarkCompleted(DateTimeOffset now)
    {
        if (Status == FollowUpStatus.Cancelled)
        {
            throw DomainException.Conflict("follow_up_cancelled", "A cancelled follow-up cannot be completed.");
        }

        if (Status == FollowUpStatus.Completed)
        {
            return;
        }

        Status = FollowUpStatus.Completed;
        CompletedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public void Cancel(DateTimeOffset now)
    {
        if (Status == FollowUpStatus.Completed)
        {
            throw DomainException.Conflict("follow_up_completed", "A completed follow-up cannot be cancelled.");
        }

        if (Status == FollowUpStatus.Cancelled)
        {
            return;
        }

        Status = FollowUpStatus.Cancelled;
        CancelledAtUtc = now;
        UpdatedAtUtc = now;
    }

    public bool IsOpen => Status is FollowUpStatus.Recommended or FollowUpStatus.AppointmentBooked;

    /// <summary>
    /// Display status for portals. Does not mutate stored status when a date passes.
    /// </summary>
    public string ResolveDisplayStatus(DateOnly todayLocal)
    {
        return Status switch
        {
            FollowUpStatus.Cancelled => "Cancelled",
            FollowUpStatus.Completed => "Completed",
            FollowUpStatus.AppointmentBooked when RecommendedDate < todayLocal => "Overdue",
            FollowUpStatus.AppointmentBooked when RecommendedDate == todayLocal => "Due",
            FollowUpStatus.AppointmentBooked => "Appointment Booked",
            FollowUpStatus.Recommended when RecommendedDate < todayLocal => "Overdue",
            FollowUpStatus.Recommended when RecommendedDate == todayLocal => "Due",
            _ => "Recommended"
        };
    }

    private void EnsureEditable()
    {
        if (Status == FollowUpStatus.Cancelled)
        {
            throw DomainException.Conflict("follow_up_cancelled", "That follow-up was cancelled.");
        }

        if (Status == FollowUpStatus.Completed)
        {
            throw DomainException.Conflict("follow_up_completed", "That follow-up is already completed.");
        }
    }

    private static void EnsureFutureDate(DateOnly recommendedDate, DateOnly todayLocal)
    {
        if (recommendedDate <= todayLocal)
        {
            throw DomainException.Validation("follow_up_date_invalid", "Choose a follow-up date in the future.");
        }
    }

    private static string? NormalizeNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        var text = note.Trim();
        if (text.Length > 500)
        {
            throw DomainException.Validation("follow_up_note_invalid", "Keep the follow-up note under 500 characters.");
        }

        return text;
    }
}

/// <summary>
/// Scheduled reminder row for a follow-up recommendation. Delivery is driven by the automation outbox.
/// </summary>
public sealed class FollowUpReminder
{
    private FollowUpReminder()
    {
    }

    public Guid Id { get; private set; }

    public Guid FollowUpId { get; private set; }

    public FollowUpReminderType ReminderType { get; private set; }

    public DateTimeOffset ScheduledAtUtc { get; private set; }

    public FollowUpReminderDeliveryStatus DeliveryStatus { get; private set; }

    public DateTimeOffset? SentAtUtc { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public string? SkipReason { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static FollowUpReminder Schedule(
        Guid followUpId,
        FollowUpReminderType reminderType,
        DateTimeOffset scheduledAtUtc,
        string idempotencyKey,
        DateTimeOffset now)
    {
        if (followUpId == Guid.Empty)
        {
            throw DomainException.Validation("follow_up_reminder_invalid", "A follow-up is required for the reminder.");
        }

        var key = (idempotencyKey ?? string.Empty).Trim();
        if (key.Length is < 8 or > 160)
        {
            throw DomainException.Validation("follow_up_reminder_key_invalid", "A valid reminder idempotency key is required.");
        }

        return new FollowUpReminder
        {
            Id = Guid.NewGuid(),
            FollowUpId = followUpId,
            ReminderType = reminderType,
            ScheduledAtUtc = scheduledAtUtc,
            DeliveryStatus = FollowUpReminderDeliveryStatus.Pending,
            IdempotencyKey = key,
            CreatedAtUtc = now
        };
    }

    public void MarkSent(DateTimeOffset now)
    {
        if (DeliveryStatus is FollowUpReminderDeliveryStatus.Cancelled or FollowUpReminderDeliveryStatus.Skipped)
        {
            return;
        }

        DeliveryStatus = FollowUpReminderDeliveryStatus.Sent;
        SentAtUtc = now;
        SkipReason = null;
    }

    public void MarkSkipped(string reason, DateTimeOffset now)
    {
        if (DeliveryStatus == FollowUpReminderDeliveryStatus.Sent)
        {
            return;
        }

        DeliveryStatus = FollowUpReminderDeliveryStatus.Skipped;
        SentAtUtc = now;
        SkipReason = string.IsNullOrWhiteSpace(reason) ? "skipped" : reason.Trim()[..Math.Min(reason.Trim().Length, 240)];
    }

    public void MarkFailed(string reason)
    {
        if (DeliveryStatus == FollowUpReminderDeliveryStatus.Sent)
        {
            return;
        }

        DeliveryStatus = FollowUpReminderDeliveryStatus.Failed;
        SkipReason = string.IsNullOrWhiteSpace(reason) ? "failed" : reason.Trim()[..Math.Min(reason.Trim().Length, 240)];
    }

    public void Cancel(string reason, DateTimeOffset now)
    {
        if (DeliveryStatus is FollowUpReminderDeliveryStatus.Sent or FollowUpReminderDeliveryStatus.Cancelled)
        {
            return;
        }

        DeliveryStatus = FollowUpReminderDeliveryStatus.Cancelled;
        SentAtUtc = now;
        SkipReason = string.IsNullOrWhiteSpace(reason) ? "cancelled" : reason.Trim()[..Math.Min(reason.Trim().Length, 240)];
    }
}
