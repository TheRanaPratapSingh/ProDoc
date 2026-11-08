namespace ProDoc.Domain.Common;

/// <summary>
/// Reusable date / date-range rules for clinic-local workflows (Asia/Kolkata by default).
/// Returns <see cref="AppError"/> — never throws for ordinary invalid user input.
/// </summary>
public static class DateValidation
{
    public const string DefaultClinicTimeZoneId = "Asia/Kolkata";

    public static TimeZoneInfo ClinicZone(string? timeZoneId = null)
    {
        var id = string.IsNullOrWhiteSpace(timeZoneId) ? DefaultClinicTimeZoneId : timeZoneId.Trim();
        if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
        {
            return zone;
        }

        if (TimeZoneInfo.TryFindSystemTimeZoneById(DefaultClinicTimeZoneId, out zone)
            || TimeZoneInfo.TryFindSystemTimeZoneById("India Standard Time", out zone))
        {
            return zone;
        }

        return TimeZoneInfo.Utc;
    }

    public static DateOnly TodayInClinic(DateTimeOffset utcNow, string? timeZoneId = null) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, ClinicZone(timeZoneId)).DateTime);

    /// <summary>
    /// Validates that <paramref name="to"/> is on or after <paramref name="from"/> and within <paramref name="maxInclusiveDays"/>.
    /// </summary>
    public static AppError? ValidateInclusiveRange(DateOnly from, DateOnly to, int maxInclusiveDays = 31)
    {
        if (to < from)
        {
            return AppError.Validation(
                "date_range_invalid",
                "Start date cannot be later than end date.",
                new Dictionary<string, string[]>
                {
                    ["startDate"] = ["Start date cannot be later than end date."],
                    ["endDate"] = ["End date must be greater than or equal to start date."]
                });
        }

        if (to.DayNumber - from.DayNumber > maxInclusiveDays)
        {
            return AppError.Validation(
                "date_range_too_large",
                $"Choose a range of {maxInclusiveDays} days or fewer.",
                new Dictionary<string, string[]>
                {
                    ["endDate"] = [$"Choose a range of {maxInclusiveDays} days or fewer."]
                });
        }

        return null;
    }

    public static AppError? ValidateFutureAppointmentDate(
        DateOnly date,
        DateTimeOffset utcNow,
        string? timeZoneId = null,
        DateOnly? latestInclusive = null)
    {
        var today = TodayInClinic(utcNow, timeZoneId);
        if (date < today)
        {
            return AppError.Validation(
                "appointment_date_past",
                "Please select an available future appointment date.",
                new Dictionary<string, string[]>
                {
                    ["date"] = ["Please select an available future appointment date."]
                });
        }

        if (latestInclusive is not null && date > latestInclusive)
        {
            return AppError.Validation(
                "appointment_date_outside_window",
                "Appointments are available only within the permitted booking period.",
                new Dictionary<string, string[]>
                {
                    ["date"] = ["Appointments are available only within the permitted booking period."]
                });
        }

        return null;
    }

    public static AppError? ValidateFollowUpDate(
        DateOnly date,
        DateTimeOffset utcNow,
        string? timeZoneId = null)
    {
        var today = TodayInClinic(utcNow, timeZoneId);
        if (date <= today)
        {
            return AppError.Validation(
                "follow_up_date_invalid",
                "Follow-up date must be in the future.",
                new Dictionary<string, string[]>
                {
                    ["followUpDate"] = ["Follow-up date must be in the future."]
                });
        }

        return null;
    }

    public static AppError? ValidateNotFutureDateOfBirth(DateOnly dateOfBirth, DateTimeOffset utcNow, string? timeZoneId = null)
    {
        var today = TodayInClinic(utcNow, timeZoneId);
        if (dateOfBirth > today)
        {
            return AppError.Validation(
                "date_of_birth_invalid",
                "Date of birth cannot be in the future.",
                new Dictionary<string, string[]>
                {
                    ["dateOfBirth"] = ["Date of birth cannot be in the future."]
                });
        }

        return null;
    }
}
