using Microsoft.AspNetCore.Mvc;
using ProDoc.Application.Auth;
using ProDoc.Domain.Common;

namespace ProDoc.Api.Errors;

/// <summary>
/// Maps expected domain/auth failures to ProblemDetails without treating them as unhandled 500s.
/// Prefer <see cref="FromAppError"/> / <see cref="FromAppResult"/> so callers never throw for expected outcomes.
/// </summary>
public static class DomainProblemResults
{
    public static ObjectResult FromAuthFailure(AuthFailure failure) =>
        ToObjectResult(failure.StatusCode, failure.Code, failure.Message);

    public static ObjectResult FromDomain(DomainException exception) =>
        ToObjectResult(exception.StatusCode, exception.Code, exception.Title);

    public static ObjectResult FromAppError(AppError error) =>
        ToObjectResult(error.StatusCode, error.Code, error.Message, error.Errors);

    public static ObjectResult FromAppResult(AppResult result) =>
        result.IsSuccess
            ? new ObjectResult(null) { StatusCode = StatusCodes.Status204NoContent }
            : FromAppError(result.Error!);

    public static ObjectResult ToObjectResult(
        int statusCode,
        string code,
        string message,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        if (errors is { Count: > 0 })
        {
            var validation = new ValidationProblemDetails(errors.ToDictionary(pair => pair.Key, pair => pair.Value))
            {
                Status = statusCode,
                Title = message,
                Detail = message,
                Type = $"urn:prodoc:{code}"
            };
            validation.Extensions["code"] = code;
            validation.Extensions["success"] = false;
            validation.Extensions["message"] = message;
            return new ObjectResult(validation) { StatusCode = statusCode };
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = message,
            Detail = message,
            Type = $"urn:prodoc:{code}"
        };
        problem.Extensions["code"] = code;
        problem.Extensions["success"] = false;
        problem.Extensions["message"] = message;

        return new ObjectResult(problem) { StatusCode = statusCode };
    }
}
