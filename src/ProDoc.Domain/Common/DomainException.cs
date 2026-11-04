namespace ProDoc.Domain.Common;

public sealed class DomainException : Exception
{
    private DomainException(string code, string title, int statusCode)
        : base(title)
    {
        Code = code;
        Title = title;
        StatusCode = statusCode;
    }

    public string Code { get; }

    public string Title { get; }

    public int StatusCode { get; }

    public static DomainException Validation(string code, string title) => new(code, title, 400);

    public static DomainException Unauthorized(string code, string title) => new(code, title, 401);

    public static DomainException Forbidden(string code, string title) => new(code, title, 403);

    public static DomainException NotFound(string code, string title) => new(code, title, 404);

    public static DomainException Conflict(string code, string title) => new(code, title, 409);

    public static DomainException TooManyRequests(string code, string title) => new(code, title, 429);
}
