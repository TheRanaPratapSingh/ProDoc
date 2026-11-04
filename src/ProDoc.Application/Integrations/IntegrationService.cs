using ProDoc.Application.Abstractions;
using ProDoc.Application.Providers;
using ProDoc.Domain.Clinical;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Integrations;
using ProDoc.Domain.Queue;

namespace ProDoc.Application.Integrations;

public sealed class IntegrationService(
    IIntegrationStore store,
    FhirMappingService fhir,
    IEnumerable<ILabIntegrationAdapter> labAdapters,
    IEnumerable<IPharmacyIntegrationAdapter> pharmacyAdapters,
    IAbdmIntegrationAdapter abdm,
    IClock clock)
{
    public async Task SeedDefaultsAsync(CancellationToken cancellationToken)
    {
        await EnsureProviderAsync("abdm-sandbox", "ABDM readiness (sandbox)", IntegrationProviderKind.Abdm, IntegrationProviderMode.Sandbox, cancellationToken);
        await EnsureProviderAsync("lab-sandbox", "Diagnostic lab (sandbox)", IntegrationProviderKind.Laboratory, IntegrationProviderMode.Sandbox, cancellationToken);
        await EnsureProviderAsync("pharmacy-sandbox", "Pharmacy directory (sandbox)", IntegrationProviderKind.Pharmacy, IntegrationProviderMode.Sandbox, cancellationToken);
        await EnsureProviderAsync("fhir-export", "FHIR R4 subset export", IntegrationProviderKind.FhirExchange, IntegrationProviderMode.Sandbox, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task<IntegrationStatusOverview> GetStatusAsync(Guid userId, CancellationToken cancellationToken)
    {
        var providers = (await store.ListProvidersAsync(cancellationToken)).Select(MapProvider).ToArray();
        var consents = (await store.ListConsentsAsync(userId, cancellationToken)).Select(MapConsent).ToArray();
        var abha = await GetAbhaAsync(userId, cancellationToken);
        var labs = await store.ListLabOrdersAsync(userId, cancellationToken);
        var shares = await store.ListPharmacySharesAsync(userId, cancellationToken);
        return new IntegrationStatusOverview(
            providers,
            consents,
            abha,
            labs.Count(item => item.Status is LabOrderStatus.Requested or LabOrderStatus.SampleCollected or LabOrderStatus.InProgress),
            shares.Count(item => item.Status is PharmacyShareStatus.Shared or PharmacyShareStatus.Accepted));
    }

    public async Task<IReadOnlyList<IntegrationProviderDetails>> ListProvidersAsync(CancellationToken cancellationToken) =>
        (await store.ListProvidersAsync(cancellationToken)).Select(MapProvider).ToArray();

    public async Task<IntegrationProviderDetails> ConfigureProviderAsync(
        Guid adminUserId,
        Guid providerId,
        string mode,
        bool isActive,
        string? baseUrl,
        string? credentialReference,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<IntegrationProviderMode>(mode, true, out var parsedMode))
        {
            throw DomainException.Validation("integration_mode_invalid", "Choose Disabled, Sandbox, or ProductionReady.");
        }

        var provider = await store.FindProviderAsync(providerId, true, cancellationToken)
            ?? throw DomainException.NotFound("integration_provider_not_found", "That integration provider was not found.");
        provider.Configure(parsedMode, isActive, baseUrl, credentialReference, clock.UtcNow);
        await store.AddAuditAsync(Audit(adminUserId, AuditActions.IntegrationProviderConfigured, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapProvider(provider);
    }

    public async Task<IReadOnlyList<IntegrationConsentDetails>> ListConsentsAsync(Guid userId, CancellationToken cancellationToken) =>
        (await store.ListConsentsAsync(userId, cancellationToken)).Select(MapConsent).ToArray();

    public async Task<IntegrationConsentDetails> GrantConsentAsync(
        Guid userId,
        string providerKind,
        string purpose,
        string? scopeJson,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<IntegrationProviderKind>(providerKind, true, out var kind))
        {
            throw DomainException.Validation("integration_kind_invalid", "Choose a valid integration provider kind.");
        }

        EnsureKnownPurpose(purpose);
        var existing = await store.FindConsentAsync(userId, kind, purpose.Trim(), true, cancellationToken);
        if (existing is null)
        {
            existing = IntegrationConsent.Grant(userId, kind, purpose, scopeJson, clock.UtcNow);
            store.AddConsent(existing);
        }
        else
        {
            existing.Regrant(scopeJson, clock.UtcNow);
        }

        await store.AddAuditAsync(Audit(userId, AuditActions.IntegrationConsentGranted, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapConsent(existing);
    }

    public async Task<IntegrationConsentDetails> RevokeConsentAsync(
        Guid userId,
        string providerKind,
        string purpose,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<IntegrationProviderKind>(providerKind, true, out var kind))
        {
            throw DomainException.Validation("integration_kind_invalid", "Choose a valid integration provider kind.");
        }

        var existing = await store.FindConsentAsync(userId, kind, purpose.Trim(), true, cancellationToken)
            ?? throw DomainException.NotFound("integration_consent_not_found", "That consent was not found.");
        existing.Revoke(clock.UtcNow);
        await store.AddAuditAsync(Audit(userId, AuditActions.IntegrationConsentRevoked, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapConsent(existing);
    }

    public async Task<FhirBundleExport> ExportFhirAsync(
        Guid userId,
        Guid? appointmentId,
        Guid? prescriptionId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await RequireConsentAsync(userId, IntegrationProviderKind.FhirExchange, IntegrationPurposes.FhirExport, cancellationToken);
        var profile = await store.FindUserProfileAsync(userId, cancellationToken);
        var record = await store.FindPrimaryMedicalRecordAsync(userId, cancellationToken);
        var appointments = appointmentId is Guid aid
            ? new[]
            {
                await store.FindAppointmentAsync(aid, cancellationToken)
                    ?? throw DomainException.NotFound("appointment_not_found", "That appointment was not found.")
            }
            : await store.ListRecentAppointmentsAsync(userId, 20, cancellationToken);

        if (appointments.Any(item => item.AccountUserId != userId))
        {
            throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        }

        Prescription? prescription = null;
        if (prescriptionId is Guid pid)
        {
            prescription = await store.FindPrescriptionAsync(pid, cancellationToken)
                ?? throw DomainException.NotFound("prescription_not_found", "That prescription was not found.");
            if (prescription.AccountUserId != userId)
            {
                throw DomainException.NotFound("prescription_not_found", "That prescription was not found.");
            }
        }

        var first = appointments.FirstOrDefault();
        var doctor = first is null ? null : await store.FindDoctorAsync(first.DoctorId, cancellationToken);
        var clinic = first is null ? null : await store.FindClinicAsync(first.ClinicId, cancellationToken);
        var bundle = fhir.BuildPatientBundle(userId, profile.FullName, profile.Phone, record, appointments, doctor, clinic, prescription);
        await store.AddAuditAsync(Audit(userId, AuditActions.FhirBundleExported, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return bundle;
    }

    public bool ValidateFhirBundle(string json) =>
        fhir.TryValidateBundleShape(json, out _);

    public Task<AbdmReadinessResult> GetAbdmReadinessAsync(CancellationToken cancellationToken) =>
        abdm.GetReadinessAsync(cancellationToken);

    public async Task<AbhaLinkDetails> GetAbhaAsync(Guid userId, CancellationToken cancellationToken)
    {
        var link = await store.FindAbhaLinkAsync(userId, false, cancellationToken);
        return MapAbha(link);
    }

    public async Task<AbhaLinkDetails> SimulateAbhaSandboxAsync(
        Guid userId,
        string? preferredHandle,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await RequireConsentAsync(userId, IntegrationProviderKind.Abdm, IntegrationPurposes.AbdmExchange, cancellationToken);
        var readiness = await abdm.GetReadinessAsync(cancellationToken);
        if (!readiness.SandboxAvailable)
        {
            throw DomainException.Validation("abdm_sandbox_unavailable", readiness.Message);
        }

        var simulated = await abdm.SimulateSandboxLinkAsync(userId, preferredHandle, cancellationToken);
        var link = await store.FindAbhaLinkAsync(userId, true, cancellationToken);
        if (link is null)
        {
            link = AbhaLink.Create(userId, clock.UtcNow);
            store.AddAbhaLink(link);
        }

        if (simulated.Simulated && !string.IsNullOrWhiteSpace(simulated.SandboxHandle))
        {
            link.MarkSandboxSimulated(simulated.SandboxHandle, clock.UtcNow);
        }
        else
        {
            link.MarkPendingExternal(clock.UtcNow);
        }

        await store.AddAuditAsync(Audit(userId, AuditActions.AbhaSandboxSimulated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapAbha(link);
    }

    public async Task<AbhaLinkDetails> ClearAbhaAsync(Guid userId, RequestMetadata metadata, CancellationToken cancellationToken)
    {
        var link = await store.FindAbhaLinkAsync(userId, true, cancellationToken);
        if (link is null)
        {
            return MapAbha(null);
        }

        link.Clear(clock.UtcNow);
        await store.AddAuditAsync(Audit(userId, AuditActions.AbhaLinkCleared, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapAbha(link);
    }

    public async Task<IReadOnlyList<LabCatalogItem>> ListLabCatalogAsync(string? providerCode, CancellationToken cancellationToken)
    {
        var adapter = ResolveLab(providerCode);
        await EnsureProviderEnabledAsync(adapter.ProviderCode, IntegrationProviderKind.Laboratory, cancellationToken);
        return await adapter.ListCatalogAsync(cancellationToken);
    }

    public async Task<LabOrderDetails> CreateLabOrderAsync(
        Guid userId,
        string providerCode,
        string catalogCode,
        Guid? appointmentId,
        Guid? prescriptionId,
        string idempotencyKey,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await RequireConsentAsync(userId, IntegrationProviderKind.Laboratory, IntegrationPurposes.LabOrders, cancellationToken);
        var adapter = ResolveLab(providerCode);
        var provider = await EnsureProviderEnabledAsync(adapter.ProviderCode, IntegrationProviderKind.Laboratory, cancellationToken);

        var existingDelivery = await store.FindDeliveryByIdempotencyAsync(idempotencyKey, cancellationToken);
        if (existingDelivery is { Status: IntegrationDeliveryStatus.Succeeded, ReferenceId: Guid existingId })
        {
            var existingOrder = await store.FindLabOrderAsync(existingId, false, cancellationToken);
            if (existingOrder is not null)
            {
                return MapLabOrder(existingOrder);
            }
        }

        var catalog = await adapter.ListCatalogAsync(cancellationToken);
        var item = catalog.FirstOrDefault(entry => string.Equals(entry.Code, catalogCode.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw DomainException.Validation("lab_catalog_invalid", "Choose a test from the laboratory catalogue.");

        if (appointmentId is Guid aid)
        {
            var appointment = await store.FindAppointmentAsync(aid, cancellationToken)
                ?? throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
            if (appointment.AccountUserId != userId)
            {
                throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
            }
        }

        if (prescriptionId is Guid pid)
        {
            var prescription = await store.FindPrescriptionAsync(pid, cancellationToken)
                ?? throw DomainException.NotFound("prescription_not_found", "That prescription was not found.");
            if (prescription.AccountUserId != userId)
            {
                throw DomainException.NotFound("prescription_not_found", "That prescription was not found.");
            }
        }

        var order = LabOrder.Create(userId, adapter.ProviderCode, item.Code, item.Name, clock.UtcNow, appointmentId, prescriptionId);
        store.AddLabOrder(order);
        var delivery = IntegrationDeliveryLog.Start(
            IntegrationProviderKind.Laboratory,
            "lab.order.submit",
            idempotencyKey,
            "LabOrder",
            order.Id,
            clock.UtcNow,
            provider.Id);
        store.AddDelivery(delivery);

        var result = await adapter.SubmitOrderAsync(
            new LabOrderRequest(item.Code, item.Name, userId, appointmentId, prescriptionId, idempotencyKey),
            cancellationToken);
        if (!result.Accepted)
        {
            delivery.MarkFailed(result.ErrorCode ?? "lab_submit_failed", clock.UtcNow);
            order.UpdateStatus(LabOrderStatus.Cancelled, null, clock.UtcNow);
            await store.SaveChangesAsync(cancellationToken);
            throw DomainException.Validation(result.ErrorCode ?? "lab_submit_failed", "The laboratory adapter rejected this order.");
        }

        order.UpdateStatus(result.NextStatus ?? LabOrderStatus.Requested, result.ExternalOrderId, clock.UtcNow);
        delivery.MarkSucceeded(result.ExternalOrderId, clock.UtcNow);
        await store.NotifyAsync(
            UserNotification.Create(
                userId,
                "lab.order.requested",
                order.Id,
                "Lab test requested",
                $"{item.Name} was submitted to the sandbox laboratory. Results are not confirmed clinical findings.",
                clock.UtcNow),
            cancellationToken);
        await store.AddAuditAsync(Audit(userId, AuditActions.LabOrderCreated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapLabOrder(order);
    }

    public async Task<IReadOnlyList<LabOrderDetails>> ListLabOrdersAsync(Guid userId, CancellationToken cancellationToken) =>
        (await store.ListLabOrdersAsync(userId, cancellationToken)).Select(MapLabOrder).ToArray();

    public async Task<IReadOnlyList<LabReportDetails>> ListLabReportsAsync(Guid userId, CancellationToken cancellationToken) =>
        (await store.ListLabReportsAsync(userId, cancellationToken)).Select(MapLabReport).ToArray();

    public async Task<LabReportDetails> GetLabReportAsync(Guid userId, Guid reportId, CancellationToken cancellationToken)
    {
        var report = await store.FindLabReportAsync(reportId, cancellationToken)
            ?? throw DomainException.NotFound("lab_report_not_found", "That laboratory report was not found.");
        if (report.PatientUserId != userId)
        {
            throw DomainException.NotFound("lab_report_not_found", "That laboratory report was not found.");
        }

        return MapLabReport(report);
    }

    public async Task HandleLabWebhookAsync(
        string providerCode,
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        var adapter = ResolveLab(providerCode);
        var parsed = adapter.ParseWebhook(rawBody, headers);
        if (!parsed.SignatureValid)
        {
            throw DomainException.Unauthorized("webhook_signature_invalid", "Webhook signature verification failed.");
        }

        var existing = await store.FindWebhookAsync(adapter.ProviderCode, parsed.EventId, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        var webhook = IntegrationWebhookEvent.Record(
            IntegrationProviderKind.Laboratory,
            adapter.ProviderCode,
            parsed.EventId,
            parsed.EventType,
            rawBody,
            true,
            clock.UtcNow);
        store.AddWebhook(webhook);

        if (!string.IsNullOrWhiteSpace(parsed.ExternalOrderId))
        {
            var order = await store.FindLabOrderByExternalAsync(adapter.ProviderCode, parsed.ExternalOrderId, true, cancellationToken);
            if (order is not null)
            {
                if (parsed.Status is LabOrderStatus status)
                {
                    order.UpdateStatus(status, parsed.ExternalOrderId, clock.UtcNow, parsed.Preliminary);
                }

                if (!string.IsNullOrWhiteSpace(parsed.ReportSummary)
                    && !await store.LabReportExistsForOrderAsync(order.Id, cancellationToken))
                {
                    var report = LabReport.Create(
                        order.Id,
                        order.PatientUserId,
                        parsed.ReportSummary,
                        parsed.ReportJson ?? "{}",
                        parsed.Preliminary,
                        clock.UtcNow);
                    store.AddLabReport(report);
                    await store.NotifyAsync(
                        UserNotification.Create(
                            order.PatientUserId,
                            "lab.report.available",
                            report.Id,
                            "Lab report available",
                            $"{report.Summary} — {report.Disclaimer}",
                            clock.UtcNow),
                        cancellationToken);
                }
            }
        }

        webhook.MarkProcessed(clock.UtcNow);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PharmacyDirectoryItem>> ListPharmaciesAsync(string? providerCode, string? city, CancellationToken cancellationToken)
    {
        var adapter = ResolvePharmacy(providerCode);
        await EnsureProviderEnabledAsync(adapter.ProviderCode, IntegrationProviderKind.Pharmacy, cancellationToken);
        return await adapter.ListPharmaciesAsync(city, cancellationToken);
    }

    public async Task<PharmacyShareDetails> SharePrescriptionAsync(
        Guid userId,
        Guid prescriptionId,
        string providerCode,
        string pharmacyCode,
        string idempotencyKey,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await RequireConsentAsync(userId, IntegrationProviderKind.Pharmacy, IntegrationPurposes.PharmacyShare, cancellationToken);
        var adapter = ResolvePharmacy(providerCode);
        var provider = await EnsureProviderEnabledAsync(adapter.ProviderCode, IntegrationProviderKind.Pharmacy, cancellationToken);

        var existingDelivery = await store.FindDeliveryByIdempotencyAsync(idempotencyKey, cancellationToken);
        if (existingDelivery is { Status: IntegrationDeliveryStatus.Succeeded, ReferenceId: Guid existingId })
        {
            var existingShare = await store.FindPharmacyShareAsync(existingId, false, cancellationToken);
            if (existingShare is not null)
            {
                return MapPharmacyShare(existingShare);
            }
        }

        var prescription = await store.FindPrescriptionAsync(prescriptionId, cancellationToken)
            ?? throw DomainException.NotFound("prescription_not_found", "That prescription was not found.");
        if (prescription.AccountUserId != userId)
        {
            throw DomainException.NotFound("prescription_not_found", "That prescription was not found.");
        }

        var directory = await adapter.ListPharmaciesAsync(null, cancellationToken);
        var pharmacy = directory.FirstOrDefault(item => string.Equals(item.Code, pharmacyCode.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw DomainException.Validation("pharmacy_not_found", "Choose a pharmacy from the directory.");

        var snapshot = new
        {
            prescriptionId = prescription.Id,
            number = prescription.Number,
            status = prescription.Status.ToString(),
            items = prescription.Versions.OrderByDescending(item => item.VersionNumber).FirstOrDefault()?.Items
                .Select(item => new { item.MedicineName, item.Dosage, item.Frequency, item.Duration, item.Route })
                .ToArray() ?? Array.Empty<object>(),
            note = "Shared with patient consent. Automatic substitution is not permitted."
        };

        var share = PharmacyShare.Create(userId, prescriptionId, adapter.ProviderCode, pharmacy.Name, clock.UtcNow);
        store.AddPharmacyShare(share);
        var delivery = IntegrationDeliveryLog.Start(
            IntegrationProviderKind.Pharmacy,
            "pharmacy.share",
            idempotencyKey,
            "PharmacyShare",
            share.Id,
            clock.UtcNow,
            provider.Id);
        store.AddDelivery(delivery);

        var result = await adapter.SharePrescriptionAsync(
            new PharmacyShareRequest(userId, prescriptionId, pharmacy.Code, pharmacy.Name, idempotencyKey, snapshot),
            cancellationToken);
        if (!result.Accepted)
        {
            delivery.MarkFailed(result.ErrorCode ?? "pharmacy_share_failed", clock.UtcNow);
            share.Withdraw(clock.UtcNow);
            await store.SaveChangesAsync(cancellationToken);
            throw DomainException.Validation(result.ErrorCode ?? "pharmacy_share_failed", "The pharmacy adapter rejected this share.");
        }

        share.UpdateStatus(result.NextStatus ?? PharmacyShareStatus.Shared, result.ExternalShareId, clock.UtcNow);
        delivery.MarkSucceeded(result.ExternalShareId, clock.UtcNow);
        await store.AddAuditAsync(Audit(userId, AuditActions.PharmacyShareCreated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapPharmacyShare(share);
    }

    public async Task<IReadOnlyList<PharmacyShareDetails>> ListPharmacySharesAsync(Guid userId, CancellationToken cancellationToken) =>
        (await store.ListPharmacySharesAsync(userId, cancellationToken)).Select(MapPharmacyShare).ToArray();

    public async Task<PharmacyShareDetails> WithdrawPharmacyShareAsync(
        Guid userId,
        Guid shareId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var share = await store.FindPharmacyShareAsync(shareId, true, cancellationToken)
            ?? throw DomainException.NotFound("pharmacy_share_not_found", "That pharmacy share was not found.");
        if (share.PatientUserId != userId)
        {
            throw DomainException.NotFound("pharmacy_share_not_found", "That pharmacy share was not found.");
        }

        share.Withdraw(clock.UtcNow);
        await store.AddAuditAsync(Audit(userId, AuditActions.PharmacyShareWithdrawn, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapPharmacyShare(share);
    }

    public async Task HandlePharmacyWebhookAsync(
        string providerCode,
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        var adapter = ResolvePharmacy(providerCode);
        var parsed = adapter.ParseWebhook(rawBody, headers);
        if (!parsed.SignatureValid)
        {
            throw DomainException.Unauthorized("webhook_signature_invalid", "Webhook signature verification failed.");
        }

        var existing = await store.FindWebhookAsync(adapter.ProviderCode, parsed.EventId, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        var webhook = IntegrationWebhookEvent.Record(
            IntegrationProviderKind.Pharmacy,
            adapter.ProviderCode,
            parsed.EventId,
            parsed.EventType,
            rawBody,
            true,
            clock.UtcNow);
        store.AddWebhook(webhook);

        if (!string.IsNullOrWhiteSpace(parsed.ExternalShareId))
        {
            var share = await store.FindPharmacyShareByExternalAsync(adapter.ProviderCode, parsed.ExternalShareId, true, cancellationToken);
            if (share is not null && parsed.Status is PharmacyShareStatus status)
            {
                share.UpdateStatus(status, parsed.ExternalShareId, clock.UtcNow);
            }
        }

        webhook.MarkProcessed(clock.UtcNow);
        await store.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureProviderAsync(
        string code,
        string displayName,
        IntegrationProviderKind kind,
        IntegrationProviderMode mode,
        CancellationToken cancellationToken)
    {
        if (await store.FindProviderByCodeAsync(code, false, cancellationToken) is not null)
        {
            return;
        }

        store.AddProvider(IntegrationProvider.Create(code, displayName, kind, mode, clock.UtcNow));
    }

    private async Task<IntegrationProvider> EnsureProviderEnabledAsync(
        string code,
        IntegrationProviderKind kind,
        CancellationToken cancellationToken)
    {
        var provider = await store.FindProviderByCodeAsync(code, false, cancellationToken)
            ?? throw DomainException.NotFound("integration_provider_not_found", "That integration provider was not found.");
        if (provider.Kind != kind || !provider.IsActive || provider.Mode == IntegrationProviderMode.Disabled)
        {
            throw DomainException.Validation("integration_disabled", "This integration provider is disabled.");
        }

        return provider;
    }

    private async Task RequireConsentAsync(
        Guid userId,
        IntegrationProviderKind kind,
        string purpose,
        CancellationToken cancellationToken)
    {
        var consent = await store.FindConsentAsync(userId, kind, purpose, false, cancellationToken);
        if (consent is not { IsActive: true })
        {
            throw DomainException.Validation(
                "integration_consent_required",
                "Grant explicit patient consent before sharing or exchanging this health data.");
        }
    }

    private ILabIntegrationAdapter ResolveLab(string? providerCode)
    {
        var code = string.IsNullOrWhiteSpace(providerCode) ? "lab-sandbox" : providerCode.Trim().ToLowerInvariant();
        return labAdapters.FirstOrDefault(item => string.Equals(item.ProviderCode, code, StringComparison.OrdinalIgnoreCase))
            ?? throw DomainException.NotFound("lab_adapter_not_found", "No laboratory adapter is registered for that provider.");
    }

    private IPharmacyIntegrationAdapter ResolvePharmacy(string? providerCode)
    {
        var code = string.IsNullOrWhiteSpace(providerCode) ? "pharmacy-sandbox" : providerCode.Trim().ToLowerInvariant();
        return pharmacyAdapters.FirstOrDefault(item => string.Equals(item.ProviderCode, code, StringComparison.OrdinalIgnoreCase))
            ?? throw DomainException.NotFound("pharmacy_adapter_not_found", "No pharmacy adapter is registered for that provider.");
    }

    private static void EnsureKnownPurpose(string purpose)
    {
        var normalized = purpose.Trim();
        if (normalized is not (
            IntegrationPurposes.FhirExport
            or IntegrationPurposes.LabOrders
            or IntegrationPurposes.PharmacyShare
            or IntegrationPurposes.AbdmExchange))
        {
            throw DomainException.Validation("integration_purpose_invalid", "Choose a supported integration purpose.");
        }
    }

    private static IntegrationProviderDetails MapProvider(IntegrationProvider provider) =>
        new(
            provider.Id,
            provider.Code,
            provider.DisplayName,
            provider.Kind.ToString(),
            provider.Mode.ToString(),
            provider.IsActive,
            provider.BaseUrl,
            provider.CredentialReference,
            provider.ContractVersion,
            provider.UpdatedAtUtc);

    private static IntegrationConsentDetails MapConsent(IntegrationConsent consent) =>
        new(
            consent.Id,
            consent.ProviderKind.ToString(),
            consent.Purpose,
            consent.ScopeJson,
            consent.Status.ToString(),
            consent.GrantedAtUtc,
            consent.RevokedAtUtc);

    private static AbhaLinkDetails MapAbha(AbhaLink? link) =>
        link is null
            ? new AbhaLinkDetails(AbhaLinkStatus.NotLinked.ToString(), null, "No ABHA link on file. Production ABDM connectivity is not claimed.", DateTimeOffset.UtcNow, false)
            : new AbhaLinkDetails(link.Status.ToString(), link.SandboxHandle, link.Notes, link.UpdatedAtUtc, false);

    private static LabOrderDetails MapLabOrder(LabOrder order) =>
        new(
            order.Id,
            order.ProviderCode,
            order.CatalogCode,
            order.TestName,
            order.Status.ToString(),
            order.ExternalOrderId,
            order.ResultsArePreliminary,
            order.AppointmentId,
            order.CreatedAtUtc,
            order.UpdatedAtUtc);

    private static LabReportDetails MapLabReport(LabReport report) =>
        new(
            report.Id,
            report.LabOrderId,
            report.Summary,
            report.ResultJson,
            report.IsPreliminary,
            report.Disclaimer,
            report.ReportedAtUtc);

    private static PharmacyShareDetails MapPharmacyShare(PharmacyShare share) =>
        new(
            share.Id,
            share.PrescriptionId,
            share.ProviderCode,
            share.PharmacyName,
            share.Status.ToString(),
            share.ExternalShareId,
            share.SharedAtUtc,
            share.UpdatedAtUtc);

    private AuditLog Audit(Guid userId, string action, RequestMetadata metadata) =>
        AuditLog.Record(userId, action, clock.UtcNow, metadata.IpAddress, metadata.UserAgent);
}
