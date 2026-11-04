using Microsoft.AspNetCore.Mvc;

namespace ProDoc.Api.Errors;

public sealed class ApiExceptionHandler(IHostEnvironment environment, ILogger<ApiExceptionHandler> logger)
    : Microsoft.AspNetCore.Diagnostics.IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, code, title) = exception switch
        {
            Domain.Common.DomainException domain => (domain.StatusCode, domain.Code, domain.Title),
            _ => (StatusCodes.Status500InternalServerError, "server_error", "Something went wrong.")
        };

        if (status >= 500)
        {
            logger.LogError(exception, "Unhandled exception {ErrorCode}", code);
        }
        else
        {
            logger.LogInformation("Request rejected with {ErrorCode}", code);
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = $"urn:prodoc:{code}",
            Detail = status >= 500 && environment.IsDevelopment() ? exception.Message : null,
            Extensions = { ["traceId"] = httpContext.TraceIdentifier }
        }, cancellationToken);

        return true;
    }
}
