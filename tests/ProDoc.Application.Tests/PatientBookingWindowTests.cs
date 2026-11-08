using ProDoc.Application.Scheduling;

namespace ProDoc.Application.Tests;

public sealed class PatientBookingWindowTests
{
    [Theory]
    [InlineData(2026, 11, 6, 2026, 11, 2, 2026, 11, 15)] // Friday → Mon 2 .. Sun 15
    [InlineData(2026, 11, 2, 2026, 11, 2, 2026, 11, 15)] // Monday
    [InlineData(2026, 11, 8, 2026, 11, 2, 2026, 11, 15)] // Sunday
    [InlineData(2026, 12, 31, 2026, 12, 28, 2027, 1, 10)] // year boundary
    public void Patient_window_is_current_and_next_week(
        int y, int m, int d,
        int sy, int sm, int sd,
        int ey, int em, int ed)
    {
        var today = new DateOnly(y, m, d);
        var (start, end) = ScheduleService.ResolveHorizonWindow(today, AvailabilityHorizon.PatientTwoWeeks);
        Assert.Equal(new DateOnly(sy, sm, sd), start);
        Assert.Equal(new DateOnly(ey, em, ed), end);
        Assert.Equal(14, end.DayNumber - start.DayNumber + 1);
        Assert.Equal(DayOfWeek.Monday, start.DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, end.DayOfWeek);
    }

    [Fact]
    public void Staff_window_is_today_through_sixty_days()
    {
        var today = new DateOnly(2026, 11, 6);
        var (start, end) = ScheduleService.ResolveHorizonWindow(today, AvailabilityHorizon.StaffExtended);
        Assert.Equal(today, start);
        Assert.Equal(today.AddDays(60), end);
    }
}
