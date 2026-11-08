using ProDoc.Domain.Clinical;
using ProDoc.Domain.Common;

namespace ProDoc.Domain.Tests.Clinical;

public sealed class PatientFollowUpTests
{
    [Fact]
    public void Create_rejects_non_future_dates()
    {
        var today = new DateOnly(2026, 11, 7);
        var ex = Assert.Throws<DomainException>(() =>
            PatientFollowUp.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Akash Rana",
                today,
                today,
                DateTimeOffset.UtcNow));

        Assert.Equal("follow_up_date_invalid", ex.Code);
    }

    [Fact]
    public void Display_status_does_not_auto_complete_when_overdue()
    {
        var today = new DateOnly(2026, 11, 14);
        var followUp = PatientFollowUp.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Akash Rana",
            today.AddDays(7),
            today,
            DateTimeOffset.UtcNow);

        Assert.Equal("Recommended", followUp.ResolveDisplayStatus(today));
        Assert.Equal("Overdue", followUp.ResolveDisplayStatus(today.AddDays(10)));
        Assert.Equal(FollowUpStatus.Recommended, followUp.Status);
    }

    [Fact]
    public void Link_appointment_sets_booked_without_losing_recommendation_date()
    {
        var today = new DateOnly(2026, 11, 7);
        var followUp = PatientFollowUp.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Akash Rana",
            today.AddDays(7),
            today,
            DateTimeOffset.UtcNow,
            note: "Follow up for consultation review.");

        var appointmentId = Guid.NewGuid();
        followUp.LinkAppointment(appointmentId, DateTimeOffset.UtcNow);

        Assert.Equal(FollowUpStatus.AppointmentBooked, followUp.Status);
        Assert.Equal(appointmentId, followUp.LinkedAppointmentId);
        Assert.Equal("Follow up for consultation review.", followUp.FollowUpNote);
        Assert.Equal("Appointment Booked", followUp.ResolveDisplayStatus(today));
    }

    [Fact]
    public void Cancel_stops_further_edits()
    {
        var today = new DateOnly(2026, 11, 7);
        var followUp = PatientFollowUp.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Akash Rana",
            today.AddDays(7),
            today,
            DateTimeOffset.UtcNow);

        followUp.Cancel(DateTimeOffset.UtcNow);
        var ex = Assert.Throws<DomainException>(() =>
            followUp.Update(today.AddDays(10), today, "note", true, DateTimeOffset.UtcNow));
        Assert.Equal("follow_up_cancelled", ex.Code);
    }
}
