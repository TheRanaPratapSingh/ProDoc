using ProDoc.Domain.Common;

namespace ProDoc.Domain.Tests;

public class AppResultAndDateValidationTests
{
    [Fact]
    public void AppResult_success_has_no_error()
    {
        var result = AppResult<int>.Success(42);
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void AppResult_failure_keeps_stable_code_and_status()
    {
        var result = AppResult<string>.Validation("phone_invalid", "Enter a valid Indian mobile number.");
        Assert.True(result.IsFailure);
        Assert.Equal("phone_invalid", result.Error!.Code);
        Assert.Equal(400, result.Error.StatusCode);
    }

    [Fact]
    public void Not_found_and_conflict_use_expected_status_codes()
    {
        Assert.Equal(404, AppResult.NotFound("appointment_not_found", "Missing").Error!.StatusCode);
        Assert.Equal(409, AppResult.Conflict("slot_unavailable", "Taken").Error!.StatusCode);
        Assert.Equal(429, AppResult.TooManyRequests("otp_cooldown", "Wait").Error!.StatusCode);
    }

    [Fact]
    public void Date_range_rejects_start_after_end_without_throwing()
    {
        var error = DateValidation.ValidateInclusiveRange(new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 1));
        Assert.NotNull(error);
        Assert.Equal("date_range_invalid", error!.Code);
        Assert.Equal(400, error.StatusCode);
        Assert.Contains("startDate", error.Errors!.Keys);
    }

    [Fact]
    public void Date_range_rejects_spans_over_31_days()
    {
        var error = DateValidation.ValidateInclusiveRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 15));
        Assert.NotNull(error);
        Assert.Equal("date_range_too_large", error!.Code);
    }

    [Fact]
    public void Past_appointment_date_is_rejected()
    {
        var now = new DateTimeOffset(2026, 11, 8, 6, 0, 0, TimeSpan.Zero);
        var error = DateValidation.ValidateFutureAppointmentDate(new DateOnly(2026, 11, 1), now, "Asia/Kolkata");
        Assert.NotNull(error);
        Assert.Equal("appointment_date_past", error!.Code);
    }

    [Fact]
    public void Follow_up_must_be_after_today()
    {
        var now = new DateTimeOffset(2026, 11, 8, 6, 0, 0, TimeSpan.Zero);
        var today = DateValidation.TodayInClinic(now, "Asia/Kolkata");
        Assert.NotNull(DateValidation.ValidateFollowUpDate(today, now, "Asia/Kolkata"));
        Assert.Null(DateValidation.ValidateFollowUpDate(today.AddDays(1), now, "Asia/Kolkata"));
    }

    [Fact]
    public void DomainException_maps_to_AppError_without_losing_status()
    {
        var mapped = DomainException.Conflict("duplicate_appointment", "Already booked").ToAppError();
        Assert.Equal(409, mapped.StatusCode);
        Assert.Equal("duplicate_appointment", mapped.Code);
    }
}
