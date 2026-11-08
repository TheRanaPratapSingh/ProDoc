using ProDoc.Domain.Common;

namespace ProDoc.Domain.Providers;

public sealed class Doctor
{
    private List<DoctorQualification> _qualifications = [];
    private List<DoctorSpecialization> _specializations = [];
    private List<DoctorSpokenLanguage> _languages = [];
    private List<DoctorExpertise> _expertise = [];
    private List<DoctorRegistration> _registrations = [];
    private List<ProviderDocument> _documents = [];

    private Doctor()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Reference { get; private set; } = string.Empty;

    public string? FirstName { get; private set; }

    public string? LastName { get; private set; }

    public DateOnly? DateOfBirth { get; private set; }

    public string? Gender { get; private set; }

    public string? Email { get; private set; }

    public string PreferredLanguage { get; private set; } = ProDoc.Domain.Common.Languages.English;

    public string? DisplayName { get; private set; }

    public string? Biography { get; private set; }

    public int YearsOfExperience { get; private set; }

    public VerificationStatus Status { get; private set; }

    public bool IsAcceptingPatients { get; private set; } = true;

    public DateTimeOffset? SubmittedAtUtc { get; private set; }

    public DateTimeOffset? ReviewedAtUtc { get; private set; }

    public string? RejectionReason { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = null!;

    public IReadOnlyCollection<DoctorQualification> Qualifications => _qualifications;

    public IReadOnlyCollection<DoctorSpecialization> Specializations => _specializations;

    public IReadOnlyCollection<DoctorSpokenLanguage> Languages => _languages;

    public IReadOnlyCollection<DoctorExpertise> Expertise => _expertise;

    public IReadOnlyCollection<DoctorRegistration> Registrations => _registrations;

    public IReadOnlyCollection<ProviderDocument> Documents => _documents;

    public bool IsPubliclyBookable(DateOnly today) =>
        Status == VerificationStatus.Approved && IsAcceptingPatients && HasValidRegistration(today);

    public static Doctor Begin(Guid userId, string preferredLanguage, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A user is required.", nameof(userId));
        }

        return new Doctor
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Reference = ApplicationReference.Create("DOC", now),
            PreferredLanguage = ProviderText.Language(preferredLanguage),
            Status = VerificationStatus.Draft,
            IsAcceptingPatients = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void UpdateBasics(
        string? firstName,
        string? lastName,
        DateOnly? dateOfBirth,
        string? gender,
        string? email,
        string preferredLanguage,
        DateTimeOffset now)
    {
        EnsureEditable();
        FirstName = string.IsNullOrWhiteSpace(firstName) ? null : ProviderText.PersonName(firstName, "name_invalid", "Enter a first name using letters.");
        LastName = string.IsNullOrWhiteSpace(lastName) ? null : ProviderText.PersonName(lastName, "name_invalid", "Enter a last name using letters.");
        DateOfBirth = dateOfBirth is null ? null : ProviderText.DateOfBirth(dateOfBirth.Value, now);
        Gender = string.IsNullOrWhiteSpace(gender) ? null : ProviderText.Gender(gender);
        Email = ProviderText.Email(email);
        PreferredLanguage = ProviderText.Language(preferredLanguage);
        Touch(now);
    }

    public void UpdateProfessional(
        string? displayName,
        string? biography,
        int yearsOfExperience,
        IEnumerable<string>? spokenLanguages,
        IEnumerable<string>? expertise,
        DateTimeOffset now)
    {
        EnsureEditable();
        DisplayName = ProviderText.Optional(displayName, 120, "display_name_invalid", "Enter a display name of 120 characters or fewer.");
        Biography = ProviderText.Multiline(biography, 2000, "biography_invalid", "Enter a biography of 2000 characters or fewer.");
        YearsOfExperience = ProviderText.ExperienceYears(yearsOfExperience);
        ReplaceLanguages(spokenLanguages is null ? [] : ProviderText.SpokenLanguages(spokenLanguages));
        ReplaceExpertise(ProviderText.Expertise(expertise));
        Touch(now);
    }

    public DoctorQualification AddQualification(string name, string? institution, int graduationYear, DateTimeOffset now)
    {
        EnsureEditable();
        var qualification = DoctorQualification.Create(
            Id,
            ProviderText.Required(name, 120, "qualification_invalid", "Enter the qualification."),
            ProviderText.Optional(institution, 160, "qualification_invalid", "Enter an institution of 160 characters or fewer."),
            ProviderText.GraduationYear(graduationYear, now));
        _qualifications.Add(qualification);
        Touch(now);
        return qualification;
    }

    public void RemoveQualification(Guid qualificationId, DateTimeOffset now)
    {
        EnsureEditable();
        var qualification = _qualifications.SingleOrDefault(item => item.Id == qualificationId)
            ?? throw DomainException.NotFound("qualification_not_found", "That qualification was not found.");
        _qualifications.Remove(qualification);
        Touch(now);
    }

    public void SetSpecializations(IReadOnlyCollection<Guid> specializationIds, Guid primaryId, DateTimeOffset now)
    {
        EnsureEditable();
        var distinct = specializationIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (distinct.Length == 0 || !distinct.Contains(primaryId))
        {
            throw DomainException.Validation("specialization_invalid", "Choose a primary specialization from the catalog.");
        }

        _specializations.RemoveAll(link => !distinct.Contains(link.SpecializationId));
        foreach (var specializationId in distinct)
        {
            var existing = _specializations.SingleOrDefault(link => link.SpecializationId == specializationId);
            if (existing is null)
            {
                _specializations.Add(DoctorSpecialization.Create(Id, specializationId, specializationId == primaryId));
            }
            else
            {
                existing.SetPrimary(specializationId == primaryId);
            }
        }

        Touch(now);
    }

    public void UpdateRegistration(
        string number,
        string council,
        string state,
        string registrationType,
        DateOnly registeredOn,
        DateOnly? expiresOn,
        DateTimeOffset now)
    {
        EnsureEditable();
        var normalizedType = ProviderText.RegistrationType(registrationType);
        var normalizedNumber = ProviderText.Required(number, 40, "registration_invalid", "Enter the registration number.");
        var normalizedCouncil = ProviderText.Required(council, 120, "registration_invalid", "Enter the registration council.");
        var normalizedState = ProviderText.Required(state, 80, "registration_invalid", "Enter the registration state.");
        if (registeredOn > DateOnly.FromDateTime(now.UtcDateTime) || (expiresOn is DateOnly expiry && expiry < registeredOn))
        {
            throw DomainException.Validation("registration_invalid", "Check the registration and expiry dates.");
        }

        if (normalizedType == "provisional" && expiresOn is null)
        {
            throw DomainException.Validation("registration_invalid", "A provisional registration needs an expiry date.");
        }

        var current = _registrations.SingleOrDefault(item => item.IsCurrent);
        if (current is not null && current.Matches(normalizedNumber, normalizedCouncil, normalizedState, normalizedType, registeredOn, expiresOn))
        {
            return;
        }

        current?.Supersede();
        _registrations.Add(DoctorRegistration.Create(
            Id,
            normalizedNumber,
            normalizedCouncil,
            normalizedState,
            normalizedType,
            registeredOn,
            expiresOn,
            (current?.Version ?? 0) + 1));
        RequireReverification(now);
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
        if (category is DocumentCategory.ClinicLogo or DocumentCategory.ClinicRegistration)
        {
            throw DomainException.Validation("document_invalid", "Choose a doctor document category.");
        }

        var previous = _documents.Where(document => document.IsCurrent && document.Category == category).ToArray();
        foreach (var document in previous)
        {
            document.Supersede();
        }

        var created = ProviderDocument.Create(
            Id,
            null,
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

    public void SetAcceptingPatients(bool accepting, DateTimeOffset now)
    {
        if (Status != VerificationStatus.Approved)
        {
            throw DomainException.Validation("profile_not_approved", "Only an approved profile can be activated.");
        }

        IsAcceptingPatients = accepting;
        Touch(now);
    }

    /// <summary>
    /// Suspends an approved doctor when the linked account is deactivated by Super Admin.
    /// </summary>
    public void SuspendForAccountDeactivation(string reason, DateTimeOffset now)
    {
        if (Status != VerificationStatus.Approved)
        {
            IsAcceptingPatients = false;
            Touch(now);
            return;
        }

        RejectionReason = RequiredReason(reason);
        Status = VerificationStatus.Suspended;
        ReviewedAtUtc = now;
        IsAcceptingPatients = false;
        Touch(now);
    }

    /// <summary>
    /// Restores a doctor who was suspended solely because the account was deactivated.
    /// </summary>
    public void ReinstateFromAccountDeactivation(string expectedReason, DateTimeOffset now)
    {
        if (Status != VerificationStatus.Suspended)
        {
            return;
        }

        if (!string.Equals(RejectionReason, expectedReason, StringComparison.Ordinal))
        {
            return;
        }

        Status = VerificationStatus.Approved;
        RejectionReason = null;
        ReviewedAtUtc = now;
        IsAcceptingPatients = true;
        Touch(now);
    }

    /// <summary>
    /// Scrubs provider PII and removes public discovery while keeping the doctor row for clinical history.
    /// </summary>
    public void AnonymizeForPermanentDeletion(DateTimeOffset now)
    {
        FirstName = "Deleted";
        LastName = "Doctor";
        DisplayName = "Deleted Doctor";
        Email = null;
        Biography = null;
        DateOfBirth = null;
        Gender = null;
        IsAcceptingPatients = false;
        if (Status is VerificationStatus.Approved or VerificationStatus.Submitted or VerificationStatus.UnderReview
            or VerificationStatus.AdditionalInformationRequired)
        {
            Status = VerificationStatus.Suspended;
            RejectionReason = "Account permanently deleted";
            ReviewedAtUtc = now;
        }

        Touch(now);
    }

    public void Submit(DateTimeOffset now)
    {
        if (Status is VerificationStatus.Submitted or VerificationStatus.UnderReview or VerificationStatus.Approved or VerificationStatus.Suspended)
        {
            throw DomainException.Conflict(
                "application_already_submitted",
                "An application is already with the review team.");
        }

        ValidateReady(now);
        Status = VerificationStatus.Submitted;
        SubmittedAtUtc = now;
        ReviewedAtUtc = null;
        RejectionReason = null;
        Touch(now);
    }

    /// <summary>
    /// Platform-admin provisioned doctors skip the verification queue when invite essentials are complete.
    /// Does not require documents (those remain for self-serve onboarding).
    /// </summary>
    public void ProvisionApproved(DateTimeOffset now)
    {
        if (Status is VerificationStatus.Suspended or VerificationStatus.Rejected)
        {
            throw DomainException.Validation(
                "doctor_not_provisionable",
                "Suspended or rejected doctor profiles cannot be activated from an invitation.");
        }

        if (string.IsNullOrWhiteSpace(DisplayName)
            && string.IsNullOrWhiteSpace(FirstName)
            && string.IsNullOrWhiteSpace(LastName))
        {
            throw DomainException.Validation(
                "application_incomplete",
                "Add the doctor's name before activating the profile.");
        }

        if (!_specializations.Any(link => link.IsPrimary))
        {
            throw DomainException.Validation(
                "application_incomplete",
                "Assign a primary specialization before activating the doctor.");
        }

        if (!HasValidRegistration(DateOnly.FromDateTime(now.UtcDateTime)))
        {
            throw DomainException.Validation(
                "application_incomplete",
                "Add a current medical registration before activating the doctor.");
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            DisplayName = $"{FirstName} {LastName}".Trim();
        }

        Status = VerificationStatus.Approved;
        SubmittedAtUtc ??= now;
        ReviewedAtUtc = now;
        RejectionReason = null;
        IsAcceptingPatients = true;
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
                RejectionReason = RequiredReason(reason);
                Status = VerificationStatus.AdditionalInformationRequired;
                ReviewedAtUtc = now;
                break;
            case ReviewDecision.Approve:
                RequireReviewable("Only a submitted application can be approved.");
                if (!HasValidRegistration(DateOnly.FromDateTime(now.UtcDateTime)))
                {
                    throw DomainException.Validation("registration_expired", "The medical registration is expired.");
                }

                Status = VerificationStatus.Approved;
                RejectionReason = null;
                ReviewedAtUtc = now;
                IsAcceptingPatients = true;
                break;
            case ReviewDecision.Reject:
                RequireReviewable("Only a submitted application can be rejected.");
                RejectionReason = RequiredReason(reason);
                Status = VerificationStatus.Rejected;
                ReviewedAtUtc = now;
                break;
            case ReviewDecision.Suspend:
                RequireStatus(VerificationStatus.Approved, "Only an approved doctor can be suspended.");
                RejectionReason = RequiredReason(reason);
                Status = VerificationStatus.Suspended;
                ReviewedAtUtc = now;
                break;
            default:
                throw DomainException.Validation("review_invalid", "Choose a review decision.");
        }

        Touch(now);
    }

    public bool HasValidRegistration(DateOnly today) =>
        _registrations.Any(registration => registration.IsCurrent && registration.IsValidOn(today));

    public bool RefreshExpiry(DateOnly today, DateTimeOffset now)
    {
        if (Status != VerificationStatus.Approved || HasValidRegistration(today))
        {
            return false;
        }

        Status = VerificationStatus.Expired;
        Touch(now);
        return true;
    }

    private void ValidateReady(DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName) || DateOfBirth is null || Gender is null)
        {
            throw DomainException.Validation("application_incomplete", "Add your name, date of birth, and gender before submitting.");
        }

        if (string.IsNullOrWhiteSpace(DisplayName) || _languages.Count == 0)
        {
            throw DomainException.Validation("application_incomplete", "Add your display name and spoken languages before submitting.");
        }

        if (_qualifications.Count == 0 || !_specializations.Any(link => link.IsPrimary))
        {
            throw DomainException.Validation("application_incomplete", "Add a qualification and a primary specialization before submitting.");
        }

        if (!HasValidRegistration(DateOnly.FromDateTime(now.UtcDateTime)))
        {
            throw DomainException.Validation("application_incomplete", "Add a current medical registration before submitting.");
        }

        if (!_documents.Any(document => document.IsCurrent && document.Category == DocumentCategory.RegistrationCertificate))
        {
            throw DomainException.Validation("application_incomplete", "Upload the medical registration certificate before submitting.");
        }

        if (!_documents.Any(document => document.IsCurrent && document.Category == DocumentCategory.ProfilePhoto))
        {
            throw DomainException.Validation("application_incomplete", "Upload a profile photo before submitting.");
        }
    }

    private void EnsureEditable()
    {
        if (Status is VerificationStatus.Submitted or VerificationStatus.UnderReview or VerificationStatus.Suspended)
        {
            throw DomainException.Validation(
                "application_locked",
                "This application is with the review team and cannot be changed.");
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

    private static string RequiredReason(string? reason)
    {
        var trimmed = ProviderText.Required(reason, 500, "review_reason_required", "Enter a reason of 500 characters or fewer.");
        return trimmed;
    }

    private void ReplaceLanguages(IReadOnlyList<string> codes)
    {
        _languages.Clear();
        foreach (var code in codes)
        {
            _languages.Add(DoctorSpokenLanguage.Create(Id, code));
        }
    }

    private void ReplaceExpertise(IReadOnlyList<string> labels)
    {
        _expertise.Clear();
        foreach (var label in labels)
        {
            _expertise.Add(DoctorExpertise.Create(Id, label));
        }
    }

    private void Touch(DateTimeOffset now) => UpdatedAtUtc = now;
}
