namespace ProDoc.Domain.Common;

/// <summary>
/// Expected business failure. Prefer returning this via <see cref="AppResult"/> / <see cref="AppResult{T}"/>
/// instead of throwing <see cref="DomainException"/> so Visual Studio first-chance breaks are not raised
/// for ordinary validation, not-found, and conflict outcomes.
/// </summary>
public sealed record AppError(
    string Code,
    string Message,
    int StatusCode,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static AppError Validation(string code, string message, IReadOnlyDictionary<string, string[]>? errors = null) =>
        new(code, message, 400, errors);

    public static AppError Unauthorized(string code, string message) => new(code, message, 401);

    public static AppError Forbidden(string code, string message) => new(code, message, 403);

    public static AppError NotFound(string code, string message) => new(code, message, 404);

    public static AppError Conflict(string code, string message) => new(code, message, 409);

    public static AppError Unprocessable(string code, string message) => new(code, message, 422);

    public static AppError TooManyRequests(string code, string message) => new(code, message, 429);

    public static AppError FromException(DomainException exception) =>
        new(exception.Code, exception.Title, exception.StatusCode);

    /// <summary>
    /// Field-level validation envelope (HTTP 400) matching API <c>validation_failed</c> responses.
    /// </summary>
    public static AppError ValidationFailed(IReadOnlyDictionary<string, string[]> errors, string? message = null) =>
        new(
            "validation_failed",
            message ?? "Please correct the highlighted fields.",
            400,
            errors);
}

/// <summary>
/// Non-generic result for commands that return no value on success.
/// </summary>
public readonly struct AppResult
{
    private AppResult(AppError? error)
    {
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public bool IsFailure => Error is not null;

    public AppError? Error { get; }

    public static AppResult Success() => new(null);

    public static AppResult Failure(AppError error) => new(error);

    public static AppResult Failure(string code, string message, int statusCode = 400) =>
        new(new AppError(code, message, statusCode));

    public static AppResult Validation(string code, string message) => Failure(AppError.Validation(code, message));

    public static AppResult NotFound(string code, string message) => Failure(AppError.NotFound(code, message));

    public static AppResult Conflict(string code, string message) => Failure(AppError.Conflict(code, message));

    public static AppResult Unauthorized(string code, string message) => Failure(AppError.Unauthorized(code, message));

    public static AppResult Forbidden(string code, string message) => Failure(AppError.Forbidden(code, message));

    public static AppResult TooManyRequests(string code, string message) =>
        Failure(AppError.TooManyRequests(code, message));

    public static AppResult FromException(DomainException exception) => Failure(AppError.FromException(exception));
}

/// <summary>
/// Result of an application operation. Success carries <see cref="Value"/>; expected failures carry <see cref="Error"/>.
/// Do not throw <see cref="DomainException"/> for outcomes represented here.
/// </summary>
public readonly struct AppResult<T>
{
    private AppResult(T? value, AppError? error)
    {
        Value = value;
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public bool IsFailure => Error is not null;

    public T? Value { get; }

    public AppError? Error { get; }

    public static AppResult<T> Success(T value) => new(value, null);

    public static AppResult<T> Failure(AppError error) => new(default, error);

    public static AppResult<T> Failure(string code, string message, int statusCode = 400) =>
        Failure(new AppError(code, message, statusCode));

    public static AppResult<T> Validation(string code, string message) => Failure(AppError.Validation(code, message));

    public static AppResult<T> NotFound(string code, string message) => Failure(AppError.NotFound(code, message));

    public static AppResult<T> Conflict(string code, string message) => Failure(AppError.Conflict(code, message));

    public static AppResult<T> Unauthorized(string code, string message) =>
        Failure(AppError.Unauthorized(code, message));

    public static AppResult<T> Forbidden(string code, string message) => Failure(AppError.Forbidden(code, message));

    public static AppResult<T> TooManyRequests(string code, string message) =>
        Failure(AppError.TooManyRequests(code, message));

    public static AppResult<T> FromException(DomainException exception) => Failure(AppError.FromException(exception));

    public static AppResult<T> From(AppResult result) =>
        result.IsSuccess ? throw new InvalidOperationException("Cannot lift a void success into a typed result without a value.") : Failure(result.Error!);

    public AppResult AsUntyped() => IsSuccess ? AppResult.Success() : AppResult.Failure(Error!);
}
