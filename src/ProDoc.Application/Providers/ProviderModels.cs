namespace ProDoc.Application.Providers;

public sealed record SpecializationModel(Guid Id, string Code, string Name, bool IsPrimary);

public sealed record QualificationModel(Guid Id, string Name, string? Institution, int GraduationYear);

public sealed record RegistrationModel(
    Guid Id,
    string Number,
    string Council,
    string State,
    string RegistrationType,
    DateOnly RegisteredOn,
    DateOnly? ExpiresOn,
    int Version,
    bool IsCurrent);

public sealed record DocumentModel(
    Guid Id,
    string Category,
    string FileName,
    string ContentType,
    long SizeBytes,
    int Version,
    bool IsCurrent,
    Guid? SupersedesDocumentId,
    string ScanEngine,
    string ScanDetail,
    string? RejectionReason,
    DateTimeOffset UploadedAtUtc);

public sealed record AffiliationModel(
    Guid Id,
    Guid ClinicId,
    string ClinicName,
    string ClinicStatus,
    Guid? BranchId,
    string? BranchName,
    string Kind,
    bool IsPrimary);

public sealed record DoctorProfile(
    Guid Id,
    string Reference,
    string Status,
    bool IsPubliclyBookable,
    bool IsAcceptingPatients,
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    DateOnly? DateOfBirth,
    string? Gender,
    string? Email,
    string PreferredLanguage,
    string? DisplayName,
    string? Biography,
    int? YearsOfExperience,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> Expertise,
    IReadOnlyList<QualificationModel> Qualifications,
    IReadOnlyList<SpecializationModel> Specializations,
    IReadOnlyList<RegistrationModel> Registrations,
    IReadOnlyList<DocumentModel> Documents,
    IReadOnlyList<AffiliationModel> Affiliations,
    string? RejectionReason,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ReviewedAtUtc);

public sealed record ClinicBranchModel(
    Guid Id,
    string Name,
    string AddressLine,
    string? AddressLine2,
    string City,
    string State,
    string PostalCode,
    decimal? Latitude,
    decimal? Longitude,
    string? PhoneNumber,
    bool IsPrimary,
    bool IsActive);

public sealed record ClinicProfile(
    Guid Id,
    string Reference,
    string Status,
    bool IsPubliclyListed,
    string? Name,
    string? Type,
    string? LegalName,
    string? Description,
    string? PhoneNumber,
    string? Email,
    string? Website,
    int? EstablishmentYear,
    string? RegistrationNumber,
    string? RegistrationAuthority,
    string? RegistrationState,
    DateOnly? RegistrationExpiresOn,
    IReadOnlyList<ClinicBranchModel> Branches,
    IReadOnlyList<DocumentModel> Documents,
    string? RejectionReason,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ReviewedAtUtc);

public sealed record DoctorCard(
    Guid Id,
    string DisplayName,
    int YearsOfExperience,
    string PrimarySpecialization,
    Guid? PhotoDocumentId);

public sealed record PublicDoctor(
    Guid Id,
    string DisplayName,
    string? Biography,
    int YearsOfExperience,
    string PrimarySpecialization,
    IReadOnlyList<string> Specializations,
    IReadOnlyList<string> Qualifications,
    IReadOnlyList<string> Languages,
    Guid? PhotoDocumentId,
    IReadOnlyList<AffiliationModel> Clinics);

public sealed record ClinicCard(Guid Id, string Name, string Type, string City, Guid? LogoDocumentId);

public sealed record PublicClinic(
    Guid Id,
    string Name,
    string Type,
    string? Description,
    string? PhoneNumber,
    string? Website,
    Guid? LogoDocumentId,
    IReadOnlyList<ClinicBranchModel> Branches);

public sealed record ReviewQueueItem(
    string Kind,
    Guid Id,
    string Reference,
    string Name,
    string Status,
    DateTimeOffset? SubmittedAtUtc);

public sealed record IncomingUpload(string FileName, string ContentType, Stream Content);

public sealed record StoredUpload(
    string FileName,
    string ContentType,
    long SizeBytes,
    string StorageKey,
    string ScanEngine,
    string ScanDetail);

public sealed record DocumentDownload(string FileName, string ContentType, Stream Content);
