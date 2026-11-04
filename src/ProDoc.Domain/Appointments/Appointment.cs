using ProDoc.Domain.Common;
using ProDoc.Domain.Providers;

namespace ProDoc.Domain.Appointments;

public enum AppointmentStatus
{
    Held = 1,
    Requested = 2,
    Confirmed = 3,
    Rejected = 4,
    Cancelled = 5,
    Expired = 6,
    CheckedIn = 7,
    Completed = 8
}

public static class AppointmentPolicy
{
    public static readonly TimeSpan HoldFor = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan ApprovalWindow = TimeSpan.FromHours(4);

    public static readonly TimeSpan PatientChangeCutoff = TimeSpan.FromHours(2);

    public const string PayAtClinic = "PayAtClinic";

    public const string CancellationSummary =
        "You can cancel or reschedule until 2 hours before the visit. Pay the consultation fee at the clinic.";
}

public sealed class FamilyMember
{
    private FamilyMember()
    {
    }

    public Guid Id { get; private set; }

    public Guid AccountUserId { get; private set; }

    public string FullName { get; private set; } = string.Empty;

    public DateOnly? DateOfBirth { get; private set; }

    public string? Gender { get; private set; }

    public string Relationship { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static FamilyMember Create(
        Guid accountUserId,
        string fullName,
        DateOnly? dateOfBirth,
        string? gender,
        string relationship,
        DateTimeOffset now)
    {
        var name = RequiredName(fullName);
        var relation = relationship.Trim().ToLowerInvariant();
        if (relation is not ("spouse" or "child" or "parent" or "other"))
        {
            throw DomainException.Validation("relationship_invalid", "Choose spouse, child, parent, or other.");
        }

        return new FamilyMember
        {
            Id = Guid.NewGuid(),
            AccountUserId = accountUserId,
            FullName = name,
            DateOfBirth = dateOfBirth,
            Gender = string.IsNullOrWhiteSpace(gender) ? null : gender.Trim().ToLowerInvariant(),
            Relationship = relation,
            CreatedAtUtc = now
        };
    }

    private static string RequiredName(string fullName)
    {
        var name = fullName.Trim();
        if (name.Length is < 2 or > 80)
        {
            throw DomainException.Validation("name_invalid", "Enter a name from 2 to 80 characters.");
        }

        return name;
    }
}

public sealed class Appointment
{
    private Appointment()
    {
    }

    public Guid Id { get; private set; }

    public string? Reference { get; private set; }

    public Guid AccountUserId { get; private set; }

    public Guid? FamilyMemberId { get; private set; }

    public string PatientName { get; private set; } = string.Empty;

    public Guid DoctorId { get; private set; }

    public Guid ClinicId { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid ScheduleId { get; private set; }

    public DateTimeOffset StartsAtUtc { get; private set; }

    public DateTimeOffset EndsAtUtc { get; private set; }

    public string TimeZoneId { get; private set; } = string.Empty;

    public decimal ConsultationFee { get; private set; }

    public string Currency { get; private set; } = "INR";

    public string PaymentMethod { get; private set; } = AppointmentPolicy.PayAtClinic;

    public string PaymentStatus { get; private set; } = "Unpaid";

    public decimal PaidAmount { get; private set; }

    public Guid? ActivePaymentId { get; private set; }

    public AppointmentStatus Status { get; private set; }

    public bool RequiresApproval { get; private set; }

    public DateTimeOffset? HoldExpiresAtUtc { get; private set; }

    public DateTimeOffset? ApprovalDeadlineUtc { get; private set; }

    public string? Reason { get; private set; }

    public string? Notes { get; private set; }

    public string? DecisionNote { get; private set; }

    public bool IsWalkIn { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = null!;

    public bool OccupiesSlot(DateTimeOffset now) =>
        Status is AppointmentStatus.Requested or AppointmentStatus.Confirmed or AppointmentStatus.CheckedIn or AppointmentStatus.Completed
        || (Status == AppointmentStatus.Held && HoldExpiresAtUtc > now);

    public static Appointment Hold(
        Guid accountUserId,
        Guid doctorId,
        Guid clinicId,
        Guid branchId,
        Guid scheduleId,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        string timeZoneId,
        decimal consultationFee,
        bool requiresApproval,
        DateTimeOffset now)
    {
        if (endsAtUtc <= startsAtUtc || startsAtUtc <= now)
        {
            throw DomainException.Validation("slot_invalid", "Choose a time that has not already started.");
        }

        return new Appointment
        {
            Id = Guid.NewGuid(),
            AccountUserId = accountUserId,
            DoctorId = doctorId,
            ClinicId = clinicId,
            BranchId = branchId,
            ScheduleId = scheduleId,
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = endsAtUtc,
            TimeZoneId = timeZoneId,
            ConsultationFee = consultationFee,
            Currency = "INR",
            PaymentMethod = AppointmentPolicy.PayAtClinic,
            Status = AppointmentStatus.Held,
            RequiresApproval = requiresApproval,
            HoldExpiresAtUtc = now.Add(AppointmentPolicy.HoldFor),
            PatientName = string.Empty,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public static Appointment WalkIn(
        Guid staffUserId,
        string patientName,
        Guid doctorId,
        Guid clinicId,
        Guid branchId,
        Guid scheduleId,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        string timeZoneId,
        decimal consultationFee,
        DateTimeOffset now)
    {
        var appointment = Hold(staffUserId, doctorId, clinicId, branchId, scheduleId, startsAtUtc, endsAtUtc, timeZoneId, consultationFee, false, now);
        appointment.IsWalkIn = true;
        appointment.Confirm(patientName, null, null, null, now);
        return appointment;
    }

    public void Confirm(string patientName, Guid? familyMemberId, string? reason, string? notes, DateTimeOffset now)
    {
        EnsureHeld(now);
        var name = RequiredPatientName(patientName);
        var visitReason = Optional(reason, 120, "reason_invalid", "Enter a reason of 120 characters or fewer.");
        var visitNotes = Optional(notes, 500, "notes_invalid", "Enter notes of 500 characters or fewer.");
        if (RequiresApproval && StartsAtUtc.AddMinutes(-30) <= now)
        {
            throw DomainException.Validation("approval_window_closed", "This visit is too soon for an approval request.");
        }

        PatientName = name;
        FamilyMemberId = familyMemberId;
        Reason = visitReason;
        Notes = visitNotes;
        Reference = ApplicationReference.Create("APT", now);
        if (RequiresApproval)
        {
            var deadline = now.Add(AppointmentPolicy.ApprovalWindow);
            var latest = StartsAtUtc.AddMinutes(-30);
            Status = AppointmentStatus.Requested;
            ApprovalDeadlineUtc = deadline < latest ? deadline : latest;
            HoldExpiresAtUtc = null;
        }
        else
        {
            Status = AppointmentStatus.Confirmed;
            HoldExpiresAtUtc = null;
        }

        UpdatedAtUtc = now;
    }

    public void SetPaymentMethod(string paymentMethod, DateTimeOffset now)
    {
        var method = paymentMethod.Trim();
        if (method is not (AppointmentPolicy.PayAtClinic or "PayOnline" or "PartialOnline"))
        {
            throw DomainException.Validation("payment_method_invalid", "Choose pay at clinic or pay online.");
        }

        PaymentMethod = method;
        UpdatedAtUtc = now;
    }

    public void AttachPayment(Guid paymentId, string paymentStatus, decimal paidAmount, DateTimeOffset now)
    {
        ActivePaymentId = paymentId;
        PaymentStatus = paymentStatus;
        PaidAmount = decimal.Round(paidAmount, 2);
        UpdatedAtUtc = now;
    }

    public void Approve(DateTimeOffset now)
    {
        EnsureRequested(now);
        Status = AppointmentStatus.Confirmed;
        ApprovalDeadlineUtc = null;
        UpdatedAtUtc = now;
    }

    public void Reject(string? reason, DateTimeOffset now)
    {
        EnsureRequested(now);
        Status = AppointmentStatus.Rejected;
        DecisionNote = Optional(reason, 120, "reason_invalid", "Enter a reason of 120 characters or fewer.");
        UpdatedAtUtc = now;
    }

    public void MarkCheckedIn(DateTimeOffset now)
    {
        if (Status != AppointmentStatus.Confirmed)
        {
            throw DomainException.Conflict("appointment_not_checkable", "Only a confirmed appointment can be checked in.");
        }

        Status = AppointmentStatus.CheckedIn;
        UpdatedAtUtc = now;
    }

    public void MarkCompleted(DateTimeOffset now)
    {
        if (Status is not (AppointmentStatus.CheckedIn or AppointmentStatus.Confirmed))
        {
            throw DomainException.Conflict("appointment_not_completable", "Only a checked-in visit can be completed.");
        }

        Status = AppointmentStatus.Completed;
        UpdatedAtUtc = now;
    }

    public void Cancel(bool staff, string? reason, DateTimeOffset now)
    {
        if (Status is not (AppointmentStatus.Requested or AppointmentStatus.Confirmed or AppointmentStatus.CheckedIn))
        {
            throw DomainException.Conflict("appointment_closed", "This appointment can no longer be changed.");
        }

        if (!staff && StartsAtUtc <= now.Add(AppointmentPolicy.PatientChangeCutoff))
        {
            throw DomainException.Conflict("cancellation_closed", AppointmentPolicy.CancellationSummary);
        }

        if (staff && StartsAtUtc <= now)
        {
            throw DomainException.Conflict("cancellation_closed", "This visit has already started.");
        }

        Status = AppointmentStatus.Cancelled;
        DecisionNote = Optional(reason, 120, "reason_invalid", "Enter a reason of 120 characters or fewer.") ?? DecisionNote;
        UpdatedAtUtc = now;
    }

    public void Reschedule(DateTimeOffset startsAtUtc, DateTimeOffset endsAtUtc, bool staff, DateTimeOffset now)
    {
        if (endsAtUtc <= startsAtUtc || startsAtUtc <= now)
        {
            throw DomainException.Validation("slot_invalid", "Choose a time that has not already started.");
        }

        Cancel(staff, null, now);
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        Status = RequiresApproval && !staff ? AppointmentStatus.Requested : AppointmentStatus.Confirmed;
        if (Status == AppointmentStatus.Requested)
        {
            ApprovalDeadlineUtc = now.Add(AppointmentPolicy.ApprovalWindow);
        }

        HoldExpiresAtUtc = null;
        UpdatedAtUtc = now;
    }

    public void Release(DateTimeOffset now)
    {
        if (Status != AppointmentStatus.Held)
        {
            throw DomainException.Conflict("appointment_closed", "This hold is no longer active.");
        }

        Status = AppointmentStatus.Expired;
        UpdatedAtUtc = now;
    }

    public void Expire(DateTimeOffset now)
    {
        if (Status == AppointmentStatus.Held && HoldExpiresAtUtc <= now)
        {
            Status = AppointmentStatus.Expired;
            UpdatedAtUtc = now;
        }
        else if (Status == AppointmentStatus.Requested && ApprovalDeadlineUtc <= now)
        {
            Status = AppointmentStatus.Expired;
            UpdatedAtUtc = now;
        }
    }

    private void EnsureHeld(DateTimeOffset now)
    {
        if (Status != AppointmentStatus.Held || HoldExpiresAtUtc <= now)
        {
            Status = AppointmentStatus.Expired;
            UpdatedAtUtc = now;
            throw DomainException.Conflict("hold_expired", "That time was released. Choose it again.");
        }
    }

    private void EnsureRequested(DateTimeOffset now)
    {
        if (Status != AppointmentStatus.Requested)
        {
            throw DomainException.Conflict("appointment_closed", "This request is no longer waiting for a decision.");
        }

        if (ApprovalDeadlineUtc <= now || StartsAtUtc <= now)
        {
            Status = AppointmentStatus.Expired;
            UpdatedAtUtc = now;
            throw DomainException.Conflict("approval_expired", "The request window has ended and the time was released.");
        }
    }

    private static string RequiredPatientName(string fullName)
    {
        var name = fullName.Trim();
        if (name.Length is < 2 or > 80)
        {
            throw DomainException.Validation("name_invalid", "Enter the patient's name before confirming.");
        }

        return name;
    }

    private static string? Optional(string? value, int max, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > max)
        {
            throw DomainException.Validation(code, message);
        }

        return trimmed;
    }
}
