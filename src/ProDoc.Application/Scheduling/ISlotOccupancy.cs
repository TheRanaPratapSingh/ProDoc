namespace ProDoc.Application.Scheduling;

public interface ISlotOccupancy
{
    Task<IReadOnlySet<TimeOnly>> OccupiedLocalStartsAsync(
        Guid doctorId,
        Guid branchId,
        DateOnly date,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
