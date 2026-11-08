using ProDoc.Domain.Common;
using ProDoc.Domain.Scheduling;

namespace ProDoc.Domain.Tests;

public sealed class ScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Slots_skip_breaks_and_a_closed_day()
    {
        var schedule = ConsultationSchedule.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 700.50m, 30, Now);
        schedule.ReplaceHours(
            [
                (DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(12, 0)),
                (DayOfWeek.Wednesday, new TimeOnly(14, 0), new TimeOnly(15, 0))
            ],
            Now);
        schedule.ReplaceBreaks([(DayOfWeek.Wednesday, new TimeOnly(10, 0), new TimeOnly(10, 30))], Now);
        schedule.Publish(Now);

        var slots = schedule.GenerateSlots(new DateOnly(2026, 11, 4));
        Assert.Equal(new TimeOnly(9, 0), slots[0].Start);
        Assert.DoesNotContain(slots, slot => slot.Start == new TimeOnly(10, 0));
        Assert.Contains(slots, slot => slot.Start == new TimeOnly(10, 30));
        Assert.Contains(slots, slot => slot.Start == new TimeOnly(14, 0));
        Assert.Equal(7, slots.Count);

        var closure = schedule.AddClosure(new DateOnly(2026, 11, 11), "Diwali", Now);
        Assert.Empty(schedule.GenerateSlots(new DateOnly(2026, 11, 11)));
        schedule.RemoveClosure(closure.Id, Now);
        Assert.NotEmpty(schedule.GenerateSlots(new DateOnly(2026, 11, 11)));
    }

    [Fact]
    public void A_schedule_needs_a_branch_when_it_belongs_to_a_clinic_and_a_valid_fee()
    {
        var missingBranch = Assert.Throws<DomainException>(() =>
            ConsultationSchedule.Create(Guid.NewGuid(), Guid.NewGuid(), null, 500, 15, Now));
        Assert.Equal("schedule_invalid", missingBranch.Code);

        var schedule = ConsultationSchedule.Create(Guid.NewGuid(), null, null, 0, 15, Now);
        var fee = Assert.Throws<DomainException>(() => schedule.SetFee(10.555m, Now));
        Assert.Equal("fee_invalid", fee.Code);
        var publish = Assert.Throws<DomainException>(() => schedule.Publish(Now));
        Assert.Equal("schedule_incomplete", publish.Code);
    }

    [Fact]
    public void Buffer_and_capacity_space_slots_without_overlap()
    {
        var schedule = ConsultationSchedule.Create(Guid.NewGuid(), null, null, 400, 20, Now);
        schedule.SetBuffer(5, Now);
        schedule.SetCapacity(2, Now);
        schedule.ReplaceHours([(DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(12, 0))], Now);
        var slots = schedule.GenerateSlots(new DateOnly(2026, 11, 4));
        Assert.Equal([new TimeOnly(9, 0), new TimeOnly(9, 25)], slots.Select(slot => slot.Start).ToArray());
        Assert.Equal(new TimeOnly(9, 20), slots[0].End);
        Assert.Equal(new TimeOnly(9, 45), slots[1].End);

        schedule.SetEffectivePeriod(new DateOnly(2026, 11, 5), null, Now);
        Assert.Empty(schedule.GenerateSlots(new DateOnly(2026, 11, 4)));
    }

    [Fact]
    public void Default_weekday_hours_are_monday_to_friday_eleven_to_seventeen()
    {
        var schedule = ConsultationSchedule.Create(Guid.NewGuid(), null, null, 300, 10, Now);
        schedule.ApplyDefaultWeekdayHours(Now);
        schedule.Publish(Now);

        Assert.Equal(5, schedule.Hours.Count);
        Assert.All(schedule.Hours, hour =>
        {
            Assert.Equal(new TimeOnly(11, 0), hour.StartsAt);
            Assert.Equal(new TimeOnly(17, 0), hour.EndsAt);
            Assert.InRange(hour.Day, DayOfWeek.Monday, DayOfWeek.Friday);
        });

        Assert.NotEmpty(schedule.GenerateSlots(new DateOnly(2026, 11, 6))); // Friday
        Assert.Empty(schedule.GenerateSlots(new DateOnly(2026, 11, 7))); // Saturday
        Assert.Empty(schedule.GenerateSlots(new DateOnly(2026, 11, 8))); // Sunday
        Assert.NotEmpty(schedule.GenerateSlots(new DateOnly(2026, 11, 9))); // next Monday
        Assert.NotEmpty(schedule.GenerateSlots(new DateOnly(2026, 12, 1))); // next month Tuesday
    }

    [Fact]
    public void Date_extras_replace_weekly_hours_for_that_day_only()
    {
        var schedule = ConsultationSchedule.Create(Guid.NewGuid(), null, null, 300, 30, Now);
        schedule.ApplyDefaultWeekdayHours(Now);
        var friday = new DateOnly(2026, 11, 6);
        var saturday = new DateOnly(2026, 11, 7);

        schedule.ReplaceExtrasForDate(friday, [(new TimeOnly(12, 0), new TimeOnly(16, 0))], Now);
        var fridaySlots = schedule.GenerateSlots(friday);
        Assert.Equal(new TimeOnly(12, 0), fridaySlots[0].Start);
        Assert.DoesNotContain(fridaySlots, slot => slot.Start == new TimeOnly(11, 0));
        Assert.Equal(new TimeOnly(11, 0), schedule.GenerateSlots(new DateOnly(2026, 11, 5))[0].Start);

        schedule.ReplaceExtrasForDate(saturday, [(new TimeOnly(10, 0), new TimeOnly(12, 0))], Now);
        Assert.NotEmpty(schedule.GenerateSlots(saturday));

        schedule.ReplaceExtrasForDate(friday, [], Now);
        Assert.Equal(new TimeOnly(11, 0), schedule.GenerateSlots(friday)[0].Start);
    }

    [Fact]
    public void Closure_blocks_slots_without_changing_weekly_template()
    {
        var schedule = ConsultationSchedule.Create(Guid.NewGuid(), null, null, 300, 10, Now);
        schedule.ApplyDefaultWeekdayHours(Now);
        var leaveDate = new DateOnly(2026, 11, 10);
        schedule.AddClosure(leaveDate, "Personal Leave", Now);
        Assert.Empty(schedule.GenerateSlots(leaveDate));
        Assert.NotEmpty(schedule.GenerateSlots(new DateOnly(2026, 11, 11)));
    }
}
