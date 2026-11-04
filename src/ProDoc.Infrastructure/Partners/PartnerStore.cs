using Microsoft.EntityFrameworkCore;
using ProDoc.Application.Partners;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Partners;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.Partners;

public sealed class PartnerStore(ProDocDbContext db) : IPartnerStore
{
    public Task<PartnerOrganization?> FindOrganizationAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.PartnerOrganizations.AsQueryable() : db.PartnerOrganizations.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public Task<PartnerOrganization?> FindOrganizationByOwnerAsync(Guid ownerUserId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.PartnerOrganizations.AsQueryable() : db.PartnerOrganizations.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.OwnerUserId == ownerUserId, cancellationToken);
    }

    public async Task<IReadOnlyList<PartnerOrganization>> ListOrganizationsAsync(PartnerStatus? status, CancellationToken cancellationToken)
    {
        var query = db.PartnerOrganizations.AsNoTracking().AsQueryable();
        if (status is PartnerStatus filter)
        {
            query = query.Where(item => item.Status == filter);
        }

        return await query.OrderByDescending(item => item.CreatedAtUtc).ToListAsync(cancellationToken);
    }

    public void AddOrganization(PartnerOrganization organization) => db.PartnerOrganizations.Add(organization);

    public Task<PartnerApp?> FindAppAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.PartnerApps.AsQueryable() : db.PartnerApps.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public Task<PartnerApp?> FindAppByClientIdAsync(string clientId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.PartnerApps.AsQueryable() : db.PartnerApps.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.ClientId == clientId, cancellationToken);
    }

    public async Task<IReadOnlyList<PartnerApp>> ListAppsAsync(Guid partnerOrganizationId, CancellationToken cancellationToken) =>
        await db.PartnerApps.AsNoTracking()
            .Where(item => item.PartnerOrganizationId == partnerOrganizationId)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PartnerApp>> ListActiveAppsAsync(CancellationToken cancellationToken) =>
        await db.PartnerApps.AsNoTracking().Where(item => item.IsActive).ToListAsync(cancellationToken);

    public void AddApp(PartnerApp app) => db.PartnerApps.Add(app);

    public async Task<IReadOnlyList<PartnerCredential>> ListCredentialsAsync(Guid partnerAppId, CancellationToken cancellationToken) =>
        await db.PartnerCredentials.AsNoTracking()
            .Where(item => item.PartnerAppId == partnerAppId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<PartnerCredential?> FindActiveCredentialAsync(Guid partnerAppId, string plainSecret, CancellationToken cancellationToken)
    {
        var hash = PartnerCredential.Hash(plainSecret);
        var candidates = await db.PartnerCredentials
            .Where(item => item.PartnerAppId == partnerAppId && item.RevokedAtUtc == null && item.SecretHash == hash)
            .ToListAsync(cancellationToken);
        return candidates.FirstOrDefault(item => item.Matches(plainSecret));
    }

    public Task<PartnerCredential?> FindCredentialAsync(Guid partnerAppId, Guid credentialId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.PartnerCredentials.AsQueryable() : db.PartnerCredentials.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.PartnerAppId == partnerAppId && item.Id == credentialId, cancellationToken);
    }

    public void AddCredential(PartnerCredential credential) => db.PartnerCredentials.Add(credential);

    public async Task<IReadOnlyList<PartnerWebhookEndpoint>> ListWebhookEndpointsAsync(Guid partnerAppId, CancellationToken cancellationToken) =>
        await db.PartnerWebhookEndpoints.AsNoTracking()
            .Where(item => item.PartnerAppId == partnerAppId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task<PartnerWebhookEndpoint?> FindWebhookEndpointAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.PartnerWebhookEndpoints.AsQueryable() : db.PartnerWebhookEndpoints.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PartnerWebhookEndpoint>> ListActiveEndpointsForEventAsync(string eventType, CancellationToken cancellationToken)
    {
        var endpoints = await db.PartnerWebhookEndpoints.AsNoTracking()
            .Where(item => item.IsActive)
            .ToListAsync(cancellationToken);
        return endpoints.Where(item => item.EventTypes.Contains(eventType, StringComparer.OrdinalIgnoreCase)).ToArray();
    }

    public void AddWebhookEndpoint(PartnerWebhookEndpoint endpoint) => db.PartnerWebhookEndpoints.Add(endpoint);

    public Task<bool> DeliveryExistsAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        db.PartnerWebhookDeliveries.AsNoTracking().AnyAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);

    public void AddDelivery(PartnerWebhookDelivery delivery) => db.PartnerWebhookDeliveries.Add(delivery);

    public async Task<IReadOnlyList<PartnerWebhookDelivery>> ClaimPendingDeliveriesAsync(int take, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var pending = await db.PartnerWebhookDeliveries
            .Where(item => item.Status == PartnerWebhookDeliveryStatus.Pending && item.AvailableAtUtc <= now)
            .OrderBy(item => item.AvailableAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
        return pending;
    }

    public Task<PartnerWebhookDelivery?> FindDeliveryAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.PartnerWebhookDeliveries.AsQueryable() : db.PartnerWebhookDeliveries.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PartnerWebhookDelivery>> ListDeliveriesAsync(Guid partnerAppId, int take, CancellationToken cancellationToken) =>
        await db.PartnerWebhookDeliveries.AsNoTracking()
            .Where(item => item.PartnerAppId == partnerAppId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PartnerWebhookDelivery>> ListDeadLettersAsync(int take, CancellationToken cancellationToken) =>
        await db.PartnerWebhookDeliveries.AsNoTracking()
            .Where(item => item.Status == PartnerWebhookDeliveryStatus.DeadLetter)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<PartnerApiUsageDaily?> FindUsageAsync(Guid partnerAppId, DateOnly day, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.PartnerApiUsageDaily.AsQueryable() : db.PartnerApiUsageDaily.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.PartnerAppId == partnerAppId && item.Day == day, cancellationToken);
    }

    public void AddUsage(PartnerApiUsageDaily usage) => db.PartnerApiUsageDaily.Add(usage);

    public async Task<IReadOnlyList<PartnerApiUsageDaily>> ListUsageAsync(Guid partnerAppId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await db.PartnerApiUsageDaily.AsNoTracking()
            .Where(item => item.PartnerAppId == partnerAppId && item.Day >= from && item.Day <= to)
            .OrderBy(item => item.Day)
            .ToListAsync(cancellationToken);

    public async Task<int> SumUsageTodayAsync(Guid partnerAppId, DateOnly day, CancellationToken cancellationToken)
    {
        var row = await db.PartnerApiUsageDaily.AsNoTracking()
            .SingleOrDefaultAsync(item => item.PartnerAppId == partnerAppId && item.Day == day, cancellationToken);
        return row?.RequestCount ?? 0;
    }

    public Task<PartnerBooking?> FindBookingAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.PartnerBookings.AsQueryable() : db.PartnerBookings.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public Task<PartnerBooking?> FindBookingByIdempotencyAsync(Guid partnerAppId, string idempotencyKey, CancellationToken cancellationToken) =>
        db.PartnerBookings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.PartnerAppId == partnerAppId && item.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<IReadOnlyList<PartnerBooking>> ListBookingsAsync(Guid partnerAppId, CancellationToken cancellationToken) =>
        await db.PartnerBookings.AsNoTracking()
            .Where(item => item.PartnerAppId == partnerAppId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

    public void AddBooking(PartnerBooking booking) => db.PartnerBookings.Add(booking);

    public Task<bool> IsSandboxDoctorAsync(Guid doctorId, CancellationToken cancellationToken) =>
        db.Doctors.AsNoTracking().AnyAsync(
            item => item.Id == doctorId && item.DisplayName != null && item.DisplayName.Contains("[Sandbox]"),
            cancellationToken);

    public async Task<(Guid? OrganizationId, string? Name, string? City)> FindOrganizationPublicAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var org = await db.Organizations.AsNoTracking().SingleOrDefaultAsync(item => item.Id == organizationId, cancellationToken);
        if (org is null)
        {
            return (null, null, null);
        }

        return (org.Id, org.Name, null);
    }

    public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(audit);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
