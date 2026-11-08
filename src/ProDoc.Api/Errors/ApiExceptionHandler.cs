using System.Net.Sockets;
using FluentValidation;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProDoc.Api.Errors;

public sealed class ApiExceptionHandler(IHostEnvironment environment, ILogger<ApiExceptionHandler> logger)
    : Microsoft.AspNetCore.Diagnostics.IExceptionHandler
{
    public const string CorrelationHeaderName = "X-Correlation-Id";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (IsClientDisconnect(httpContext, exception))
        {
            logger.LogDebug(exception, "Client disconnected during {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            // Connection is already gone — do not attempt a ProblemDetails response.
            return true;
        }

        var mapped = Map(exception, environment.IsDevelopment());
        var correlationId = ResolveCorrelationId(httpContext);

        if (mapped.Status >= 500)
        {
            logger.LogError(
                exception,
                "Unhandled exception {ErrorCode} correlation {CorrelationId}",
                mapped.Code,
                correlationId);
        }
        else if (mapped.Status == StatusCodes.Status429TooManyRequests)
        {
            logger.LogWarning("Request rejected with {ErrorCode} correlation {CorrelationId}", mapped.Code, correlationId);
        }
        else
        {
            logger.LogInformation("Request rejected with {ErrorCode} correlation {CorrelationId}", mapped.Code, correlationId);
        }

        if (httpContext.Response.HasStarted || httpContext.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        httpContext.Response.StatusCode = mapped.Status;
        httpContext.Response.ContentType = "application/problem+json";
        httpContext.Response.Headers[CorrelationHeaderName] = correlationId;

        if (mapped.ValidationErrors is { Count: > 0 })
        {
            await httpContext.Response.WriteAsJsonAsync(new ValidationProblemDetails(mapped.ValidationErrors)
            {
                Status = mapped.Status,
                Title = mapped.Title,
                Type = $"urn:prodoc:{mapped.Code}",
                Detail = mapped.Detail,
                Extensions =
                {
                    ["code"] = mapped.Code,
                    ["traceId"] = correlationId,
                    ["correlationId"] = correlationId
                }
            }, cancellationToken);
            return true;
        }

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = mapped.Status,
            Title = mapped.Title,
            Type = $"urn:prodoc:{mapped.Code}",
            Detail = mapped.Detail,
            Extensions =
            {
                ["code"] = mapped.Code,
                ["traceId"] = correlationId,
                ["correlationId"] = correlationId
            }
        }, cancellationToken);

        return true;
    }

    public static ExceptionMap Map(Exception exception, bool isDevelopment)
    {
        switch (exception)
        {
            case Domain.Common.DomainException domain:
                // Expected business failures: safe title/detail for clients, never a 500.
                return new ExceptionMap(domain.StatusCode, domain.Code, domain.Title, Detail: domain.Title);

            case ValidationException validation:
                var errors = validation.Errors
                    .GroupBy(error => string.IsNullOrWhiteSpace(error.PropertyName) ? string.Empty : error.PropertyName)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(error => error.ErrorMessage).Distinct().ToArray());
                return new ExceptionMap(
                    StatusCodes.Status400BadRequest,
                    "validation_failed",
                    "One or more validation errors occurred.",
                    Detail: null,
                    ValidationErrors: errors);

            case BadHttpRequestException bad:
                return new ExceptionMap(bad.StatusCode, "bad_request", "The request could not be read.");

            case UnauthorizedAccessException:
                return new ExceptionMap(
                    StatusCodes.Status403Forbidden,
                    "forbidden",
                    "You do not have permission to perform this action.");

            case DbUpdateConcurrencyException:
                return new ExceptionMap(
                    StatusCodes.Status409Conflict,
                    "concurrency_conflict",
                    "This record changed. Refresh and try again.");

            case DbUpdateException db when IsUniqueViolation(db):
                return new ExceptionMap(
                    StatusCodes.Status409Conflict,
                    "unique_constraint",
                    "This record already exists.");

            case DbUpdateException:
                return new ExceptionMap(
                    StatusCodes.Status500InternalServerError,
                    "database_error",
                    "Something went wrong while saving. Try again.",
                    // Never expose SQL / provider messages to clients.
                    Detail: null);

            case OperationCanceledException or TaskCanceledException:
                return new ExceptionMap(
                    StatusCodes.Status408RequestTimeout,
                    "request_cancelled",
                    "The request was cancelled or timed out.");

            case TimeoutException:
                return new ExceptionMap(
                    StatusCodes.Status504GatewayTimeout,
                    "timeout",
                    "The operation timed out. Try again shortly.");

            case SocketException:
                return new ExceptionMap(
                    StatusCodes.Status503ServiceUnavailable,
                    "transient_network",
                    "A temporary network issue occurred. Try again.");

            default:
                return new ExceptionMap(
                    StatusCodes.Status500InternalServerError,
                    "server_error",
                    "Something went wrong.",
                    Detail: isDevelopment && !LooksSensitive(exception) ? exception.Message : null);
        }
    }

    internal static bool IsClientDisconnect(HttpContext httpContext, Exception exception)
    {
        if (httpContext.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        for (var current = exception; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case ConnectionResetException:
                    return true;
                case OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested:
                    return true;
                case IOException when current.InnerException is SocketException socket && IsResetOrAbort(socket.SocketErrorCode):
                    return true;
                case SocketException socket when IsResetOrAbort(socket.SocketErrorCode):
                    return true;
                case BadHttpRequestException bad when IsClientDisconnectInner(bad.InnerException):
                    return true;
            }
        }

        return exception.Message.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase)
               || exception.Message.Contains("connection was aborted", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsUniqueViolation(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            // Avoid a hard Microsoft.Data.SqlClient dependency in the API layer.
            var number = current.GetType().GetProperty("Number")?.GetValue(current) as int?;
            if (number is 2601 or 2627)
            {
                return true;
            }

            if (current.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksSensitive(Exception exception)
    {
        var text = exception.ToString();
        return text.Contains("SqlException", StringComparison.OrdinalIgnoreCase)
               || text.Contains("password", StringComparison.OrdinalIgnoreCase)
               || text.Contains("connection string", StringComparison.OrdinalIgnoreCase)
               || text.Contains("Bearer ", StringComparison.OrdinalIgnoreCase)
               || text.Contains("stack trace", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue(CorrelationHeaderName, out var incoming)
            && !string.IsNullOrWhiteSpace(incoming))
        {
            return incoming.ToString();
        }

        return httpContext.TraceIdentifier;
    }

    private static bool IsClientDisconnectInner(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is ConnectionResetException)
            {
                return true;
            }

            if (current is SocketException socket && IsResetOrAbort(socket.SocketErrorCode))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsResetOrAbort(SocketError code) =>
        code is SocketError.ConnectionReset
            or SocketError.ConnectionAborted
            or SocketError.TimedOut
            or SocketError.Shutdown;

    public sealed record ExceptionMap(
        int Status,
        string Code,
        string Title,
        string? Detail = null,
        IDictionary<string, string[]>? ValidationErrors = null);
}
