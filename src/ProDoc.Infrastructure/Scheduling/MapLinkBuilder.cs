using Microsoft.Extensions.Options;
using ProDoc.Application.Abstractions;

namespace ProDoc.Infrastructure.Scheduling;

public sealed class MapOptions
{
    public const string SectionName = "Maps";

    public string Provider { get; set; } = "configured-url";

    public string DirectionsUrlTemplate { get; set; } = "https://www.google.com/maps/search/?api=1&query={query}";
}

public sealed class MapLinkBuilder(IOptions<MapOptions> options) : IPlaceLinkBuilder
{
    public string? DirectionsUrl(decimal? latitude, decimal? longitude, string? address)
    {
        var template = options.Value.DirectionsUrlTemplate;
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        var query = latitude is decimal lat && longitude is decimal lng
            ? $"{lat},{lng}"
            : address?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        return template
            .Replace("{query}", Uri.EscapeDataString(query), StringComparison.Ordinal)
            .Replace("{latitude}", latitude?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty, StringComparison.Ordinal)
            .Replace("{longitude}", longitude?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty, StringComparison.Ordinal);
    }
}
