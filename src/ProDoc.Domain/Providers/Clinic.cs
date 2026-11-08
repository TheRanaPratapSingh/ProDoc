using ProDoc.Domain.Common;
using ProDoc.Domain.Identity;

namespace ProDoc.Domain.Providers;

public sealed class ClinicBranch
{
    private ClinicBranch()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string AddressLine { get; private set; } = string.Empty;

    public string? AddressLine2 { get; private set; }

    public string? Landmark { get; private set; }

    public string? Locality { get; private set; }

    public string City { get; private set; } = string.Empty;

    public string? District { get; private set; }

    public string State { get; private set; } = string.Empty;

    public string PostalCode { get; private set; } = string.Empty;

    public string Country { get; private set; } = PracticeLocation.DefaultCountry;

    public decimal? Latitude { get; private set; }

    public decimal? Longitude { get; private set; }

    public string? GooglePlaceId { get; private set; }

    public DateTimeOffset? LocationUpdatedAtUtc { get; private set; }

    public string? PhoneNumber { get; private set; }

    public bool IsPrimary { get; private set; }

    public bool IsActive { get; private set; }

    public string FormattedAddress =>
        PracticeLocation.FormatFull(
            AddressLine,
            AddressLine2,
            Landmark,
            Locality,
            City,
            District,
            State,
            PostalCode,
            Country);

    public string CompactAddress => PracticeLocation.FormatCompact(Locality, City, PostalCode);

    internal static ClinicBranch Create(
        Guid clinicId,
        string name,
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
        string? phoneNumber,
        bool isPrimary,
        DateTimeOffset? locationUpdatedAtUtc)
    {
        return new ClinicBranch
        {
            Id = Guid.NewGuid(),
            ClinicId = clinicId,
            Name = name,
            AddressLine = addressLine,
            AddressLine2 = addressLine2,
            Landmark = landmark,
            Locality = locality,
            City = city,
            District = district,
            State = state,
            PostalCode = postalCode,
            Country = string.IsNullOrWhiteSpace(country) ? PracticeLocation.DefaultCountry : country.Trim(),
            Latitude = latitude,
            Longitude = longitude,
            GooglePlaceId = googlePlaceId,
            LocationUpdatedAtUtc = locationUpdatedAtUtc,
            PhoneNumber = phoneNumber,
            IsPrimary = isPrimary,
            IsActive = true
        };
    }

    internal void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;

    internal void Deactivate() => IsActive = false;

    internal void Update(
        string name,
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
        string? phoneNumber,
        bool isPrimary,
        DateTimeOffset? locationUpdatedAtUtc)
    {
        Name = name;
        AddressLine = addressLine;
        AddressLine2 = addressLine2;
        Landmark = landmark;
        Locality = locality;
        City = city;
        District = district;
        State = state;
        PostalCode = postalCode;
        Country = string.IsNullOrWhiteSpace(country) ? PracticeLocation.DefaultCountry : country.Trim();
        Latitude = latitude;
        Longitude = longitude;
        GooglePlaceId = googlePlaceId;
        LocationUpdatedAtUtc = locationUpdatedAtUtc;
        PhoneNumber = phoneNumber;
        IsPrimary = isPrimary;
        IsActive = true;
    }
}

public sealed class Clinic
{
    private List<ClinicBranch> _branches = [];
    private List<ProviderDocument> _documents = [];

    private Clinic()
    {
    }

    public Guid Id { get; private set; }

    public Guid OwnerUserId { get; private set; }

    public Guid? OrganizationId { get; private set; }

    public string Reference { get; private set; } = string.Empty;

    public string? Name { get; private set; }

    public ClinicType? Type { get; private set; }

    public string? LegalName { get; private set; }

    public string? Description { get; private set; }

    public string? PhoneNumber { get; private set; }

    public string? Email { get; private set; }

    public string? Website { get; private set; }

    public int? EstablishmentYear { get; private set; }

    public string? RegistrationNumber { get; private set; }

    public string? RegistrationAuthority { get; private set; }

    public string? RegistrationState { get; private set; }

    public DateOnly? RegistrationExpiresOn { get; private set; }

    public VerificationStatus Status { get; private set; }

    public DateTimeOffset? SubmittedAtUtc { get; private set; }

    public DateTimeOffset? ReviewedAtUtc { get; private set; }

    public string? RejectionReason { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = null!;

    public IReadOnlyCollection<ClinicBranch> Branches => _branches;

    public IReadOnlyCollection<ProviderDocument> Documents => _documents;

    public bool IsPubliclyListed(DateOnly today) =>
        Status == VerificationStatus.Approved
        && _branches.Any(branch => branch.IsActive)
        && (RegistrationExpiresOn is null || RegistrationExpiresOn >= today);

    public static Clinic Begin(Guid ownerUserId, DateTimeOffset now)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new ArgumentException("An owner is required.", nameof(ownerUserId));
        }

        return new Clinic
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            Reference = ApplicationReference.Create("CLN", now),
            Status = VerificationStatus.Draft,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void AttachToOrganization(Guid organizationId, DateTimeOffset now)
    {
        if (organizationId == Guid.Empty)
        {
            throw DomainException.Validation("organization_invalid", "Choose an organization.");
        }

        if (OrganizationId is Guid existing && existing != organizationId)
        {
            throw DomainException.Conflict("clinic_org_attached", "This clinic already belongs to another organization.");
        }

        OrganizationId = organizationId;
        Touch(now);
    }

    public void TransferOwnership(Guid newOwnerUserId, DateTimeOffset now)
    {
        if (newOwnerUserId == Guid.Empty)
        {
            throw DomainException.Validation("owner_invalid", "Choose a valid owner.");
        }

        OwnerUserId = newOwnerUserId;
        Touch(now);
    }

    public void DetachFromOrganization(DateTimeOffset now)
    {
        OrganizationId = null;
        Touch(now);
    }

    public void UpdateDetails(
        string? name,
        ClinicType? type,
        string? legalName,
        string? description,
        string? phoneNumber,
        string? email,
        string? website,
        int? establishmentYear,
        string? registrationNumber,
        string? registrationAuthority,
        string? registrationState,
        DateOnly? registrationExpiresOn,
        DateTimeOffset now)
    {
        EnsureEditable();
        var normalizedRegistration = ProviderText.Optional(registrationNumber, 40, "registration_invalid", "Enter a registration number of 40 characters or fewer.");
        var normalizedAuthority = ProviderText.Optional(registrationAuthority, 120, "registration_invalid", "Enter a registration authority of 120 characters or fewer.");
        var normalizedState = ProviderText.Optional(registrationState, 80, "registration_invalid", "Enter a registration state of 80 characters or fewer.");
        var registrationChanged = !string.Equals(RegistrationNumber, normalizedRegistration, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(RegistrationAuthority, normalizedAuthority, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(RegistrationState, normalizedState, StringComparison.OrdinalIgnoreCase)
            || RegistrationExpiresOn != registrationExpiresOn;

        Name = name is null ? null : ProviderText.Required(name, 160, "clinic_name_invalid", "Enter the clinic name.");
        Type = type;
        LegalName = ProviderText.Optional(legalName, 200, "clinic_name_invalid", "Enter a legal name of 200 characters or fewer.");
        Description = ProviderText.Multiline(description, 2000, "clinic_description_invalid", "Enter a description of 2000 characters or fewer.");
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : Identity.PhoneNumber.ParseIndianMobile(phoneNumber).E164;
        Email = ProviderText.Email(email);
        Website = ProviderText.Website(website);
        EstablishmentYear = establishmentYear is null ? null : ProviderText.EstablishmentYear(establishmentYear.Value, now);
        RegistrationNumber = normalizedRegistration;
        RegistrationAuthority = normalizedAuthority;
        RegistrationState = normalizedState;
        RegistrationExpiresOn = registrationExpiresOn;
        if (registrationChanged)
        {
            RequireReverification(now);
        }

        Touch(now);
    }

    public ClinicBranch AddBranch(
        string name,
        string addressLine,
        string? addressLine2,
        string city,
        string state,
        string postalCode,
        decimal? latitude,
        decimal? longitude,
        string? phoneNumber,
        bool isPrimary,
        DateTimeOffset now,
        string? landmark = null,
        string? locality = null,
        string? district = null,
        string? country = null,
        string? googlePlaceId = null)
    {
        EnsureEditable();
        var branch = BuildBranch(
            name,
            addressLine,
            addressLine2,
            landmark,
            locality,
            city,
            district,
            state,
            postalCode,
            country,
            latitude,
            longitude,
            googlePlaceId,
            phoneNumber,
            isPrimary || _branches.All(item => !item.IsActive),
            now);
        if (branch.IsPrimary)
        {
            foreach (var existing in _branches)
            {
                existing.SetPrimary(false);
            }
        }

        _branches.Add(branch);
        Touch(now);
        return branch;
    }

    public void UpdateBranch(
        Guid branchId,
        string name,
        string addressLine,
        string? addressLine2,
        string city,
        string state,
        string postalCode,
        decimal? latitude,
        decimal? longitude,
        string? phoneNumber,
        bool isPrimary,
        DateTimeOffset now,
        string? landmark = null,
        string? locality = null,
        string? district = null,
        string? country = null,
        string? googlePlaceId = null)
    {
        EnsureEditable();
        var branch = _branches.SingleOrDefault(item => item.Id == branchId)
            ?? throw DomainException.NotFound("branch_not_found", "That branch was not found.");
        var updated = BuildBranch(
            name,
            addressLine,
            addressLine2,
            landmark,
            locality,
            city,
            district,
            state,
            postalCode,
            country,
            latitude,
            longitude,
            googlePlaceId,
            phoneNumber,
            isPrimary,
            now);
        if (updated.IsPrimary)
        {
            foreach (var existing in _branches)
            {
                existing.SetPrimary(false);
            }
        }

        branch.Update(
            updated.Name,
            updated.AddressLine,
            updated.AddressLine2,
            updated.Landmark,
            updated.Locality,
            updated.City,
            updated.District,
            updated.State,
            updated.PostalCode,
            updated.Country,
            updated.Latitude,
            updated.Longitude,
            updated.GooglePlaceId,
            updated.PhoneNumber,
            updated.IsPrimary,
            updated.LocationUpdatedAtUtc);
        if (!_branches.Any(item => item.IsActive && item.IsPrimary))
        {
            branch.SetPrimary(true);
        }

        Touch(now);
    }

    public void DeactivateBranch(Guid branchId, DateTimeOffset now)
    {
        EnsureEditable();
        var branch = _branches.SingleOrDefault(item => item.Id == branchId)
            ?? throw DomainException.NotFound("branch_not_found", "That branch was not found.");
        branch.Deactivate();
        branch.SetPrimary(false);
        var replacement = _branches.FirstOrDefault(item => item.IsActive);
        replacement?.SetPrimary(true);
        Touch(now);
    }

    public ProviderDocument AttachDocument(
        DocumentCategory category,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string storageKey,
        string scanEngine,
        string scanDetail,
        DateTimeOffset now)
    {
        EnsureEditable();
        if (category is not (DocumentCategory.ClinicLogo or DocumentCategory.ClinicRegistration or DocumentCategory.Other))
        {
            throw DomainException.Validation("document_invalid", "Choose a clinic document category.");
        }

        var previous = _documents.Where(document => document.IsCurrent && document.Category == category).ToArray();
        foreach (var document in previous)
        {
            document.Supersede();
        }

        var created = ProviderDocument.Create(
            null,
            Id,
            category,
            originalFileName,
            contentType,
            sizeBytes,
            storageKey,
            previous.Length == 0 ? 1 : previous.Max(document => document.Version) + 1,
            previous.OrderByDescending(document => document.Version).FirstOrDefault()?.Id,
            scanEngine,
            scanDetail,
            now);
        _documents.Add(created);
        if (ProviderDocument.IsRegulated(category))
        {
            RequireReverification(now);
        }

        Touch(now);
        return created;
    }

    public void Submit(DateTimeOffset now)
    {
        if (Status is VerificationStatus.Submitted or VerificationStatus.UnderReview or VerificationStatus.Approved or VerificationStatus.Suspended)
        {
            throw DomainException.Conflict("application_already_submitted", "An application is already with the review team.");
        }

        ValidateReady(now);
        Status = VerificationStatus.Submitted;
        SubmittedAtUtc = now;
        ReviewedAtUtc = null;
        RejectionReason = null;
        Touch(now);
    }

    /// <summary>Platform-admin provisioned clinics skip the verification queue when basics are complete.</summary>
    public void ProvisionApproved(DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(Name) || Type is null || PhoneNumber is null)
        {
            throw DomainException.Validation("application_incomplete", "Add the clinic name, type, and contact number.");
        }

        if (!_branches.Any(branch => branch.IsActive && branch.IsPrimary))
        {
            throw DomainException.Validation("application_incomplete", "Add a primary branch before activating the clinic.");
        }

        Status = VerificationStatus.Approved;
        SubmittedAtUtc ??= now;
        ReviewedAtUtc = now;
        RejectionReason = null;
        Touch(now);
    }

    public void Review(ReviewDecision decision, string? reason, DateTimeOffset now)
    {
        switch (decision)
        {
            case ReviewDecision.StartReview:
                RequireStatus(VerificationStatus.Submitted, "Only a submitted application can move into review.");
                Status = VerificationStatus.UnderReview;
                break;
            case ReviewDecision.RequestInformation:
                RequireReviewable("Ask for more information only while the application is in review.");
                RejectionReason = ProviderText.Required(reason, 500, "review_reason_required", "Enter a reason of 500 characters or fewer.");
                Status = VerificationStatus.AdditionalInformationRequired;
                ReviewedAtUtc = now;
                break;
            case ReviewDecision.Approve:
                RequireReviewable("Only a submitted application can be approved.");
                if (!IsRegistrationCurrent(DateOnly.FromDateTime(now.UtcDateTime)))
                {
                    throw DomainException.Validation("registration_expired", "The clinic registration is expired.");
                }

                Status = VerificationStatus.Approved;
                RejectionReason = null;
                ReviewedAtUtc = now;
                break;
            case ReviewDecision.Reject:
                RequireReviewable("Only a submitted application can be rejected.");
                RejectionReason = ProviderText.Required(reason, 500, "review_reason_required", "Enter a reason of 500 characters or fewer.");
                Status = VerificationStatus.Rejected;
                ReviewedAtUtc = now;
                break;
            case ReviewDecision.Suspend:
                RequireStatus(VerificationStatus.Approved, "Only an approved clinic can be suspended.");
                RejectionReason = ProviderText.Required(reason, 500, "review_reason_required", "Enter a reason of 500 characters or fewer.");
                Status = VerificationStatus.Suspended;
                ReviewedAtUtc = now;
                break;
            default:
                throw DomainException.Validation("review_invalid", "Choose a review decision.");
        }

        Touch(now);
    }

    private void ValidateReady(DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(Name) || Type is null || PhoneNumber is null)
        {
            throw DomainException.Validation("application_incomplete", "Add the clinic name, type, and contact number before submitting.");
        }

        if (Type != ClinicType.IndividualPractice && string.IsNullOrWhiteSpace(RegistrationNumber))
        {
            throw DomainException.Validation("application_incomplete", "Add the clinic registration number before submitting.");
        }

        if (Type != ClinicType.IndividualPractice
            && !_documents.Any(document => document.IsCurrent && document.Category == DocumentCategory.ClinicRegistration))
        {
            throw DomainException.Validation("application_incomplete", "Upload the clinic registration document before submitting.");
        }

        if (!_branches.Any(branch => branch.IsActive && branch.IsPrimary))
        {
            throw DomainException.Validation("application_incomplete", "Add a primary branch before submitting.");
        }

        if (!IsRegistrationCurrent(DateOnly.FromDateTime(now.UtcDateTime)))
        {
            throw DomainException.Validation("registration_expired", "The clinic registration is expired.");
        }
    }

    public bool RefreshExpiry(DateOnly today, DateTimeOffset now)
    {
        if (Status != VerificationStatus.Approved || IsRegistrationCurrent(today))
        {
            return false;
        }

        Status = VerificationStatus.Expired;
        Touch(now);
        return true;
    }

    private bool IsRegistrationCurrent(DateOnly today) =>
        RegistrationExpiresOn is null || RegistrationExpiresOn >= today;

    private ClinicBranch BuildBranch(
        string name,
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
        string? phoneNumber,
        bool isPrimary,
        DateTimeOffset now)
    {
        PracticeLocation.EnsureCoordinates(latitude, longitude);

        return ClinicBranch.Create(
            Id,
            ProviderText.Required(name, 120, "branch_invalid", "Enter the branch name."),
            ProviderText.Required(addressLine, 200, "branch_invalid", "Enter the address."),
            ProviderText.Optional(addressLine2, 200, "branch_invalid", "Enter an address of 200 characters or fewer."),
            ProviderText.Optional(landmark, 160, "branch_invalid", "Enter a landmark of 160 characters or fewer."),
            ProviderText.Optional(locality, 120, "branch_invalid", "Enter a locality of 120 characters or fewer."),
            ProviderText.Required(city, 80, "branch_invalid", "Enter the city."),
            ProviderText.Optional(district, 80, "branch_invalid", "Enter a district of 80 characters or fewer."),
            ProviderText.Required(state, 80, "branch_invalid", "Enter the state."),
            ProviderText.PostalCode(postalCode),
            ProviderText.Optional(country, 80, "branch_invalid", "Enter a country of 80 characters or fewer.")
                ?? PracticeLocation.DefaultCountry,
            latitude,
            longitude,
            PracticeLocation.OptionalGooglePlaceId(googlePlaceId),
            string.IsNullOrWhiteSpace(phoneNumber) ? null : Identity.PhoneNumber.ParseIndianMobile(phoneNumber).E164,
            isPrimary,
            latitude is null ? null : now);
    }

    private void EnsureEditable()
    {
        if (Status is VerificationStatus.Submitted or VerificationStatus.UnderReview or VerificationStatus.Suspended)
        {
            throw DomainException.Validation("application_locked", "This application is with the review team and cannot be changed.");
        }
    }

    private void RequireReverification(DateTimeOffset now)
    {
        if (Status != VerificationStatus.Approved)
        {
            return;
        }

        Status = VerificationStatus.Submitted;
        SubmittedAtUtc = now;
        ReviewedAtUtc = null;
        RejectionReason = null;
    }

    private void RequireReviewable(string title)
    {
        if (Status is not (VerificationStatus.Submitted or VerificationStatus.UnderReview))
        {
            throw DomainException.Validation("review_invalid", title);
        }
    }

    private void RequireStatus(VerificationStatus expected, string title)
    {
        if (Status != expected)
        {
            throw DomainException.Validation("review_invalid", title);
        }
    }

    private void Touch(DateTimeOffset now) => UpdatedAtUtc = now;
}
