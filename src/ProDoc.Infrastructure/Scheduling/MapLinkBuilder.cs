using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using ProDoc.Application.Abstractions;
using ProDoc.Domain.Providers;

namespace ProDoc.Infrastructure.Scheduling;

public sealed class MapOptions
{
    public const string SectionName = "Maps";

    public string Provider { get; set; } = "google-maps-url";

    /// <summary>Optional override. Prefer built-in Google Maps URL builders when empty.</summary>
    public string? DirectionsUrlTemplate { get; set; }

    public string? SearchUrlTemplate { get; set; }
}

public sealed class MapLinkBuilder(IOptions<MapOptions> options) : IPlaceLinkBuilder
{
    public string? MapsUrl(PlaceLinkInput place)
    {
        var template = options.Value.SearchUrlTemplate;
        if (!string.IsNullOrWhiteSpace(template))
        {
            return ApplyTemplate(template, place);
        }

        var query = PracticeLocation.MapsQuery(place.Name, place.Address, place.Latitude, place.Longitude);
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var url = new StringBuilder("https://www.google.com/maps/search/?api=1&query=")
            .Append(Uri.EscapeDataString(query));
        if (!string.IsNullOrWhiteSpace(place.GooglePlaceId))
        {
            url.Append("&query_place_id=").Append(Uri.EscapeDataString(place.GooglePlaceId.Trim()));
        }

        return url.ToString();
    }

    public string? DirectionsUrl(PlaceLinkInput place)
    {
        var template = options.Value.DirectionsUrlTemplate;
        if (!string.IsNullOrWhiteSpace(template))
        {
            return ApplyTemplate(template, place);
        }

        string? destination;
        if (place.Latitude is decimal lat && place.Longitude is decimal lng)
        {
            destination = lat.ToString(CultureInfo.InvariantCulture) + "," + lng.ToString(CultureInfo.InvariantCulture);
        }
        else if (!string.IsNullOrWhiteSpace(place.Address) || !string.IsNullOrWhiteSpace(place.Name))
        {
            destination = PracticeLocation.MapsQuery(place.Name, place.Address, null, null);
        }
        else
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(destination))
        {
            return null;
        }

        var url = new StringBuilder("https://www.google.com/maps/dir/?api=1&destination=")
            .Append(Uri.EscapeDataString(destination));
        if (!string.IsNullOrWhiteSpace(place.GooglePlaceId))
        {
            url.Append("&destination_place_id=").Append(Uri.EscapeDataString(place.GooglePlaceId.Trim()));
        }

        return url.ToString();
    }

    private static string? ApplyTemplate(string template, PlaceLinkInput place)
    {
        var query = PracticeLocation.MapsQuery(place.Name, place.Address, place.Latitude, place.Longitude);
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        return template
            .Replace("{query}", Uri.EscapeDataString(query), StringComparison.Ordinal)
            .Replace("{latitude}", place.Latitude?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, StringComparison.Ordinal)
            .Replace("{longitude}", place.Longitude?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, StringComparison.Ordinal)
            .Replace("{placeId}", Uri.EscapeDataString(place.GooglePlaceId?.Trim() ?? string.Empty), StringComparison.Ordinal);
    }
}
