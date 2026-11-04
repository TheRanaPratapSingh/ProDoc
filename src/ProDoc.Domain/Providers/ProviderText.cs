using System.Text.RegularExpressions;
using ProDoc.Domain.Common;

namespace ProDoc.Domain.Providers;

internal static partial class ProviderText
{
    public static string PersonName(string? value, string code, string title)
    {
        var trimmed = Collapse(value);
        if (trimmed is null || trimmed.Length > 40 || !PersonNamePattern().IsMatch(trimmed))
        {
            throw DomainException.Validation(code, title);
        }

        return trimmed;
    }

    public static string Required(string? value, int max, string code, string title)
    {
        var trimmed = Collapse(value);
        if (trimmed is null || trimmed.Length > max)
        {
            throw DomainException.Validation(code, title);
        }

        return trimmed;
    }

    public static string? Optional(string? value, int max, string code, string title)
    {
        var trimmed = Collapse(value);
        if (trimmed is null)
        {
            return null;
        }

        if (trimmed.Length > max)
        {
            throw DomainException.Validation(code, title);
        }

        return trimmed;
    }

    public static string? Multiline(string? value, int max, string code, string title)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().Replace("\r\n", "\n", StringComparison.Ordinal);
        if (trimmed.Length > max || trimmed.Any(character => char.IsControl(character) && character != '\n'))
        {
            throw DomainException.Validation(code, title);
        }

        return trimmed;
    }

    public static string? Email(string? value)
    {
        var trimmed = Optional(value, 160, "email_invalid", "Enter an email address of 160 characters or fewer.");
        if (trimmed is null)
        {
            return null;
        }

        if (!EmailPattern().IsMatch(trimmed))
        {
            throw DomainException.Validation("email_invalid", "Enter a valid email address.");
        }

        return trimmed;
    }

    public static string Gender(string? value)
    {
        var normalized = Collapse(value)?.ToLowerInvariant();
        if (normalized is not ("female" or "male" or "other" or "undisclosed"))
        {
            throw DomainException.Validation("gender_invalid", "Choose female, male, other, or undisclosed.");
        }

        return normalized;
    }

    public static string Language(string? value)
    {
        if (!Languages.IsSupported(value))
        {
            throw DomainException.Validation("language_invalid", "Choose English or Hindi.");
        }

        return value!;
    }

    public static IReadOnlyList<string> SpokenLanguages(IEnumerable<string>? codes)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "en", "hi", "bn", "te", "mr", "ta", "ur", "gu", "kn", "ml", "pa", "or", "as"
        };
        var selected = new List<string>();
        foreach (var code in codes ?? [])
        {
            var normalized = code.Trim().ToLowerInvariant();
            if (!allowed.Contains(normalized))
            {
                throw DomainException.Validation("language_invalid", "Choose a supported spoken language.");
            }

            if (!selected.Contains(normalized, StringComparer.Ordinal))
            {
                selected.Add(normalized);
            }
        }

        return selected;
    }

    public static IReadOnlyList<string> Expertise(IEnumerable<string>? labels)
    {
        var selected = new List<string>();
        foreach (var label in labels ?? [])
        {
            var trimmed = Collapse(label);
            if (trimmed is null)
            {
                continue;
            }

            if (trimmed.Length > 80 || selected.Count == 8)
            {
                throw DomainException.Validation("expertise_invalid", "Add up to 8 areas of expertise, 80 characters each.");
            }

            if (!selected.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                selected.Add(trimmed);
            }
        }

        return selected;
    }

    public static int ExperienceYears(int years)
    {
        if (years is < 0 or > 70)
        {
            throw DomainException.Validation("experience_invalid", "Years of experience must be between 0 and 70.");
        }

        return years;
    }

    public static int GraduationYear(int year, DateTimeOffset now)
    {
        if (year < 1940 || year > now.Year)
        {
            throw DomainException.Validation("qualification_invalid", "Enter a graduation year between 1940 and this year.");
        }

        return year;
    }

    public static DateOnly DateOfBirth(DateOnly dateOfBirth, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var age = today.Year - dateOfBirth.Year - (today.DayOfYear < dateOfBirth.DayOfYear ? 1 : 0);
        if (age is < 18 or > 100)
        {
            throw DomainException.Validation("date_of_birth_invalid", "Enter a date of birth for an adult aged 18 to 100.");
        }

        return dateOfBirth;
    }

    public static string RegistrationType(string? value)
    {
        var normalized = Collapse(value)?.ToLowerInvariant();
        if (normalized is not ("permanent" or "provisional"))
        {
            throw DomainException.Validation("registration_invalid", "Choose a permanent or provisional registration.");
        }

        return normalized;
    }

    public static string PostalCode(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length != 6)
        {
            throw DomainException.Validation("postal_code_invalid", "Enter a 6-digit postal code.");
        }

        return digits;
    }

    public static int EstablishmentYear(int year, DateTimeOffset now)
    {
        if (year < 1850 || year > now.Year)
        {
            throw DomainException.Validation("establishment_year_invalid", "Enter an establishment year between 1850 and this year.");
        }

        return year;
    }

    public static string? Website(string? value)
    {
        var trimmed = Optional(value, 200, "website_invalid", "Enter a website of 200 characters or fewer.");
        if (trimmed is null)
        {
            return null;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
        {
            throw DomainException.Validation("website_invalid", "Enter a website starting with http:// or https://.");
        }

        return trimmed;
    }

    private static string? Collapse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    [GeneratedRegex(@"^[\p{L}\p{M}][\p{L}\p{M} .'-]{0,39}$", RegexOptions.CultureInvariant)]
    private static partial Regex PersonNamePattern();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();
}
