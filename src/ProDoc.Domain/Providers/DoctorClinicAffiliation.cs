using ProDoc.Domain.Common;

namespace ProDoc.Domain.Providers;

public sealed class DoctorClinicAffiliation
{
    private DoctorClinicAffiliation()
    {
    }

    public Guid Id { get; private set; }

    public Guid DoctorId { get; private set; }

    public Guid ClinicId { get; private set; }

    public Guid? BranchId { get; private set; }

    public AssociationKind Kind { get; private set; }

    public bool IsPrimary { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>
    /// When set, this doctor consults at a practice address that differs from the clinic branch address.
    /// Does not overwrite the clinic's official location.
    /// </summary>
    public bool HasPracticeLocationOverride => !string.IsNullOrWhiteSpace(PracticeAddressLine);

    public string? PracticeAddressLine { get; private set; }

    public string? PracticeAddressLine2 { get; private set; }

    public string? PracticeLandmark { get; private set; }

    public string? PracticeLocality { get; private set; }

    public string? PracticeCity { get; private set; }

    public string? PracticeDistrict { get; private set; }

    public string? PracticeState { get; private set; }

    public string? PracticePostalCode { get; private set; }

    public string? PracticeCountry { get; private set; }

    public decimal? PracticeLatitude { get; private set; }

    public decimal? PracticeLongitude { get; private set; }

    public string? PracticeGooglePlaceId { get; private set; }

    public DateTimeOffset? PracticeLocationUpdatedAtUtc { get; private set; }

    public static DoctorClinicAffiliation Create(
        Guid doctorId,
        Guid clinicId,
        Guid? branchId,
        AssociationKind kind,
        bool isPrimary,
        DateTimeOffset now)
    {
        if (doctorId == Guid.Empty || clinicId == Guid.Empty)
        {
            throw DomainException.Validation("affiliation_invalid", "Choose a doctor and a clinic.");
        }

        if (!Enum.IsDefined(kind))
        {
            throw DomainException.Validation("affiliation_invalid", "Choose how this doctor practices at the clinic.");
        }

        return new DoctorClinicAffiliation
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            ClinicId = clinicId,
            BranchId = branchId,
            Kind = kind,
            IsPrimary = isPrimary,
            CreatedAtUtc = now
        };
    }

    public void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;

    public void SetBranch(Guid? branchId) => BranchId = branchId;

    public void SetPracticeLocation(
        string addressLine,
        string? addressLine2,
        string? landmark,
        string? locality,
        string city,
        string? district,
        string state,
        string postalCode,
        string? country,
        decimal? latitude,
        decimal? longitude,
        string? googlePlaceId,
        DateTimeOffset now)
    {
        PracticeLocation.EnsureCoordinates(latitude, longitude);
        PracticeAddressLine = ProviderText.Required(addressLine, 200, "practice_location_invalid", "Enter the practice address.");
        PracticeAddressLine2 = ProviderText.Optional(addressLine2, 200, "practice_location_invalid", "Enter an address of 200 characters or fewer.");
        PracticeLandmark = ProviderText.Optional(landmark, 160, "practice_location_invalid", "Enter a landmark of 160 characters or fewer.");
        PracticeLocality = ProviderText.Optional(locality, 120, "practice_location_invalid", "Enter a locality of 120 characters or fewer.");
        PracticeCity = ProviderText.Required(city, 80, "practice_location_invalid", "Enter the city.");
        PracticeDistrict = ProviderText.Optional(district, 80, "practice_location_invalid", "Enter a district of 80 characters or fewer.");
        PracticeState = ProviderText.Required(state, 80, "practice_location_invalid", "Enter the state.");
        PracticePostalCode = ProviderText.PostalCode(postalCode);
        PracticeCountry = ProviderText.Optional(country, 80, "practice_location_invalid", "Enter a country of 80 characters or fewer.")
            ?? PracticeLocation.DefaultCountry;
        PracticeLatitude = latitude;
        PracticeLongitude = longitude;
        PracticeGooglePlaceId = PracticeLocation.OptionalGooglePlaceId(googlePlaceId);
        PracticeLocationUpdatedAtUtc = now;
    }

    public void ClearPracticeLocation()
    {
        PracticeAddressLine = null;
        PracticeAddressLine2 = null;
        PracticeLandmark = null;
        PracticeLocality = null;
        PracticeCity = null;
        PracticeDistrict = null;
        PracticeState = null;
        PracticePostalCode = null;
        PracticeCountry = null;
        PracticeLatitude = null;
        PracticeLongitude = null;
        PracticeGooglePlaceId = null;
        PracticeLocationUpdatedAtUtc = null;
    }

    public string? FormattedPracticeAddress =>
        HasPracticeLocationOverride
            ? PracticeLocation.FormatFull(
                PracticeAddressLine,
                PracticeAddressLine2,
                PracticeLandmark,
                PracticeLocality,
                PracticeCity,
                PracticeDistrict,
                PracticeState,
                PracticePostalCode,
                PracticeCountry)
            : null;
}
