namespace ProDoc.Domain.Scheduling;

/// <summary>
/// Mutually exclusive patient booking experience for a consultation schedule
/// (doctor × clinic × branch).
/// </summary>
public enum ScheduleBookingMode
{
    /// <summary>Classic calendar booking into exclusive fixed-duration slots.</summary>
    TimedSlots = 0,

    /// <summary>Patients pick an hourly range and receive a sequential queue token (A1, B1, …).</summary>
    HourlyQueue = 1
}
