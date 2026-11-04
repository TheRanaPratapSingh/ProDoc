using ProDoc.Domain.Queue;

namespace ProDoc.Domain.Tests;

public sealed class QueueEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Call_start_and_complete_follow_the_state_machine()
    {
        var session = ClinicQueueSession.Open(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(Now.DateTime), Now);
        var entry = QueueEntry.CheckInWalkIn(session, Guid.NewGuid(), "Guest", "A001", 1, 1, Now);
        var actor = Guid.NewGuid();

        entry.Call(actor, Now);
        Assert.Equal(QueueEntryStatus.Called, entry.Status);
        entry.StartConsultation(actor, Now);
        Assert.Equal(QueueEntryStatus.InConsultation, entry.Status);
        entry.Complete(actor, Now);
        Assert.Equal(QueueEntryStatus.Completed, entry.Status);
    }

    [Fact]
    public void Skip_does_not_leave_an_active_queue_entry()
    {
        var session = ClinicQueueSession.Open(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(Now.DateTime), Now);
        var entry = QueueEntry.CheckInWalkIn(session, Guid.NewGuid(), "Guest", "A001", 1, 1, Now);
        entry.Skip(Guid.NewGuid(), "No show", Now);

        Assert.Equal(QueueEntryStatus.Skipped, entry.Status);
        Assert.False(entry.IsActive);
    }
}
