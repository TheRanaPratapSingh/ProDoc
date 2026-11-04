using ProDoc.Domain.Governance;
using ProDoc.Domain.Partners;

namespace ProDoc.Application.Partners;

public interface IPartnerStore
{
    Task<PartnerOrganization?> FindOrganizationAsync(Guid id, bool tracking, CancellationToken cancellationToken);

    Task<PartnerOrganization?> FindOrganizationByOwnerAsync(Guid ownerUserId, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<PartnerOrganization>> ListOrganizationsAsync(PartnerStatus? status, CancellationToken cancellationToken);

    void AddOrganization(PartnerOrganization organization);

    Task<PartnerApp?> FindAppAsync(Guid id, bool tracking, CancellationToken cancellationToken);

    Task<PartnerApp?> FindAppByClientIdAsync(string clientId, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<PartnerApp>> ListAppsAsync(Guid partnerOrganizationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PartnerApp>> ListActiveAppsAsync(CancellationToken cancellationToken);

    void AddApp(PartnerApp app);

    Task<IReadOnlyList<PartnerCredential>> ListCredentialsAsync(Guid partnerAppId, CancellationToken cancellationToken);

    Task<PartnerCredential?> FindActiveCredentialAsync(Guid partnerAppId, string plainSecret, CancellationToken cancellationToken);

    Task<PartnerCredential?> FindCredentialAsync(Guid partnerAppId, Guid credentialId, bool tracking, CancellationToken cancellationToken);

    void AddCredential(PartnerCredential credential);

    Task<IReadOnlyList<PartnerWebhookEndpoint>> ListWebhookEndpointsAsync(Guid partnerAppId, CancellationToken cancellationToken);

    Task<PartnerWebhookEndpoint?> FindWebhookEndpointAsync(Guid id, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<PartnerWebhookEndpoint>> ListActiveEndpointsForEventAsync(string eventType, CancellationToken cancellationToken);

    void AddWebhookEndpoint(PartnerWebhookEndpoint endpoint);

    Task<bool> DeliveryExistsAsync(string idempotencyKey, CancellationToken cancellationToken);

    void AddDelivery(PartnerWebhookDelivery delivery);

    Task<IReadOnlyList<PartnerWebhookDelivery>> ClaimPendingDeliveriesAsync(int take, DateTimeOffset now, CancellationToken cancellationToken);

    Task<PartnerWebhookDelivery?> FindDeliveryAsync(Guid id, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<PartnerWebhookDelivery>> ListDeliveriesAsync(Guid partnerAppId, int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<PartnerWebhookDelivery>> ListDeadLettersAsync(int take, CancellationToken cancellationToken);

    Task<PartnerApiUsageDaily?> FindUsageAsync(Guid partnerAppId, DateOnly day, bool tracking, CancellationToken cancellationToken);

    void AddUsage(PartnerApiUsageDaily usage);

    Task<IReadOnlyList<PartnerApiUsageDaily>> ListUsageAsync(Guid partnerAppId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<int> SumUsageTodayAsync(Guid partnerAppId, DateOnly day, CancellationToken cancellationToken);

    Task<PartnerBooking?> FindBookingAsync(Guid id, bool tracking, CancellationToken cancellationToken);

    Task<PartnerBooking?> FindBookingByIdempotencyAsync(Guid partnerAppId, string idempotencyKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<PartnerBooking>> ListBookingsAsync(Guid partnerAppId, CancellationToken cancellationToken);

    void AddBooking(PartnerBooking booking);

    Task<bool> IsSandboxDoctorAsync(Guid doctorId, CancellationToken cancellationToken);

    Task<(Guid? OrganizationId, string? Name, string? City)> FindOrganizationPublicAsync(Guid organizationId, CancellationToken cancellationToken);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IPartnerAccessTokenIssuer
{
    PartnerTokenResult Issue(PartnerPrincipal principal, DateTimeOffset now);
}

public interface IPartnerWebhookPublisher
{
    Task PublishAsync(
        string eventType,
        object payload,
        string idempotencyKey,
        CancellationToken cancellationToken,
        Guid? organizationId = null);
}

public interface IPartnerWebhookSender
{
    Task<(bool Success, int? StatusCode, string? Error)> SendAsync(
        string targetUrl,
        string signingSecret,
        string eventType,
        Guid deliveryId,
        string idempotencyKey,
        string payloadJson,
        CancellationToken cancellationToken);
}
