using ProDoc.Domain.Common;

namespace ProDoc.Domain.Queue;

public enum QueueEntryStatus
{
    Waiting = 1,
    Called = 2,
    InConsultation = 3,
    Completed = 4,
    Skipped = 5,
    Cancelled = 6,
    OnHold = 7
}

public enum QueueSessionStatus
{
    Open = 1,
    Closed = 2
}

public enum CheckInMethod
{
    Qr = 1,
    Reception = 2,
    WalkIn = 3
}

public static class QueueDefaults
{
    public const string TokenPrefix = "A";

    public const int EarlyCheckInMinutes = 60;

    public const int LateCheckInMinutes = 120;

    public static readonly TimeSpan NotificationCooldown = TimeSpan.FromMinutes(2);
}

public sealed class ClinicQueueSettings
{
    private ClinicQueueSettings()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    public Guid BranchId { get; private set; }

    public string TokenPrefix { get; private set; } = QueueDefaults.TokenPrefix;

    public bool ScheduledPatientPriority { get; private set; } = true;

    public int EarlyCheckInMinutes { get; private set; } = QueueDefaults.EarlyCheckInMinutes;

    public int LateCheckInMinutes { get; private set; } = QueueDefaults.LateCheckInMinutes;

    public bool AllowWalkIns { get; private set; } = true;

    public bool AllowPriorityPatients { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static ClinicQueueSettings CreateDefault(Guid clinicId, Guid branchId, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            ClinicId = clinicId,
            BranchId = branchId,
            UpdatedAtUtc = now
        };

    public void Update(
        string tokenPrefix,
        bool scheduledPatientPriority,
        int earlyCheckInMinutes,
        int lateCheckInMinutes,
        bool allowWalkIns,
        bool allowPriorityPatients,
        DateTimeOffset now)
    {
        var prefix = tokenPrefix.Trim().ToUpperInvariant();
        if (prefix.Length is < 1 or > 4 || prefix.Any(ch => !char.IsLetter(ch)))
        {
            throw DomainException.Validation("token_prefix_invalid", "Use 1 to 4 letters for the token prefix.");
        }

        if (earlyCheckInMinutes is < 0 or > 10_080 || lateCheckInMinutes is < 0 or > 10_080)
        {
            throw DomainException.Validation("check_in_window_invalid", "Choose valid early and late check-in windows.");
        }

        TokenPrefix = prefix;
        ScheduledPatientPriority = scheduledPatientPriority;
        EarlyCheckInMinutes = earlyCheckInMinutes;
        LateCheckInMinutes = lateCheckInMinutes;
        AllowWalkIns = allowWalkIns;
        AllowPriorityPatients = allowPriorityPatients;
        UpdatedAtUtc = now;
    }
}

public sealed class ClinicQueueSession
{
    private ClinicQueueSession()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid DoctorId { get; private set; }

    public DateOnly OperatingDate { get; private set; }

    public QueueSessionStatus Status { get; private set; }

    public DateTimeOffset StartedAtUtc { get; private set; }

    public DateTimeOffset? ClosedAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static ClinicQueueSession Open(
        Guid clinicId,
        Guid branchId,
        Guid doctorId,
        DateOnly operatingDate,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            ClinicId = clinicId,
            BranchId = branchId,
            DoctorId = doctorId,
            OperatingDate = operatingDate,
            Status = QueueSessionStatus.Open,
            StartedAtUtc = now,
            CreatedAtUtc = now
        };

    public void Close(DateTimeOffset now)
    {
        Status = QueueSessionStatus.Closed;
        ClosedAtUtc = now;
    }
}

public sealed class QueueTokenCounter
{
    /// <summary>Sentinel for day-wide counters used by timed-slot visit tokens.</summary>
    public const int DayWideRangeMinutes = -1;

    private QueueTokenCounter()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid DoctorId { get; private set; }

    public DateOnly OperatingDate { get; private set; }

    /// <summary>
    /// Local range start in minutes from midnight for hourly queues, or <see cref="DayWideRangeMinutes"/> for timed slots.
    /// </summary>
    public int RangeStartMinutes { get; private set; } = DayWideRangeMinutes;

    public int LastIssuedNumber { get; private set; }

    public byte[] RowVersion { get; private set; } = null!;

    public static QueueTokenCounter Start(
        Guid clinicId,
        Guid branchId,
        Guid doctorId,
        DateOnly operatingDate,
        int rangeStartMinutes = DayWideRangeMinutes) =>
        new()
        {
            Id = Guid.NewGuid(),
            ClinicId = clinicId,
            BranchId = branchId,
            DoctorId = doctorId,
            OperatingDate = operatingDate,
            RangeStartMinutes = rangeStartMinutes,
            LastIssuedNumber = 0
        };

    public int Next()
    {
        LastIssuedNumber += 1;
        return LastIssuedNumber;
    }
}

public sealed class AppointmentCheckInToken
{
    private AppointmentCheckInToken()
    {
    }

    public Guid Id { get; private set; }

    public Guid AppointmentId { get; private set; }

    public Guid BranchId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? UsedAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static AppointmentCheckInToken Create(
        Guid appointmentId,
        Guid branchId,
        string tokenHash,
        DateTimeOffset expiresAtUtc,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointmentId,
            BranchId = branchId,
            TokenHash = tokenHash,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = now
        };

    public void MarkUsed(DateTimeOffset now)
    {
        if (RevokedAtUtc is not null || UsedAtUtc is not null || ExpiresAtUtc <= now)
        {
            throw DomainException.Conflict("check_in_token_invalid", "That check-in code is no longer valid.");
        }

        UsedAtUtc = now;
    }

    public void Revoke(DateTimeOffset now) => RevokedAtUtc ??= now;
}

public sealed class WalkInRecord
{
    private WalkInRecord()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid DoctorId { get; private set; }

    public string PatientDisplayName { get; private set; } = string.Empty;

    public string? ContactNumber { get; private set; }

    public string? ReasonForVisit { get; private set; }

    public decimal ConsultationFee { get; private set; }

    public string Currency { get; private set; } = "INR";

    public Guid CreatedByAccountId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static WalkInRecord Create(
        Guid clinicId,
        Guid branchId,
        Guid doctorId,
        string patientDisplayName,
        string? contactNumber,
        string? reasonForVisit,
        decimal consultationFee,
        Guid createdByAccountId,
        DateTimeOffset now)
    {
        var name = patientDisplayName.Trim();
        if (name.Length is < 2 or > 80)
        {
            throw DomainException.Validation("name_invalid", "Enter a patient name from 2 to 80 characters.");
        }

        if (consultationFee is < 0 or > 100_000)
        {
            throw DomainException.Validation("fee_invalid", "Enter a consultation fee from 0 to 100000.");
        }

        return new WalkInRecord
        {
            Id = Guid.NewGuid(),
            ClinicId = clinicId,
            BranchId = branchId,
            DoctorId = doctorId,
            PatientDisplayName = name,
            ContactNumber = string.IsNullOrWhiteSpace(contactNumber) ? null : contactNumber.Trim(),
            ReasonForVisit = string.IsNullOrWhiteSpace(reasonForVisit) ? null : reasonForVisit.Trim()[..Math.Min(reasonForVisit.Trim().Length, 120)],
            ConsultationFee = decimal.Round(consultationFee, 2),
            CreatedByAccountId = createdByAccountId,
            CreatedAtUtc = now
        };
    }
}

public sealed class UserNotification
{
    private UserNotification()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Kind { get; private set; } = string.Empty;

    public Guid? SubjectId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    public static UserNotification Create(
        Guid userId,
        string kind,
        Guid? subjectId,
        string title,
        string body,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Kind = kind.Trim(),
            SubjectId = subjectId,
            Title = title.Length <= 120 ? title.Trim() : title.Trim()[..120],
            Body = body.Length <= 500 ? body.Trim() : body.Trim()[..500],
            CreatedAtUtc = now
        };

    public void MarkRead(DateTimeOffset now)
    {
        if (ReadAtUtc is not null)
        {
            return;
        }

        ReadAtUtc = now;
    }
}

/// <summary>Stable notification kind strings used across portals and deep links.</summary>
public static class NotificationKinds
{
    public const string AppointmentConfirmed = "appointment.confirmed";
    public const string AppointmentCancelled = "appointment.cancelled";
    public const string AppointmentRescheduled = "appointment.rescheduled";
    public const string AppointmentReminder = "appointment.reminder";
    public const string FollowUpRecommended = "follow_up.recommended";
    public const string FollowUpUpdated = "follow_up.updated";
    public const string FollowUpReminder = "follow_up.reminder";
    public const string FollowUpCancelled = "follow_up.cancelled";
    public const string AppointmentScheduleUpdate = "appointment.schedule_update";
    public const string AppointmentCheckedIn = "appointment.checked_in";
    public const string AppointmentCompleted = "appointment.completed";
    public const string AppointmentNoShow = "appointment.no_show";
    public const string PaymentPaid = "payment.paid";
    public const string PaymentFailed = "payment.failed";
}
