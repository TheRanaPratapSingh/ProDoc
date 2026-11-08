using ProDoc.Domain.Common;
using ProDoc.Domain.Providers;
using ProDoc.Domain.Scheduling;

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

    /// <summary>Maximum same-day missed-appointment reschedules a patient may perform.</summary>
    public const int MaxSameDayReschedules = 2;

    public const string PayAtClinic = "PayAtClinic";

    public const string CancellationSummary =
        "You can cancel or reschedule until 2 hours before the visit. Pay the consultation fee at the clinic.";

    public const string SameDayRescheduleSummary =
        "After missing a visit, you can move it to another open slot on the same day before clinic closing.";
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

    /// <summary>Patient-facing visit token (e.g. A1). Separate from <see cref="Reference"/>.</summary>
    public string? TokenLabel { get; private set; }

    public int? TokenNumber { get; private set; }

    public Guid AccountUserId { get; private set; }

    public Guid? FamilyMemberId { get; private set; }

    public string PatientName { get; private set; } = string.Empty;

    public Guid DoctorId { get; private set; }

    public Guid ClinicId { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid ScheduleId { get; private set; }

    /// <summary>
    /// Snapshot of clinic/branch display name at booking time so historical appointments keep the original place label.
    /// </summary>
    public string? LocationClinicName { get; private set; }

    public string? LocationBranchName { get; private set; }

    /// <summary>Formatted practice address captured when the appointment was booked.</summary>
    public string? LocationAddress { get; private set; }

    public decimal? LocationLatitude { get; private set; }

    public decimal? LocationLongitude { get; private set; }

    public string? LocationGooglePlaceId { get; private set; }

    /// <summary>
    /// Snapshot of the schedule booking mode at hold/create time. Survives later mode changes.
    /// </summary>
    public ScheduleBookingMode BookingMode { get; private set; } = ScheduleBookingMode.TimedSlots;

    public DateTimeOffset StartsAtUtc { get; private set; }

    public DateTimeOffset EndsAtUtc { get; private set; }

    /// <summary>
    /// Clinic-local calendar date of the visit (from <see cref="StartsAtUtc"/> + <see cref="TimeZoneId"/>).
    /// Used for patient+doctor same-day duplicate prevention.
    /// </summary>
    public DateOnly ClinicLocalDate { get; private set; }

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

    /// <summary>Count of same-day missed-appointment reschedules applied by the patient.</summary>
    public int SameDayRescheduleCount { get; private set; }

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
        DateTimeOffset now,
        ScheduleBookingMode bookingMode = ScheduleBookingMode.TimedSlots)
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
            BookingMode = bookingMode,
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = endsAtUtc,
            TimeZoneId = timeZoneId,
            ClinicLocalDate = ToClinicLocalDate(startsAtUtc, timeZoneId),
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
        DateTimeOffset now,
        ScheduleBookingMode bookingMode = ScheduleBookingMode.TimedSlots)
    {
        var appointment = Hold(
            staffUserId,
            doctorId,
            clinicId,
            branchId,
            scheduleId,
            startsAtUtc,
            endsAtUtc,
            timeZoneId,
            consultationFee,
            false,
            now,
            bookingMode);
        appointment.IsWalkIn = true;
        appointment.Confirm(patientName, null, null, null, now);
        return appointment;
    }

    /// <summary>
    /// Desk booking for a scheduled slot. Skips patient-self approval; staff authority confirms immediately.
    /// </summary>
    public static Appointment DeskBook(
        Guid accountUserId,
        string patientName,
        Guid doctorId,
        Guid clinicId,
        Guid branchId,
        Guid scheduleId,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        string timeZoneId,
        decimal consultationFee,
        string? reason,
        string? notes,
        DateTimeOffset now,
        ScheduleBookingMode bookingMode = ScheduleBookingMode.TimedSlots)
    {
        var appointment = Hold(
            accountUserId,
            doctorId,
            clinicId,
            branchId,
            scheduleId,
            startsAtUtc,
            endsAtUtc,
            timeZoneId,
            consultationFee,
            false,
            now,
            bookingMode);
        appointment.Confirm(patientName, null, reason, notes, now);
        return appointment;
    }

    /// <summary>
    /// Persists the resolved practice location so later clinic address edits do not rewrite history.
    /// Call when creating a hold/walk-in, or when the appointment is reassigned to a different clinic/branch.
    /// </summary>
    public void CaptureLocation(
        string clinicName,
        string? branchName,
        string address,
        decimal? latitude,
        decimal? longitude,
        string? googlePlaceId,
        DateTimeOffset now)
    {
        PracticeLocation.EnsureCoordinates(latitude, longitude);
        LocationClinicName = string.IsNullOrWhiteSpace(clinicName) ? null : clinicName.Trim();
        LocationBranchName = string.IsNullOrWhiteSpace(branchName) ? null : branchName.Trim();
        LocationAddress = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        LocationLatitude = latitude;
        LocationLongitude = longitude;
        LocationGooglePlaceId = PracticeLocation.OptionalGooglePlaceId(googlePlaceId);
        UpdatedAtUtc = now;
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

    public void AssignVisitToken(string label, int number, DateTimeOffset now)
    {
        if (TokenNumber is not null || !string.IsNullOrWhiteSpace(TokenLabel))
        {
            return;
        }

        if (number <= 0 || string.IsNullOrWhiteSpace(label))
        {
            throw DomainException.Validation("token_invalid", "A valid visit token is required.");
        }

        TokenNumber = number;
        TokenLabel = label.Trim();
        UpdatedAtUtc = now;
    }

    /// <summary>Clears the visit token so a new one can be allocated (e.g. after moving to another operating day).</summary>
    public void ClearVisitToken(DateTimeOffset now)
    {
        if (TokenNumber is null && string.IsNullOrWhiteSpace(TokenLabel))
        {
            return;
        }

        TokenNumber = null;
        TokenLabel = null;
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

    /// <summary>
    /// Doctor opening the clinical workspace: approve pending requests (even after the
    /// patient approval window) and check in confirmed visits so consultation can start.
    /// </summary>
    public void PrepareForDoctorConsultation(DateTimeOffset now)
    {
        if (Status is AppointmentStatus.CheckedIn or AppointmentStatus.Completed)
        {
            return;
        }

        if (Status == AppointmentStatus.Requested)
        {
            Status = AppointmentStatus.Confirmed;
            ApprovalDeadlineUtc = null;
            UpdatedAtUtc = now;
        }

        if (Status == AppointmentStatus.Confirmed)
        {
            Status = AppointmentStatus.CheckedIn;
            UpdatedAtUtc = now;
            return;
        }

        throw DomainException.Conflict(
            "consultation_not_ready",
            $"This visit cannot be opened for consultation while it is {Status}.");
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

        if (!staff)
        {
            if (StartsAtUtc <= now)
            {
                throw DomainException.Conflict("cancellation_closed", "This visit has already started.");
            }

            if (StartsAtUtc <= now.Add(AppointmentPolicy.PatientChangeCutoff))
            {
                throw DomainException.Conflict("cancellation_closed", AppointmentPolicy.CancellationSummary);
            }
        }
        else if (StartsAtUtc <= now)
        {
            throw DomainException.Conflict("cancellation_closed", "This visit has already started.");
        }

        Status = AppointmentStatus.Cancelled;
        DecisionNote = Optional(reason, 120, "reason_invalid", "Enter a reason of 120 characters or fewer.") ?? DecisionNote;
        UpdatedAtUtc = now;
    }

    public void Reschedule(
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        bool staff,
        DateTimeOffset now,
        string? reason = null,
        bool sameDayMissed = false)
    {
        if (endsAtUtc <= startsAtUtc || startsAtUtc <= now)
        {
            throw DomainException.Validation("slot_invalid", "Choose a time that has not already started.");
        }

        if (Status is not (AppointmentStatus.Requested or AppointmentStatus.Confirmed))
        {
            throw DomainException.Conflict("appointment_closed", "This appointment can no longer be changed.");
        }

        if (!staff)
        {
            if (sameDayMissed)
            {
                if (Status != AppointmentStatus.Confirmed)
                {
                    throw DomainException.Conflict("appointment_closed", "Only a confirmed visit can be moved after missing the time.");
                }

                if (StartsAtUtc > now)
                {
                    throw DomainException.Conflict("reschedule_not_missed", "Same-day reschedule is only available after the visit start time.");
                }

                if (SameDayRescheduleCount >= AppointmentPolicy.MaxSameDayReschedules)
                {
                    throw DomainException.Conflict(
                        "reschedule_limit",
                        $"You can reschedule a missed visit at most {AppointmentPolicy.MaxSameDayReschedules} times on the same day.");
                }

                SameDayRescheduleCount += 1;
            }
            else
            {
                if (StartsAtUtc <= now)
                {
                    throw DomainException.Conflict("cancellation_closed", "This visit has already started.");
                }

                if (StartsAtUtc <= now.Add(AppointmentPolicy.PatientChangeCutoff))
                {
                    throw DomainException.Conflict("cancellation_closed", AppointmentPolicy.CancellationSummary);
                }
            }
        }
        else if (StartsAtUtc <= now && !sameDayMissed)
        {
            throw DomainException.Conflict("cancellation_closed", "This visit has already started.");
        }

        // Keep the same appointment row — never create a second booking. Slot occupancy flips via SaveMoveAsync.
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        ClinicLocalDate = ToClinicLocalDate(startsAtUtc, TimeZoneId);
        Status = RequiresApproval && !staff && !sameDayMissed ? AppointmentStatus.Requested : AppointmentStatus.Confirmed;
        if (Status == AppointmentStatus.Requested)
        {
            ApprovalDeadlineUtc = now.Add(AppointmentPolicy.ApprovalWindow);
        }
        else
        {
            ApprovalDeadlineUtc = null;
        }

        HoldExpiresAtUtc = null;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            DecisionNote = Optional(reason, 120, "reason_invalid", "Enter a reason of 120 characters or fewer.");
        }

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
        if (string.IsNullOrWhiteSpace(name))
        {
            throw DomainException.Validation("patient_name_mandatory", "Patient name is mandatory.");
        }

        if (name.Length is < 2 or > 80)
        {
            throw DomainException.Validation("name_invalid", "Enter a name from 2 to 80 characters.");
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

    public static DateOnly ToClinicLocalDate(DateTimeOffset startsAtUtc, string timeZoneId)
    {
        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var resolved) ? resolved : TimeZoneInfo.Utc;
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(startsAtUtc, zone).DateTime);
    }
}
