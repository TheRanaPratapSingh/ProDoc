namespace ProDoc.Domain.Common;

public static class Languages
{
    public const string English = "en";

    public const string Hindi = "hi";

    public static bool IsSupported(string? value) => value is English or Hindi;
}
