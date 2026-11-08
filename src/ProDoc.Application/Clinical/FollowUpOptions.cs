namespace ProDoc.Application.Clinical;

/// <summary>
/// Configurable follow-up reminder offsets (days before recommended date, Asia/Kolkata local midnight).
/// Defaults: 3 days before, 1 day before, and on the due date (0).
/// </summary>
public sealed class FollowUpOptions
{
    public const string SectionName = "FollowUps";

    /// <summary>Days before the recommended date to remind (include 0 for due-date). Sorted unique non-negative.</summary>
    public int[] ReminderDaysBefore { get; set; } = [3, 1, 0];

    /// <summary>Local hour (0-23) in Asia/Kolkata when day-of reminders are considered due.</summary>
    public int ReminderLocalHour { get; set; } = 9;
}
