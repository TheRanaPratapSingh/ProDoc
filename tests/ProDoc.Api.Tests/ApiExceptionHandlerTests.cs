using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ProDoc.Api.Errors;
using ProDoc.Domain.Common;

namespace ProDoc.Api.Tests;

public sealed class ApiExceptionHandlerTests
{
    [Fact]
    public void Domain_validation_maps_to_400()
    {
        var mapped = ApiExceptionHandler.Map(DomainException.Validation("phone_invalid", "Enter a valid Indian mobile number."), false);

        Assert.Equal(StatusCodes.Status400BadRequest, mapped.Status);
        Assert.Equal("phone_invalid", mapped.Code);
        Assert.Null(mapped.Detail);
    }

    [Fact]
    public void Domain_unprocessable_maps_to_422()
    {
        var mapped = ApiExceptionHandler.Map(DomainException.Unprocessable("state_invalid", "Complete prior steps first."), false);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, mapped.Status);
        Assert.Equal("state_invalid", mapped.Code);
    }

    [Fact]
    public void Domain_conflict_and_rate_limit_map_correctly()
    {
        Assert.Equal(409, ApiExceptionHandler.Map(DomainException.Conflict("dup", "Exists."), false).Status);
        Assert.Equal(429, ApiExceptionHandler.Map(DomainException.TooManyRequests("otp_cooldown", "Wait."), false).Status);
        Assert.Equal(401, ApiExceptionHandler.Map(DomainException.Unauthorized("denied", "Denied."), false).Status);
        Assert.Equal(403, ApiExceptionHandler.Map(DomainException.Forbidden("forbidden", "No."), false).Status);
        Assert.Equal(404, ApiExceptionHandler.Map(DomainException.NotFound("missing", "Gone."), false).Status);
    }

    [Fact]
    public void Fluent_validation_maps_to_validation_problem()
    {
        var exception = new ValidationException([
            new ValidationFailure("PhoneNumber", "Enter a valid Indian mobile number."),
            new ValidationFailure("Code", "Enter the 6-digit code.")
        ]);

        var mapped = ApiExceptionHandler.Map(exception, false);

        Assert.Equal(StatusCodes.Status400BadRequest, mapped.Status);
        Assert.Equal("validation_failed", mapped.Code);
        Assert.NotNull(mapped.ValidationErrors);
        Assert.Contains("PhoneNumber", mapped.ValidationErrors.Keys);
        Assert.Contains("Code", mapped.ValidationErrors.Keys);
    }

    [Fact]
    public void Concurrency_conflict_maps_to_409()
    {
        var mapped = ApiExceptionHandler.Map(new DbUpdateConcurrencyException("row changed"), false);

        Assert.Equal(StatusCodes.Status409Conflict, mapped.Status);
        Assert.Equal("concurrency_conflict", mapped.Code);
        Assert.Null(mapped.Detail);
    }

    [Fact]
    public void Database_errors_never_expose_provider_detail()
    {
        var mapped = ApiExceptionHandler.Map(
            new DbUpdateException("SqlException: Login failed for user 'sa' with password secret"),
            isDevelopment: true);

        Assert.Equal(StatusCodes.Status500InternalServerError, mapped.Status);
        Assert.Equal("database_error", mapped.Code);
        Assert.Null(mapped.Detail);
    }

    [Fact]
    public void Cancellation_maps_to_408()
    {
        var mapped = ApiExceptionHandler.Map(new OperationCanceledException(), false);

        Assert.Equal(StatusCodes.Status408RequestTimeout, mapped.Status);
        Assert.Equal("request_cancelled", mapped.Code);
    }

    [Fact]
    public void Unexpected_errors_hide_detail_outside_development()
    {
        var production = ApiExceptionHandler.Map(new InvalidOperationException("boom"), false);
        var development = ApiExceptionHandler.Map(new InvalidOperationException("boom"), true);

        Assert.Equal("server_error", production.Code);
        Assert.Null(production.Detail);
        Assert.Equal("boom", development.Detail);
    }
}
