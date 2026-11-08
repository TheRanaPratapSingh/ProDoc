using Microsoft.Extensions.Options;
using ProDoc.Application.Abstractions;
using ProDoc.Infrastructure.Scheduling;

namespace ProDoc.Api.Tests;

public sealed class MapLinkBuilderTests
{
    [Fact]
    public void MapsUrl_uses_place_id_when_present()
    {
        var links = new MapLinkBuilder(Options.Create(new MapOptions()));
        var url = links.MapsUrl(new PlaceLinkInput(
            "City Care Clinic",
            "Shop 12, Main Market, Dhampur",
            null,
            null,
            "ChIJabcdefghijklmnop"));

        Assert.NotNull(url);
        Assert.Contains("maps/search/?api=1", url);
        Assert.Contains("query=", url);
        Assert.Contains("query_place_id=ChIJabcdefghijklmnop", url);
    }

    [Fact]
    public void DirectionsUrl_prefers_coordinates()
    {
        var links = new MapLinkBuilder(Options.Create(new MapOptions()));
        var url = links.DirectionsUrl(new PlaceLinkInput(
            "City Care Clinic",
            "Shop 12, Main Market",
            29.31m,
            78.51m,
            null));

        Assert.Equal("https://www.google.com/maps/dir/?api=1&destination=29.31%2C78.51", url);
    }

    [Fact]
    public void MapsUrl_returns_null_without_destination()
    {
        var links = new MapLinkBuilder(Options.Create(new MapOptions()));
        Assert.Null(links.MapsUrl(new PlaceLinkInput()));
    }
}
