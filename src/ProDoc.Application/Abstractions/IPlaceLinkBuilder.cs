namespace ProDoc.Application.Abstractions;

public sealed record PlaceLinkInput(
    string? Name = null,
    string? Address = null,
    decimal? Latitude = null,
    decimal? Longitude = null,
    string? GooglePlaceId = null);

public interface IPlaceLinkBuilder
{
    /// <summary>Open the place on Google Maps (search / pin). Null when no address or coordinates exist.</summary>
    string? MapsUrl(PlaceLinkInput place);

    /// <summary>Turn-by-turn directions destination. Null when no destination can be formed.</summary>
    string? DirectionsUrl(PlaceLinkInput place);

    /// <summary>Backward-compatible helper used by existing discovery mapping.</summary>
    string? DirectionsUrl(decimal? latitude, decimal? longitude, string? address) =>
        DirectionsUrl(new PlaceLinkInput(Address: address, Latitude: latitude, Longitude: longitude));
}
