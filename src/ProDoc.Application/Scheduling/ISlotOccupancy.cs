namespace ProDoc.Application.Scheduling;

public interface ISlotOccupancy
{
    /// <summary>
    /// Exact local start times that are fully occupied (timed-slot exclusive booking).
    /// </summary>
    Task<IReadOnlySet<TimeOnly>> OccupiedLocalStartsAsync(
        Guid doctorId,
        Guid branchId,
        DateOnly date,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        Guid? exceptAppointmentId = null);

    /// <summary>
    /// Active booking counts keyed by local range start (hourly queue capacity).
    /// </summary>
    Task<IReadOnlyDictionary<TimeOnly, int>> OccupancyCountsAsync(
        Guid doctorId,
        Guid branchId,
        DateOnly date,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        Guid? exceptAppointmentId = null);
}
