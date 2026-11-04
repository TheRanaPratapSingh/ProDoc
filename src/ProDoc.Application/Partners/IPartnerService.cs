using ProDoc.Application.Providers;
using ProDoc.Domain.Partners;

namespace ProDoc.Application.Partners;

public interface IPartnerService
{
    Task SeedDefaultsAsync(CancellationToken cancellationToken);

    PartnerCatalog Catalog();

    Task<PartnerOrganizationDetails> RegisterAsync(
        Guid ownerUserId,
        string name,
        string contactEmail,
        string? website,
        RequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<PartnerOrganizationDetails?> MyOrganizationAsync(Guid ownerUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PartnerOrganizationDetails>> AdminListAsync(string? status, CancellationToken cancellationToken);

    Task<PartnerOrganizationDetails> AdminDecideAsync(
        Guid actorUserId,
        Guid partnerOrganizationId,
        string decision,
        string? note,
        RequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<PartnerAppDetails> CreateAppAsync(
        Guid ownerUserId,
        Guid partnerOrganizationId,
        string name,
        IEnumerable<string> scopes,
        string environment,
        RequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PartnerAppDetails>> ListAppsAsync(Guid ownerUserId, Guid partnerOrganizationId, CancellationToken cancellationToken);

    Task<PartnerAppDetails> AdminConfigureAppAsync(
        Guid actorUserId,
        Guid appId,
        IEnumerable<string> scopes,
        int rateLimitPerMinute,
        int dailyQuota,
        bool? isActive,
        RequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<PartnerCredentialIssued> IssueCredentialAsync(
        Guid ownerUserId,
        Guid appId,
        RequestMetadata metadata,
        CancellationToken cancellationToken);

    Task RevokeCredentialAsync(
        Guid ownerUserId,
        Guid appId,
        Guid credentialId,
        RequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PartnerCredentialDetails>> ListCredentialsAsync(Guid ownerUserId, Guid appId, CancellationToken cancellationToken);

    Task<PartnerWebhookEndpointCreated> CreateWebhookAsync(
        Guid ownerUserId,
        Guid appId,
        string targetUrl,
        IEnumerable<string> eventTypes,
        RequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PartnerWebhookEndpointDetails>> ListWebhooksAsync(Guid ownerUserId, Guid appId, CancellationToken cancellationToken);

    Task<string> RotateWebhookSecretAsync(Guid ownerUserId, Guid endpointId, RequestMetadata metadata, CancellationToken cancellationToken);

    Task SetWebhookActiveAsync(Guid ownerUserId, Guid endpointId, bool active, RequestMetadata metadata, CancellationToken cancellationToken);

    Task<IReadOnlyList<PartnerWebhookDeliveryDetails>> ListDeliveriesAsync(Guid ownerUserId, Guid appId, CancellationToken cancellationToken);

    Task<PartnerUsageSummary> UsageAsync(Guid ownerUserId, Guid appId, CancellationToken cancellationToken);

    Task<PartnerTokenResult> ExchangeClientCredentialsAsync(
        string clientId,
        string clientSecret,
        string? scope,
        CancellationToken cancellationToken);

    Task<PartnerPrincipal> RequirePrincipalAsync(Guid partnerAppId, CancellationToken cancellationToken);

    Task EnsureQuotaAsync(PartnerPrincipal principal, CancellationToken cancellationToken);

    Task RecordUsageAsync(Guid partnerAppId, bool isError, CancellationToken cancellationToken);

    Task ProcessWebhookDeliveriesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PartnerDoctorCard>> SearchDoctorsAsync(
        PartnerPrincipal principal,
        string? query,
        string? city,
        CancellationToken cancellationToken);

    Task<PartnerDoctorCard> GetDoctorAsync(PartnerPrincipal principal, Guid doctorId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PartnerClinicCard>> SearchClinicsAsync(
        PartnerPrincipal principal,
        string? query,
        string? city,
        CancellationToken cancellationToken);

    Task<PartnerClinicCard> GetClinicAsync(PartnerPrincipal principal, Guid clinicId, CancellationToken cancellationToken);

    Task<object> GetAvailabilityAsync(
        PartnerPrincipal principal,
        Guid doctorId,
        DateOnly from,
        DateOnly to,
        Guid? branchId,
        CancellationToken cancellationToken);

    Task<PartnerOrganizationPublic> GetOrganizationAsync(
        PartnerPrincipal principal,
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<PartnerBookingDetails> CreateBookingAsync(
        PartnerPrincipal principal,
        Guid doctorId,
        Guid? clinicId,
        Guid? branchId,
        Guid? scheduleId,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        string externalPatientRef,
        string patientDisplayName,
        string? reason,
        string? idempotencyKey,
        CancellationToken cancellationToken);

    Task<PartnerBookingDetails> GetBookingAsync(PartnerPrincipal principal, Guid bookingId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PartnerBookingDetails>> ListBookingsAsync(PartnerPrincipal principal, CancellationToken cancellationToken);

    Task<PartnerBookingDetails> CancelBookingAsync(PartnerPrincipal principal, Guid bookingId, CancellationToken cancellationToken);

    Task<PartnerPrescriptionSummary> GetPrescriptionAsync(
        PartnerPrincipal principal,
        Guid prescriptionId,
        bool patientConsentAttested,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PartnerLabOrderSummary>> ListLabCatalogAsync(PartnerPrincipal principal, CancellationToken cancellationToken);

    Task<PartnerPlatformStats> AdminStatsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PartnerWebhookDeliveryDetails>> AdminDeadLettersAsync(CancellationToken cancellationToken);

    IReadOnlyList<PartnerDocsGuide> DocsGuides();
}
