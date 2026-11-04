namespace ProDoc.Application.Abstractions;

public interface IPlaceLinkBuilder
{
    string? DirectionsUrl(decimal? latitude, decimal? longitude, string? address);
}
