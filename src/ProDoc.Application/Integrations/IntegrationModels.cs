namespace ProDoc.Application.Integrations;

public sealed record IntegrationProviderDetails(
    Guid Id,
    string Code,
    string DisplayName,
    string Kind,
    string Mode,
    bool IsActive,
    string? BaseUrl,
    string? CredentialReference,
    string ContractVersion,
    DateTimeOffset UpdatedAtUtc);

public sealed record IntegrationConsentDetails(
    Guid Id,
    string ProviderKind,
    string Purpose,
    string ScopeJson,
    string Status,
    DateTimeOffset GrantedAtUtc,
    DateTimeOffset? RevokedAtUtc);

public sealed record IntegrationDeliveryDetails(
    Guid Id,
    string ProviderKind,
    string Operation,
    string Status,
    string ReferenceType,
    Guid? ReferenceId,
    string? ExternalReference,
    string? ErrorCode,
    int AttemptCount,
    DateTimeOffset UpdatedAtUtc);

public sealed record LabCatalogItem(string Code, string Name, string Specimen, string Turnaround);

public sealed record LabOrderDetails(
    Guid Id,
    string ProviderCode,
    string CatalogCode,
    string TestName,
    string Status,
    string? ExternalOrderId,
    bool ResultsArePreliminary,
    Guid? AppointmentId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record LabReportDetails(
    Guid Id,
    Guid LabOrderId,
    string Summary,
    string ResultJson,
    bool IsPreliminary,
    string Disclaimer,
    DateTimeOffset ReportedAtUtc);

public sealed record PharmacyDirectoryItem(string Code, string Name, string City, bool SupportsAvailability);

public sealed record PharmacyShareDetails(
    Guid Id,
    Guid PrescriptionId,
    string ProviderCode,
    string PharmacyName,
    string Status,
    string? ExternalShareId,
    DateTimeOffset SharedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record AbhaLinkDetails(
    string Status,
    string? SandboxHandle,
    string Notes,
    DateTimeOffset UpdatedAtUtc,
    bool ProductionConnected);

public sealed record FhirBundleExport(
    string ResourceType,
    string Type,
    string Timestamp,
    IReadOnlyList<object> Entry,
    string Disclaimer);

public sealed record IntegrationStatusOverview(
    IReadOnlyList<IntegrationProviderDetails> Providers,
    IReadOnlyList<IntegrationConsentDetails> Consents,
    AbhaLinkDetails Abha,
    int OpenLabOrders,
    int ActivePharmacyShares);
