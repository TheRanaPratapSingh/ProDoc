using ProDoc.Domain.Appointments;

namespace ProDoc.Domain.Tests;

public sealed class AppointmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Instant_confirmation_snapshots_the_fee_and_reference()
    {
        var appointment = Appointment.Hold(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now.AddHours(4), Now.AddHours(4).AddMinutes(20), "UTC", 500, false, Now);
        appointment.Confirm("Asha Rao", null, "Fever", "Since morning", Now);

        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
        Assert.Equal(500, appointment.ConsultationFee);
        Assert.Equal("PayAtClinic", appointment.PaymentMethod);
        Assert.StartsWith("APT-2026-", appointment.Reference);
        Assert.Equal("Fever", appointment.Reason);
    }

    [Fact]
    public void Approval_booking_stays_requested_until_the_doctor_decides()
    {
        var appointment = Held(requiresApproval: true, startsIn: TimeSpan.FromHours(6));
        appointment.Confirm("Asha Rao", null, null, null, Now);

        Assert.Equal(AppointmentStatus.Requested, appointment.Status);
        appointment.Approve(Now);
        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
    }

    [Fact]
    public void Approval_is_rejected_when_the_visit_is_within_thirty_minutes()
    {
        var appointment = Held(requiresApproval: true, startsIn: TimeSpan.FromMinutes(20));

        var error = Assert.Throws<ProDoc.Domain.Common.DomainException>(() => appointment.Confirm("Asha Rao", null, null, null, Now));

        Assert.Equal("approval_window_closed", error.Code);
        Assert.Equal(AppointmentStatus.Held, appointment.Status);
    }

    [Fact]
    public void Patient_cannot_cancel_inside_the_two_hour_window()
    {
        var appointment = Held(requiresApproval: false, startsIn: TimeSpan.FromHours(3));
        appointment.Confirm("Asha Rao", null, null, null, Now);

        var error = Assert.Throws<ProDoc.Domain.Common.DomainException>(() => appointment.Cancel(staff: false, null, Now.AddHours(2)));

        Assert.Equal("cancellation_closed", error.Code);
        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
    }

    [Fact]
    public void Patient_cannot_cancel_or_reschedule_after_start_time()
    {
        var appointment = Held(requiresApproval: false, startsIn: TimeSpan.FromHours(3));
        appointment.Confirm("Asha Rao", null, null, null, Now);
        var afterStart = appointment.StartsAtUtc;

        var cancelError = Assert.Throws<ProDoc.Domain.Common.DomainException>(() =>
            appointment.Cancel(staff: false, null, afterStart));
        Assert.Equal("cancellation_closed", cancelError.Code);

        var moveError = Assert.Throws<ProDoc.Domain.Common.DomainException>(() =>
            appointment.Reschedule(afterStart.AddHours(24), afterStart.AddHours(24).AddMinutes(20), staff: false, afterStart));
        Assert.Equal("cancellation_closed", moveError.Code);
        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
    }

    [Fact]
    public void Patient_cannot_cancel_exactly_at_start_time()
    {
        var appointment = Held(requiresApproval: false, startsIn: TimeSpan.FromHours(4));
        appointment.Confirm("Asha Rao", null, null, null, Now);

        var error = Assert.Throws<ProDoc.Domain.Common.DomainException>(() =>
            appointment.Cancel(staff: false, null, appointment.StartsAtUtc));

        Assert.Equal("cancellation_closed", error.Code);
    }

    [Fact]
    public void Patient_can_cancel_before_the_two_hour_window()
    {
        var appointment = Held(requiresApproval: false, startsIn: TimeSpan.FromHours(5));
        appointment.Confirm("Asha Rao", null, null, null, Now);

        appointment.Cancel(staff: false, "Plans changed", Now.AddMinutes(30));
        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
    }

    [Fact]
    public void Expired_hold_cannot_be_confirmed()
    {
        var appointment = Held(requiresApproval: false, startsIn: TimeSpan.FromHours(4));

        var error = Assert.Throws<ProDoc.Domain.Common.DomainException>(() => appointment.Confirm("Asha Rao", null, null, null, Now.AddMinutes(6)));

        Assert.Equal("hold_expired", error.Code);
        Assert.Equal(AppointmentStatus.Expired, appointment.Status);
    }

    [Fact]
    public void Patient_can_same_day_reschedule_after_missing_start()
    {
        var appointment = Held(requiresApproval: false, startsIn: TimeSpan.FromHours(1));
        appointment.Confirm("Asha Rao", null, null, null, Now);
        var afterStart = appointment.StartsAtUtc.AddMinutes(5);
        var nextStart = afterStart.AddHours(1);
        var nextEnd = nextStart.AddMinutes(20);

        appointment.Reschedule(nextStart, nextEnd, staff: false, afterStart, reason: "Running late", sameDayMissed: true);

        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
        Assert.Equal(nextStart, appointment.StartsAtUtc);
        Assert.Equal(1, appointment.SameDayRescheduleCount);
    }

    [Fact]
    public void Patient_same_day_reschedule_respects_limit()
    {
        var appointment = Held(requiresApproval: false, startsIn: TimeSpan.FromHours(1));
        appointment.Confirm("Asha Rao", null, null, null, Now);
        var afterStart = appointment.StartsAtUtc.AddMinutes(5);

        appointment.Reschedule(afterStart.AddHours(1), afterStart.AddHours(1).AddMinutes(20), staff: false, afterStart, sameDayMissed: true);
        appointment.Reschedule(afterStart.AddHours(2), afterStart.AddHours(2).AddMinutes(20), staff: false, afterStart.AddHours(1).AddMinutes(5), sameDayMissed: true);

        var error = Assert.Throws<ProDoc.Domain.Common.DomainException>(() =>
            appointment.Reschedule(
                afterStart.AddHours(3),
                afterStart.AddHours(3).AddMinutes(20),
                staff: false,
                afterStart.AddHours(2).AddMinutes(5),
                sameDayMissed: true));

        Assert.Equal("reschedule_limit", error.Code);
        Assert.Equal(2, appointment.SameDayRescheduleCount);
    }

    [Fact]
    public void Completed_visit_cannot_be_same_day_rescheduled()
    {
        var appointment = Held(requiresApproval: false, startsIn: TimeSpan.FromHours(1));
        appointment.Confirm("Asha Rao", null, null, null, Now);
        appointment.MarkCheckedIn(appointment.StartsAtUtc.AddMinutes(1));
        appointment.MarkCompleted(appointment.StartsAtUtc.AddMinutes(30));

        var error = Assert.Throws<ProDoc.Domain.Common.DomainException>(() =>
            appointment.Reschedule(
                appointment.StartsAtUtc.AddHours(2),
                appointment.StartsAtUtc.AddHours(2).AddMinutes(20),
                staff: false,
                appointment.StartsAtUtc.AddMinutes(45),
                sameDayMissed: true));

        Assert.Equal("appointment_closed", error.Code);
    }

    private static Appointment Held(bool requiresApproval, TimeSpan startsIn) =>
        Appointment.Hold(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.Add(startsIn),
            Now.Add(startsIn).AddMinutes(20),
            "UTC",
            500,
            requiresApproval,
            Now);
}
