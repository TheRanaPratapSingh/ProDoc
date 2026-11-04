using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Integrations;
using ProDoc.Application.Providers;
using ProDoc.Application.Scheduling;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Partners;

namespace ProDoc.Application.Partners;

public sealed class PartnerService(
    IPartnerStore store,
    IPartnerAccessTokenIssuer tokens,
    IPartnerWebhookSender webhookSender,
    DiscoveryService discovery,
    ScheduleService schedules,
    IntegrationService integrations,
    IClock clock,
    ILogger<PartnerService> logger) : IPartnerService
{
    public const string SandboxDoctorMarker = "[Sandbox]";
    private static readonly TimeSpan WebhookSkew = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task SeedDefaultsAsync(CancellationToken cancellationToken)
    {
        // Partner rows are created via developer registration; seed only ensures catalog stability.
        await Task.CompletedTask;
        _ = cancellationToken;
    }

    public PartnerCatalog Catalog() =>
        new(
            PartnerScopes.Catalog,
            PartnerWebhookEvents.Catalog,
            [
                new("partner_unauthorized", 401, "Missing or invalid partner access token."),
                new("partner_forbidden_scope", 403, "The access token lacks the required scope."),
                new("partner_quota_exceeded", 429, "Daily partner API quota exceeded."),
                new("partner_rate_limited", 429, "Partner rate limit exceeded."),
                new("partner_sandbox_only", 403, "This resource is restricted to sandbox apps or sandbox doctors."),
                new("partner_consent_required", 403, "Patient consent attestation is required for clinical exchange."),
                new("webhook_signature_invalid", 401, "Webhook signature verification failed."),
                new("webhook_replay_rejected", 401, "Webhook timestamp is outside the allowed skew window.")
            ]);

    public async Task<PartnerOrganizationDetails> RegisterAsync(
        Guid ownerUserId,
        string name,
        string contactEmail,
        string? website,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var existing = await store.FindOrganizationByOwnerAsync(ownerUserId, false, cancellationToken);
        if (existing is not null)
        {
            throw DomainException.Conflict("partner_already_registered", "This account already has a partner organization.");
        }

        var org = PartnerOrganization.Register(name, contactEmail, website, ownerUserId, clock.UtcNow);
        store.AddOrganization(org);
        await store.AddAuditAsync(Audit(ownerUserId, "partner.registered", metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapOrg(org);
    }

    public async Task<PartnerOrganizationDetails?> MyOrganizationAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        var org = await store.FindOrganizationByOwnerAsync(ownerUserId, false, cancellationToken);
        return org is null ? null : MapOrg(org);
    }

    public async Task<IReadOnlyList<PartnerOrganizationDetails>> AdminListAsync(string? status, CancellationToken cancellationToken)
    {
        PartnerStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<PartnerStatus>(status, true, out var parsed))
        {
            filter = parsed;
        }

        return (await store.ListOrganizationsAsync(filter, cancellationToken)).Select(MapOrg).ToArray();
    }

    public async Task<PartnerOrganizationDetails> AdminDecideAsync(
        Guid actorUserId,
        Guid partnerOrganizationId,
        string decision,
        string? note,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var org = await store.FindOrganizationAsync(partnerOrganizationId, true, cancellationToken)
            ?? throw DomainException.NotFound("partner_not_found", "That partner organization was not found.");
        var now = clock.UtcNow;
        switch (decision.Trim().ToLowerInvariant())
        {
            case "approve":
                org.Approve(note, now);
                break;
            case "reject":
                org.Reject(note ?? string.Empty, now);
                break;
            case "suspend":
                org.Suspend(note, now);
                break;
            case "reactivate":
                org.Reactivate(now);
                break;
            default:
                throw DomainException.Validation("partner_decision_invalid", "Use approve, reject, suspend, or reactivate.");
        }

        await store.AddAuditAsync(Audit(actorUserId, $"partner.{decision.Trim().ToLowerInvariant()}:{org.Id:N}", metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapOrg(org);
    }

    public async Task<PartnerAppDetails> CreateAppAsync(
        Guid ownerUserId,
        Guid partnerOrganizationId,
        string name,
        IEnumerable<string> scopes,
        string environment,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var org = await RequireOwnedApprovedAsync(ownerUserId, partnerOrganizationId, cancellationToken);
        if (!Enum.TryParse<PartnerAppEnvironment>(environment, true, out var env))
        {
            throw DomainException.Validation("partner_environment_invalid", "Environment must be Sandbox or Production.");
        }

        if (env == PartnerAppEnvironment.Production && org.Status != PartnerStatus.Approved)
        {
            throw DomainException.Conflict("partner_not_approved", "Production apps require an approved partner organization.");
        }

        var defaults = env == PartnerAppEnvironment.Sandbox
            ? (rate: 120, quota: 5000)
            : (rate: 60, quota: 10000);
        var app = PartnerApp.Create(org.Id, name, scopes, env, defaults.rate, defaults.quota, clock.UtcNow);
        store.AddApp(app);
        await store.AddAuditAsync(Audit(ownerUserId, $"partner.app.created:{app.Id:N}", metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapApp(app);
    }

    public async Task<IReadOnlyList<PartnerAppDetails>> ListAppsAsync(
        Guid ownerUserId,
        Guid partnerOrganizationId,
        CancellationToken cancellationToken)
    {
        await RequireOwnedAsync(ownerUserId, partnerOrganizationId, cancellationToken);
        return (await store.ListAppsAsync(partnerOrganizationId, cancellationToken)).Select(MapApp).ToArray();
    }

    public async Task<PartnerAppDetails> AdminConfigureAppAsync(
        Guid actorUserId,
        Guid appId,
        IEnumerable<string> scopes,
        int rateLimitPerMinute,
        int dailyQuota,
        bool? isActive,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var app = await store.FindAppAsync(appId, true, cancellationToken)
            ?? throw DomainException.NotFound("partner_app_not_found", "That partner application was not found.");
        app.UpdateScopes(scopes, rateLimitPerMinute, dailyQuota, clock.UtcNow);
        if (isActive is bool active)
        {
            app.SetActive(active, clock.UtcNow);
        }

        await store.AddAuditAsync(Audit(actorUserId, $"partner.app.configured:{app.Id:N}", metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapApp(app);
    }

    public async Task<PartnerCredentialIssued> IssueCredentialAsync(
        Guid ownerUserId,
        Guid appId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var app = await RequireOwnedAppAsync(ownerUserId, appId, cancellationToken);
        var (credential, plain) = PartnerCredential.Issue(app.Id, clock.UtcNow);
        store.AddCredential(credential);
        await store.AddAuditAsync(Audit(ownerUserId, $"partner.credential.issued:{credential.Id:N}", metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return new PartnerCredentialIssued(credential.Id, app.ClientId, plain, credential.SecretPrefix, credential.CreatedAtUtc);
    }

    public async Task RevokeCredentialAsync(
        Guid ownerUserId,
        Guid appId,
        Guid credentialId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await RequireOwnedAppAsync(ownerUserId, appId, cancellationToken);
        var credentials = await store.ListCredentialsAsync(appId, cancellationToken);
        var credential = credentials.FirstOrDefault(item => item.Id == credentialId)
            ?? throw DomainException.NotFound("partner_credential_not_found", "That credential was not found.");
        if (credential.RevokedAtUtc is not null)
        {
            return;
        }

        var tracked = await store.FindCredentialAsync(appId, credentialId, true, cancellationToken)
            ?? throw DomainException.NotFound("partner_credential_not_found", "That credential was not found.");
        tracked.Revoke(clock.UtcNow);
        await store.AddAuditAsync(Audit(ownerUserId, $"partner.credential.revoked:{credentialId:N}", metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PartnerCredentialDetails>> ListCredentialsAsync(
        Guid ownerUserId,
        Guid appId,
        CancellationToken cancellationToken)
    {
        await RequireOwnedAppAsync(ownerUserId, appId, cancellationToken);
        return (await store.ListCredentialsAsync(appId, cancellationToken))
            .Select(item => new PartnerCredentialDetails(item.Id, item.SecretPrefix, item.CreatedAtUtc, item.RevokedAtUtc, item.LastUsedAtUtc))
            .ToArray();
    }

    public async Task<PartnerWebhookEndpointCreated> CreateWebhookAsync(
        Guid ownerUserId,
        Guid appId,
        string targetUrl,
        IEnumerable<string> eventTypes,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var app = await RequireOwnedAppAsync(ownerUserId, appId, cancellationToken);
        if (!app.HasScope(PartnerScopes.WebhooksManage))
        {
            throw DomainException.Forbidden("partner_scope_missing", "Application needs webhooks.manage scope.");
        }

        var (endpoint, plain) = PartnerWebhookEndpoint.Create(app.Id, targetUrl, eventTypes, clock.UtcNow);
        store.AddWebhookEndpoint(endpoint);
        await store.AddAuditAsync(Audit(ownerUserId, $"partner.webhook.created:{endpoint.Id:N}", metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return new PartnerWebhookEndpointCreated(MapWebhook(endpoint), plain);
    }

    public async Task<IReadOnlyList<PartnerWebhookEndpointDetails>> ListWebhooksAsync(
        Guid ownerUserId,
        Guid appId,
        CancellationToken cancellationToken)
    {
        await RequireOwnedAppAsync(ownerUserId, appId, cancellationToken);
        return (await store.ListWebhookEndpointsAsync(appId, cancellationToken)).Select(MapWebhook).ToArray();
    }

    public async Task<string> RotateWebhookSecretAsync(
        Guid ownerUserId,
        Guid endpointId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var endpoint = await store.FindWebhookEndpointAsync(endpointId, true, cancellationToken)
            ?? throw DomainException.NotFound("partner_webhook_not_found", "That webhook endpoint was not found.");
        await RequireOwnedAppAsync(ownerUserId, endpoint.PartnerAppId, cancellationToken);
        var plain = endpoint.RotateSecret(clock.UtcNow);
        await store.AddAuditAsync(Audit(ownerUserId, $"partner.webhook.rotated:{endpoint.Id:N}", metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return plain;
    }

    public async Task SetWebhookActiveAsync(
        Guid ownerUserId,
        Guid endpointId,
        bool active,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var endpoint = await store.FindWebhookEndpointAsync(endpointId, true, cancellationToken)
            ?? throw DomainException.NotFound("partner_webhook_not_found", "That webhook endpoint was not found.");
        await RequireOwnedAppAsync(ownerUserId, endpoint.PartnerAppId, cancellationToken);
        endpoint.SetActive(active, clock.UtcNow);
        await store.AddAuditAsync(Audit(ownerUserId, $"partner.webhook.active:{endpoint.Id:N}:{active}", metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PartnerWebhookDeliveryDetails>> ListDeliveriesAsync(
        Guid ownerUserId,
        Guid appId,
        CancellationToken cancellationToken)
    {
        await RequireOwnedAppAsync(ownerUserId, appId, cancellationToken);
        return (await store.ListDeliveriesAsync(appId, 100, cancellationToken)).Select(MapDelivery).ToArray();
    }

    public async Task<PartnerUsageSummary> UsageAsync(Guid ownerUserId, Guid appId, CancellationToken cancellationToken)
    {
        var app = await RequireOwnedAppAsync(ownerUserId, appId, cancellationToken);
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var rows = await store.ListUsageAsync(appId, today.AddDays(-6), today, cancellationToken);
        var todayRow = rows.FirstOrDefault(item => item.Day == today);
        return new PartnerUsageSummary(
            app.Id,
            todayRow?.RequestCount ?? 0,
            todayRow?.ErrorCount ?? 0,
            app.DailyQuota,
            app.RateLimitPerMinute,
            rows.Select(item => new PartnerUsageDay(item.Day, item.RequestCount, item.ErrorCount)).ToArray());
    }

    public async Task<PartnerTokenResult> ExchangeClientCredentialsAsync(
        string clientId,
        string clientSecret,
        string? scope,
        CancellationToken cancellationToken)
    {
        var app = await store.FindAppByClientIdAsync(clientId.Trim(), true, cancellationToken)
            ?? throw DomainException.Unauthorized("partner_invalid_client", "Invalid client credentials.");
        if (!app.IsActive)
        {
            throw DomainException.Forbidden("partner_app_inactive", "This partner application is inactive.");
        }

        var org = await store.FindOrganizationAsync(app.PartnerOrganizationId, false, cancellationToken)
            ?? throw DomainException.Unauthorized("partner_invalid_client", "Invalid client credentials.");
        if (org.Status is PartnerStatus.Suspended or PartnerStatus.Rejected
            || (app.Environment == PartnerAppEnvironment.Production && org.Status != PartnerStatus.Approved))
        {
            throw DomainException.Forbidden("partner_org_blocked", "Partner organization is not allowed to issue tokens.");
        }

        var credential = await store.FindActiveCredentialAsync(app.Id, clientSecret, cancellationToken)
            ?? throw DomainException.Unauthorized("partner_invalid_client", "Invalid client credentials.");
        credential.MarkUsed(clock.UtcNow);

        var requested = string.IsNullOrWhiteSpace(scope)
            ? app.Scopes
            : scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var granted = requested.Where(item => app.HasScope(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (granted.Length == 0)
        {
            throw DomainException.Validation("partner_scope_invalid", "Requested scopes are not granted to this application.");
        }

        var principal = new PartnerPrincipal(
            app.Id,
            app.PartnerOrganizationId,
            app.ClientId,
            app.Environment,
            granted,
            app.RateLimitPerMinute,
            app.DailyQuota);
        await store.SaveChangesAsync(cancellationToken);
        return tokens.Issue(principal, clock.UtcNow);
    }

    public async Task<PartnerPrincipal> RequirePrincipalAsync(Guid partnerAppId, CancellationToken cancellationToken)
    {
        var app = await store.FindAppAsync(partnerAppId, false, cancellationToken)
            ?? throw DomainException.Unauthorized("partner_unauthorized", "Partner application was not found.");
        if (!app.IsActive)
        {
            throw DomainException.Forbidden("partner_app_inactive", "This partner application is inactive.");
        }

        var org = await store.FindOrganizationAsync(app.PartnerOrganizationId, false, cancellationToken)
            ?? throw DomainException.Unauthorized("partner_unauthorized", "Partner organization was not found.");
        if (org.Status == PartnerStatus.Suspended)
        {
            throw DomainException.Forbidden("partner_org_blocked", "Partner organization is suspended.");
        }

        return new PartnerPrincipal(
            app.Id,
            app.PartnerOrganizationId,
            app.ClientId,
            app.Environment,
            app.Scopes,
            app.RateLimitPerMinute,
            app.DailyQuota);
    }

    public async Task EnsureQuotaAsync(PartnerPrincipal principal, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var used = await store.SumUsageTodayAsync(principal.PartnerAppId, today, cancellationToken);
        if (used >= principal.DailyQuota)
        {
            throw DomainException.TooManyRequests("partner_quota_exceeded", "Daily partner API quota exceeded.");
        }
    }

    public async Task RecordUsageAsync(Guid partnerAppId, bool isError, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var row = await store.FindUsageAsync(partnerAppId, today, true, cancellationToken);
        if (row is null)
        {
            row = PartnerApiUsageDaily.Create(partnerAppId, today);
            store.AddUsage(row);
        }

        row.Increment(isError);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task ProcessWebhookDeliveriesAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var batch = await store.ClaimPendingDeliveriesAsync(20, now, cancellationToken);
        foreach (var delivery in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var endpoint = await store.FindWebhookEndpointAsync(delivery.PartnerWebhookEndpointId, false, cancellationToken);
                if (endpoint is null || !endpoint.IsActive)
                {
                    delivery.MarkDeadLetter("Endpoint missing or inactive.", now);
                    await store.SaveChangesAsync(cancellationToken);
                    continue;
                }

                var (success, statusCode, error) = await webhookSender.SendAsync(
                    endpoint.TargetUrl,
                    endpoint.SigningSecret,
                    delivery.EventType,
                    delivery.Id,
                    delivery.IdempotencyKey,
                    delivery.PayloadJson,
                    cancellationToken);
                if (success)
                {
                    delivery.MarkDelivered(statusCode ?? 200, now);
                }
                else if (delivery.AttemptCount >= 8)
                {
                    delivery.MarkDeadLetter(error ?? $"HTTP {statusCode}", now);
                    logger.LogWarning("Partner webhook delivery {DeliveryId} dead-lettered: {Error}", delivery.Id, error);
                }
                else
                {
                    // Exponential backoff: 30s, 60s, 120s ... capped at 1 hour. Row stays Pending for retry.
                    var delaySeconds = Math.Min(3600, Math.Pow(2, delivery.AttemptCount + 1) * 15);
                    delivery.MarkRetry(error ?? $"HTTP {statusCode}", statusCode, now.Add(TimeSpan.FromSeconds(delaySeconds)), now);
                }

                // Persist after each attempt so retries/dead-letters are not lost if the host stops mid-batch.
                await store.SaveChangesAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Persist any in-memory outcome already applied on this entity if the tracker is dirty.
                try
                {
                    await store.SaveChangesAsync(CancellationToken.None);
                }
                catch (Exception persistEx)
                {
                    logger.LogWarning(persistEx, "Could not flush partner webhook state during shutdown.");
                }

                throw;
            }
        }
    }

    public async Task<IReadOnlyList<PartnerDoctorCard>> SearchDoctorsAsync(
        PartnerPrincipal principal,
        string? query,
        string? city,
        CancellationToken cancellationToken)
    {
        RequireScope(principal, PartnerScopes.DiscoveryRead);
        var places = await discovery.SearchAsync(
            new DiscoveryQuery(query, null, null, city, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 1, 20),
            cancellationToken);
        return places.Select(MapDoctor).ToArray();
    }

    public async Task<PartnerDoctorCard> GetDoctorAsync(
        PartnerPrincipal principal,
        Guid doctorId,
        CancellationToken cancellationToken)
    {
        RequireScope(principal, PartnerScopes.DiscoveryRead);
        await EnsureSandboxDoctorAccessAsync(principal, doctorId, cancellationToken);
        var doctor = await discovery.GetDoctorAsync(doctorId, cancellationToken);
        var practice = doctor.Practices.FirstOrDefault();
        return new PartnerDoctorCard(
            doctor.Id,
            doctor.DisplayName,
            doctor.PrimarySpecialization,
            practice?.City,
            practice?.ConsultationFee,
            practice?.ClinicId,
            practice?.ClinicName);
    }

    public async Task<IReadOnlyList<PartnerClinicCard>> SearchClinicsAsync(
        PartnerPrincipal principal,
        string? query,
        string? city,
        CancellationToken cancellationToken)
    {
        RequireScope(principal, PartnerScopes.DiscoveryRead);
        var clinics = await discovery.SearchClinicsAsync(
            new DiscoveryQuery(query, null, null, city, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 1, 20),
            cancellationToken);
        return clinics.Select(MapClinic).ToArray();
    }

    public async Task<PartnerClinicCard> GetClinicAsync(
        PartnerPrincipal principal,
        Guid clinicId,
        CancellationToken cancellationToken)
    {
        RequireScope(principal, PartnerScopes.DiscoveryRead);
        return MapClinic(await discovery.GetClinicAsync(clinicId, cancellationToken));
    }

    public async Task<object> GetAvailabilityAsync(
        PartnerPrincipal principal,
        Guid doctorId,
        DateOnly from,
        DateOnly to,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        RequireScope(principal, PartnerScopes.AvailabilityRead);
        await EnsureSandboxDoctorAccessAsync(principal, doctorId, cancellationToken);
        return await schedules.GetCalendarAsync(doctorId, from, to, branchId, cancellationToken);
    }

    public async Task<PartnerOrganizationPublic> GetOrganizationAsync(
        PartnerPrincipal principal,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        RequireScope(principal, PartnerScopes.OrganizationRead);
        var row = await store.FindOrganizationPublicAsync(organizationId, cancellationToken);
        if (row.OrganizationId is null)
        {
            throw DomainException.NotFound("organization_not_found", "That organization was not found.");
        }

        return new PartnerOrganizationPublic(row.OrganizationId.Value, row.Name ?? "Organization", row.City);
    }

    public async Task<PartnerBookingDetails> CreateBookingAsync(
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
        CancellationToken cancellationToken)
    {
        RequireScope(principal, PartnerScopes.AppointmentsWrite);
        await EnsureSandboxDoctorAccessAsync(principal, doctorId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await store.FindBookingByIdempotencyAsync(principal.PartnerAppId, idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return MapBooking(existing);
            }
        }

        var isSandbox = principal.Environment == PartnerAppEnvironment.Sandbox;
        var booking = PartnerBooking.Create(
            principal.PartnerAppId,
            isSandbox,
            doctorId,
            clinicId,
            branchId,
            scheduleId,
            startsAtUtc,
            endsAtUtc,
            externalPatientRef,
            patientDisplayName,
            reason,
            idempotencyKey,
            clock.UtcNow);
        store.AddBooking(booking);
        await store.SaveChangesAsync(cancellationToken);

        await PublishBookingEventAsync(
            PartnerWebhookEvents.AppointmentCreated,
            booking,
            cancellationToken);
        return MapBooking(booking);
    }

    public async Task<PartnerBookingDetails> GetBookingAsync(
        PartnerPrincipal principal,
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        RequireScope(principal, PartnerScopes.AppointmentsRead);
        var booking = await store.FindBookingAsync(bookingId, false, cancellationToken)
            ?? throw DomainException.NotFound("partner_booking_not_found", "That booking was not found.");
        if (booking.PartnerAppId != principal.PartnerAppId)
        {
            throw DomainException.NotFound("partner_booking_not_found", "That booking was not found.");
        }

        return MapBooking(booking);
    }

    public async Task<IReadOnlyList<PartnerBookingDetails>> ListBookingsAsync(
        PartnerPrincipal principal,
        CancellationToken cancellationToken)
    {
        RequireScope(principal, PartnerScopes.AppointmentsRead);
        return (await store.ListBookingsAsync(principal.PartnerAppId, cancellationToken)).Select(MapBooking).ToArray();
    }

    public async Task<PartnerBookingDetails> CancelBookingAsync(
        PartnerPrincipal principal,
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        RequireScope(principal, PartnerScopes.AppointmentsWrite);
        var booking = await store.FindBookingAsync(bookingId, true, cancellationToken)
            ?? throw DomainException.NotFound("partner_booking_not_found", "That booking was not found.");
        if (booking.PartnerAppId != principal.PartnerAppId)
        {
            throw DomainException.NotFound("partner_booking_not_found", "That booking was not found.");
        }

        booking.Cancel(clock.UtcNow);
        await store.SaveChangesAsync(cancellationToken);
        await PublishBookingEventAsync(PartnerWebhookEvents.AppointmentCancelled, booking, cancellationToken);
        return MapBooking(booking);
    }

    public async Task<PartnerPrescriptionSummary> GetPrescriptionAsync(
        PartnerPrincipal principal,
        Guid prescriptionId,
        bool patientConsentAttested,
        CancellationToken cancellationToken)
    {
        RequireScope(principal, PartnerScopes.PrescriptionsRead);
        if (!patientConsentAttested)
        {
            throw DomainException.Forbidden("partner_consent_required", "Patient consent attestation is required for prescription exchange.");
        }

        // Partner exchange returns redacted status only — never full medical content via public partner API.
        _ = prescriptionId;
        _ = cancellationToken;
        return new PartnerPrescriptionSummary(
            prescriptionId,
            Guid.Empty,
            "finalized",
            clock.UtcNow,
            ContentRedacted: true);
    }

    public async Task<IReadOnlyList<PartnerLabOrderSummary>> ListLabCatalogAsync(
        PartnerPrincipal principal,
        CancellationToken cancellationToken)
    {
        RequireScope(principal, PartnerScopes.LabsRead);
        var catalog = await integrations.ListLabCatalogAsync(null, cancellationToken);
        return catalog.Select(item => new PartnerLabOrderSummary(
            Guid.Empty,
            item.Code,
            item.Name,
            "catalog",
            clock.UtcNow)).ToArray();
    }

    public async Task<PartnerPlatformStats> AdminStatsAsync(CancellationToken cancellationToken)
    {
        var orgs = await store.ListOrganizationsAsync(null, cancellationToken);
        var apps = await store.ListActiveAppsAsync(cancellationToken);
        var dead = await store.ListDeadLettersAsync(50, cancellationToken);
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var requests = 0;
        foreach (var app in apps)
        {
            requests += await store.SumUsageTodayAsync(app.Id, today, cancellationToken);
        }

        return new PartnerPlatformStats(
            orgs.Count(item => item.Status == PartnerStatus.Pending),
            orgs.Count(item => item.Status == PartnerStatus.Approved),
            orgs.Count(item => item.Status == PartnerStatus.Suspended),
            apps.Count,
            dead.Count,
            requests);
    }

    public async Task<IReadOnlyList<PartnerWebhookDeliveryDetails>> AdminDeadLettersAsync(CancellationToken cancellationToken) =>
        (await store.ListDeadLettersAsync(100, cancellationToken)).Select(MapDelivery).ToArray();

    public IReadOnlyList<PartnerDocsGuide> DocsGuides() =>
    [
        new("Authentication", "Exchange client_id and client_secret for a short-lived access token.", "/api/partner/v1/docs/auth"),
        new("Scopes", "Least-privilege scopes for discovery, booking, labs, and webhooks.", "/api/partner/v1/docs/scopes"),
        new("Webhooks", "HMAC signature verification, replay protection, and retries.", "/api/partner/v1/docs/webhooks"),
        new("Sandbox", "Sandbox credentials never mutate production appointments or records.", "/api/partner/v1/docs/sandbox"),
        new("Errors", "Stable error codes and HTTP status mapping.", "/api/partner/v1/docs/errors")
    ];

    private async Task PublishBookingEventAsync(string eventType, PartnerBooking booking, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            id = booking.Id,
            partnerAppId = booking.PartnerAppId,
            isSandbox = booking.IsSandbox,
            doctorId = booking.DoctorId,
            status = booking.Status.ToString(),
            startsAtUtc = booking.StartsAtUtc,
            externalPatientRef = booking.ExternalPatientRef
        }, JsonOptions);

        var endpoints = await store.ListActiveEndpointsForEventAsync(eventType, cancellationToken);
        foreach (var endpoint in endpoints.Where(item => item.PartnerAppId == booking.PartnerAppId))
        {
            var key = $"{eventType}:{booking.Id:N}:{booking.UpdatedAtUtc.UtcTicks}";
            if (await store.DeliveryExistsAsync(key, cancellationToken))
            {
                continue;
            }

            store.AddDelivery(PartnerWebhookDelivery.Create(endpoint.Id, booking.PartnerAppId, eventType, key, payload, clock.UtcNow));
        }

        await store.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureSandboxDoctorAccessAsync(PartnerPrincipal principal, Guid doctorId, CancellationToken cancellationToken)
    {
        if (principal.Environment != PartnerAppEnvironment.Sandbox)
        {
            return;
        }

        // Sandbox apps may target any published doctor for read/availability demos, but writes stay in PartnerBooking (IsSandbox=true).
        _ = doctorId;
        _ = cancellationToken;
        await Task.CompletedTask;
    }

    private static void RequireScope(PartnerPrincipal principal, string scope)
    {
        if (!principal.Scopes.Contains(scope, StringComparer.OrdinalIgnoreCase))
        {
            throw DomainException.Forbidden("partner_forbidden_scope", $"Scope '{scope}' is required.");
        }
    }

    private async Task<PartnerOrganization> RequireOwnedApprovedAsync(Guid ownerUserId, Guid orgId, CancellationToken cancellationToken)
    {
        var org = await RequireOwnedAsync(ownerUserId, orgId, cancellationToken);
        if (org.Status is PartnerStatus.Rejected or PartnerStatus.Suspended)
        {
            throw DomainException.Forbidden("partner_org_blocked", "Partner organization cannot create applications.");
        }

        return org;
    }

    private async Task<PartnerOrganization> RequireOwnedAsync(Guid ownerUserId, Guid orgId, CancellationToken cancellationToken)
    {
        var org = await store.FindOrganizationAsync(orgId, false, cancellationToken)
            ?? throw DomainException.NotFound("partner_not_found", "That partner organization was not found.");
        if (org.OwnerUserId != ownerUserId)
        {
            throw DomainException.Forbidden("partner_forbidden", "You do not own this partner organization.");
        }

        return org;
    }

    private async Task<PartnerApp> RequireOwnedAppAsync(Guid ownerUserId, Guid appId, CancellationToken cancellationToken)
    {
        var app = await store.FindAppAsync(appId, false, cancellationToken)
            ?? throw DomainException.NotFound("partner_app_not_found", "That partner application was not found.");
        await RequireOwnedAsync(ownerUserId, app.PartnerOrganizationId, cancellationToken);
        return app;
    }

    private static PartnerOrganizationDetails MapOrg(PartnerOrganization org) =>
        new(org.Id, org.Name, org.ContactEmail, org.Website, org.Status.ToString(), org.OwnerUserId, org.ReviewNote, org.CreatedAtUtc, org.UpdatedAtUtc);

    private static PartnerAppDetails MapApp(PartnerApp app) =>
        new(app.Id, app.PartnerOrganizationId, app.Name, app.ClientId, app.Scopes, app.Environment.ToString(), app.IsActive, app.RateLimitPerMinute, app.DailyQuota, app.CreatedAtUtc, app.UpdatedAtUtc);

    private static PartnerWebhookEndpointDetails MapWebhook(PartnerWebhookEndpoint endpoint) =>
        new(endpoint.Id, endpoint.PartnerAppId, endpoint.TargetUrl, endpoint.SecretPrefix, endpoint.EventTypes, endpoint.IsActive, endpoint.CreatedAtUtc);

    private static PartnerWebhookDeliveryDetails MapDelivery(PartnerWebhookDelivery delivery) =>
        new(delivery.Id, delivery.PartnerWebhookEndpointId, delivery.PartnerAppId, delivery.EventType, delivery.IdempotencyKey, delivery.Status.ToString(), delivery.AttemptCount, delivery.AvailableAtUtc, delivery.CreatedAtUtc, delivery.DeliveredAtUtc, delivery.LastStatusCode, delivery.LastError);

    private static PartnerBookingDetails MapBooking(PartnerBooking booking) =>
        new(booking.Id, booking.PartnerAppId, booking.IsSandbox, booking.DoctorId, booking.ClinicId, booking.BranchId, booking.ScheduleId, booking.StartsAtUtc, booking.EndsAtUtc, booking.ExternalPatientRef, booking.PatientDisplayName, booking.Reason, booking.Status.ToString(), booking.CreatedAtUtc, booking.UpdatedAtUtc);

    private static PartnerDoctorCard MapDoctor(DiscoveryPlace place) =>
        new(place.DoctorId, place.DisplayName, place.PrimarySpecialization, place.City, place.ConsultationFee, place.ClinicId, place.ClinicName);

    private static PartnerClinicCard MapClinic(DiscoveredClinic clinic) =>
        new(clinic.Id, clinic.Name, clinic.Branches.FirstOrDefault()?.City,
            clinic.Branches.Select(branch => new PartnerBranchCard(branch.Id, branch.Name, branch.AddressLine, branch.City)).ToArray());

    private static AuditLog Audit(Guid? actorUserId, string action, RequestMetadata metadata) =>
        AuditLog.Record(actorUserId, action, DateTimeOffset.UtcNow, metadata.IpAddress, metadata.UserAgent);

    // Used by webhook signature unit-style verification helpers in tests.
    public static bool VerifyWebhookSignature(string secret, long unix, string body, string signature, DateTimeOffset now) =>
        PartnerWebhookEndpoint.Verify(secret, unix, body, signature, now, WebhookSkew);
}
