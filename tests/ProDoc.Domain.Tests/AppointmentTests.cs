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
    public void Expired_hold_cannot_be_confirmed()
    {
        var appointment = Held(requiresApproval: false, startsIn: TimeSpan.FromHours(4));

        var error = Assert.Throws<ProDoc.Domain.Common.DomainException>(() => appointment.Confirm("Asha Rao", null, null, null, Now.AddMinutes(6)));

        Assert.Equal("hold_expired", error.Code);
        Assert.Equal(AppointmentStatus.Expired, appointment.Status);
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
