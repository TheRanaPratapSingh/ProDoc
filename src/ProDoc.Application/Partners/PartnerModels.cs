using ProDoc.Domain.Partners;

namespace ProDoc.Application.Partners;

public sealed record PartnerCatalog(
    IReadOnlyList<string> Scopes,
    IReadOnlyList<string> WebhookEvents,
    IReadOnlyList<PartnerErrorCode> ErrorCodes);

public sealed record PartnerErrorCode(string Code, int HttpStatus, string Description);

public sealed record PartnerOrganizationDetails(
    Guid Id,
    string Name,
    string ContactEmail,
    string? Website,
    string Status,
    Guid OwnerUserId,
    string? ReviewNote,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record PartnerAppDetails(
    Guid Id,
    Guid PartnerOrganizationId,
    string Name,
    string ClientId,
    IReadOnlyList<string> Scopes,
    string Environment,
    bool IsActive,
    int RateLimitPerMinute,
    int DailyQuota,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record PartnerCredentialIssued(
    Guid CredentialId,
    string ClientId,
    string ClientSecret,
    string SecretPrefix,
    DateTimeOffset CreatedAtUtc);

public sealed record PartnerCredentialDetails(
    Guid Id,
    string SecretPrefix,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RevokedAtUtc,
    DateTimeOffset? LastUsedAtUtc);

public sealed record PartnerWebhookEndpointDetails(
    Guid Id,
    Guid PartnerAppId,
    string TargetUrl,
    string SecretPrefix,
    IReadOnlyList<string> EventTypes,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);

public sealed record PartnerWebhookEndpointCreated(
    PartnerWebhookEndpointDetails Endpoint,
    string SigningSecret);

public sealed record PartnerWebhookDeliveryDetails(
    Guid Id,
    Guid PartnerWebhookEndpointId,
    Guid PartnerAppId,
    string EventType,
    string IdempotencyKey,
    string Status,
    int AttemptCount,
    DateTimeOffset AvailableAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DeliveredAtUtc,
    int? LastStatusCode,
    string? LastError);

public sealed record PartnerUsageDay(DateOnly Day, int RequestCount, int ErrorCount);

public sealed record PartnerUsageSummary(
    Guid PartnerAppId,
    int TodayRequests,
    int TodayErrors,
    int DailyQuota,
    int RateLimitPerMinute,
    IReadOnlyList<PartnerUsageDay> Last7Days);

public sealed record PartnerTokenResult(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string Scope);

public sealed record PartnerPrincipal(
    Guid PartnerAppId,
    Guid PartnerOrganizationId,
    string ClientId,
    PartnerAppEnvironment Environment,
    IReadOnlyList<string> Scopes,
    int RateLimitPerMinute,
    int DailyQuota);

public sealed record PartnerBookingDetails(
    Guid Id,
    Guid PartnerAppId,
    bool IsSandbox,
    Guid DoctorId,
    Guid? ClinicId,
    Guid? BranchId,
    Guid? ScheduleId,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string ExternalPatientRef,
    string PatientDisplayName,
    string? Reason,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record PartnerDoctorCard(
    Guid DoctorId,
    string FullName,
    string? Specialization,
    string? City,
    decimal? ConsultationFee,
    Guid? ClinicId,
    string? ClinicName);

public sealed record PartnerClinicCard(
    Guid ClinicId,
    string Name,
    string? City,
    IReadOnlyList<PartnerBranchCard> Branches);

public sealed record PartnerBranchCard(
    Guid BranchId,
    string Name,
    string? AddressLine,
    string? City);

public sealed record PartnerOrganizationPublic(
    Guid OrganizationId,
    string Name,
    string? City);

public sealed record PartnerPrescriptionSummary(
    Guid PrescriptionId,
    Guid AppointmentId,
    string Status,
    DateTimeOffset? FinalizedAtUtc,
    bool ContentRedacted);

public sealed record PartnerLabOrderSummary(
    Guid LabOrderId,
    string TestCode,
    string TestName,
    string Status,
    DateTimeOffset CreatedAtUtc);

public sealed record PartnerDocsGuide(
    string Title,
    string Summary,
    string Path);

public sealed record PartnerPlatformStats(
    int PendingPartners,
    int ApprovedPartners,
    int SuspendedPartners,
    int ActiveApps,
    int DeadLetterDeliveries,
    int RequestsToday);
