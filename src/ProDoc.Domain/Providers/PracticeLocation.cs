using System.Globalization;
using ProDoc.Domain.Common;

namespace ProDoc.Domain.Providers;

/// <summary>
/// Shared formatting and validation for clinic / practice physical locations.
/// Coordinates are never invented — both latitude and longitude must be supplied together.
/// </summary>
public static class PracticeLocation
{
    public const string DefaultCountry = "India";

    public static void EnsureCoordinates(decimal? latitude, decimal? longitude)
    {
        if ((latitude is null) != (longitude is null)
            || latitude is < -90 or > 90
            || longitude is < -180 or > 180)
        {
            throw DomainException.Validation("location_invalid", "Enter both latitude and longitude, or leave both empty.");
        }
    }

    public static string? OptionalGooglePlaceId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length is < 10 or > 256)
        {
            throw DomainException.Validation("place_id_invalid", "Enter a valid Google Place ID.");
        }

        return trimmed;
    }

    public static string FormatFull(
        string? addressLine,
        string? addressLine2,
        string? landmark,
        string? locality,
        string? city,
        string? district,
        string? state,
        string? postalCode,
        string? country = null)
    {
        var parts = new List<string>();
        Add(parts, addressLine);
        Add(parts, addressLine2);
        Add(parts, landmark);
        Add(parts, locality);
        Add(parts, city);
        Add(parts, district);
        Add(parts, state);
        if (!string.IsNullOrWhiteSpace(postalCode))
        {
            var pin = postalCode.Trim();
            if (parts.Count > 0 && !string.IsNullOrWhiteSpace(state))
            {
                parts[^1] = $"{parts[^1]} – {pin}";
            }
            else
            {
                parts.Add(pin);
            }
        }

        var countryValue = string.IsNullOrWhiteSpace(country) ? null : country.Trim();
        if (!string.IsNullOrWhiteSpace(countryValue)
            && !string.Equals(countryValue, DefaultCountry, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(countryValue);
        }

        return string.Join(", ", parts);
    }

    public static string FormatCompact(string? locality, string? city, string? postalCode)
    {
        return string.Join(
            ", ",
            new[] { locality, city, postalCode }.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()));
    }

    public static string MapsQuery(
        string? clinicName,
        string? address,
        decimal? latitude,
        decimal? longitude)
    {
        if (latitude is decimal lat && longitude is decimal lng)
        {
            return lat.ToString(CultureInfo.InvariantCulture) + "," + lng.ToString(CultureInfo.InvariantCulture);
        }

        var named = string.Join(
            ", ",
            new[] { clinicName, address }.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()));
        return named;
    }

    private static void Add(List<string> parts, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add(value.Trim());
        }
    }
}
