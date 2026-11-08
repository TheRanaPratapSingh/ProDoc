using ProDoc.Domain.Common;
using ProDoc.Domain.Providers;

namespace ProDoc.Domain.Tests;

public sealed class PracticeLocationTests
{
    [Fact]
    public void FormatFull_includes_landmark_locality_and_pin()
    {
        var address = PracticeLocation.FormatFull(
            "Shop 12, Main Market",
            null,
            "Near Bus Stand",
            "Dhampur",
            "Bijnor",
            "Bijnor",
            "Uttar Pradesh",
            "246761");

        Assert.Contains("Shop 12, Main Market", address);
        Assert.Contains("Near Bus Stand", address);
        Assert.Contains("Dhampur", address);
        Assert.Contains("246761", address);
    }

    [Fact]
    public void MapsQuery_prefers_coordinates_over_address()
    {
        var query = PracticeLocation.MapsQuery("City Care", "Main Market, Dhampur", 29.31m, 78.51m);
        Assert.Equal("29.31,78.51", query);
    }

    [Fact]
    public void EnsureCoordinates_rejects_partial_pair()
    {
        var error = Assert.Throws<DomainException>(() => PracticeLocation.EnsureCoordinates(29.31m, null));
        Assert.Equal("location_invalid", error.Code);
    }

    [Fact]
    public void Clinic_branch_stores_extended_location_fields()
    {
        var clinic = Clinic.Begin(Guid.NewGuid(), DateTimeOffset.UtcNow);
        clinic.UpdateDetails(
            "City Care Clinic",
            ClinicType.Clinic,
            null,
            null,
            "9876543210",
            null,
            null,
            null,
            "REG-1",
            null,
            null,
            null,
            DateTimeOffset.UtcNow);

        var branch = clinic.AddBranch(
            "Dhampur",
            "Shop 12, Main Market",
            null,
            "Bijnor",
            "Uttar Pradesh",
            "246761",
            29.31m,
            78.51m,
            null,
            true,
            DateTimeOffset.UtcNow,
            landmark: "Near Bus Stand",
            locality: "Dhampur",
            district: "Bijnor",
            country: "India",
            googlePlaceId: "ChIJtestplaceid01");

        Assert.Equal("Near Bus Stand", branch.Landmark);
        Assert.Equal("Dhampur", branch.Locality);
        Assert.Equal("ChIJtestplaceid01", branch.GooglePlaceId);
        Assert.Contains("246761", branch.FormattedAddress);
        Assert.True(branch.Latitude is not null);
    }

    [Fact]
    public void Affiliation_practice_override_does_not_require_branch_mutation()
    {
        var link = DoctorClinicAffiliation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            AssociationKind.Visiting,
            true,
            DateTimeOffset.UtcNow);

        link.SetPracticeLocation(
            "Consultation Room 3",
            null,
            "Tower B",
            "Civil Lines",
            "Moradabad",
            "Moradabad",
            "Uttar Pradesh",
            "244001",
            "India",
            28.84m,
            78.77m,
            null,
            DateTimeOffset.UtcNow);

        Assert.True(link.HasPracticeLocationOverride);
        Assert.Contains("Moradabad", link.FormattedPracticeAddress);
        link.ClearPracticeLocation();
        Assert.False(link.HasPracticeLocationOverride);
    }
}
