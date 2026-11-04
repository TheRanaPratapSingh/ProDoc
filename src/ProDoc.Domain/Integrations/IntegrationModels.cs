using System.Security.Cryptography;
using System.Text;
using ProDoc.Domain.Common;

namespace ProDoc.Domain.Integrations;

public enum IntegrationProviderKind
{
    Abdm = 1,
    Laboratory = 2,
    Pharmacy = 3,
    FhirExchange = 4
}

public enum IntegrationProviderMode
{
    Disabled = 0,
    Sandbox = 1,
    ProductionReady = 2
}

public enum IntegrationConsentStatus
{
    Granted = 1,
    Revoked = 2
}

public enum IntegrationDeliveryStatus
{
    Pending = 1,
    Succeeded = 2,
    Failed = 3,
    DeadLetter = 4
}

public enum LabOrderStatus
{
    Requested = 1,
    SampleCollected = 2,
    InProgress = 3,
    Reported = 4,
    Cancelled = 5
}

public enum PharmacyShareStatus
{
    Shared = 1,
    Accepted = 2,
    Dispensed = 3,
    Rejected = 4,
    Withdrawn = 5
}

public enum AbhaLinkStatus
{
    NotLinked = 0,
    SandboxSimulated = 1,
    PendingExternalAuthorization = 2
}

public static class IntegrationPurposes
{
    public const string FhirExport = "fhir.export";
    public const string LabOrders = "lab.orders";
    public const string PharmacyShare = "pharmacy.share";
    public const string AbdmExchange = "abdm.exchange";
}

public sealed class IntegrationProvider
{
    private IntegrationProvider()
    {
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public IntegrationProviderKind Kind { get; private set; }

    public IntegrationProviderMode Mode { get; private set; }

    public bool IsActive { get; private set; }

    public string? BaseUrl { get; private set; }

    public string? CredentialReference { get; private set; }

    public string ContractVersion { get; private set; } = "v1";

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static IntegrationProvider Create(
        string code,
        string displayName,
        IntegrationProviderKind kind,
        IntegrationProviderMode mode,
        DateTimeOffset now,
        string? baseUrl = null,
        string? credentialReference = null,
        string contractVersion = "v1")
    {
        var normalized = code.Trim().ToLowerInvariant();
        if (normalized.Length is < 2 or > 40)
        {
            throw DomainException.Validation("integration_code_invalid", "Enter a valid integration provider code.");
        }

        if (mode == IntegrationProviderMode.ProductionReady && string.IsNullOrWhiteSpace(credentialReference))
        {
            throw DomainException.Validation(
                "integration_credentials_required",
                "Production-ready providers require a credential vault reference. Credentials are never stored in plaintext.");
        }

        return new IntegrationProvider
        {
            Id = Guid.NewGuid(),
            Code = normalized,
            DisplayName = displayName.Trim(),
            Kind = kind,
            Mode = mode,
            IsActive = mode != IntegrationProviderMode.Disabled,
            BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.Trim(),
            CredentialReference = string.IsNullOrWhiteSpace(credentialReference) ? null : credentialReference.Trim(),
            ContractVersion = string.IsNullOrWhiteSpace(contractVersion) ? "v1" : contractVersion.Trim(),
            UpdatedAtUtc = now
        };
    }

    public void Configure(IntegrationProviderMode mode, bool isActive, string? baseUrl, string? credentialReference, DateTimeOffset now)
    {
        if (mode == IntegrationProviderMode.ProductionReady && string.IsNullOrWhiteSpace(credentialReference) && string.IsNullOrWhiteSpace(CredentialReference))
        {
            throw DomainException.Validation(
                "integration_credentials_required",
                "Production-ready providers require a credential vault reference.");
        }

        Mode = mode;
        IsActive = isActive && mode != IntegrationProviderMode.Disabled;
        BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? BaseUrl : baseUrl.Trim();
        if (!string.IsNullOrWhiteSpace(credentialReference))
        {
            CredentialReference = credentialReference.Trim();
        }

        UpdatedAtUtc = now;
    }
}

public sealed class IntegrationConsent
{
    private IntegrationConsent()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public IntegrationProviderKind ProviderKind { get; private set; }

    public string Purpose { get; private set; } = string.Empty;

    public string ScopeJson { get; private set; } = "{}";

    public IntegrationConsentStatus Status { get; private set; }

    public DateTimeOffset GrantedAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static IntegrationConsent Grant(
        Guid userId,
        IntegrationProviderKind kind,
        string purpose,
        string? scopeJson,
        DateTimeOffset now)
    {
        var purposeText = purpose.Trim();
        if (purposeText.Length is < 3 or > 80)
        {
            throw DomainException.Validation("integration_purpose_invalid", "Enter a valid integration purpose.");
        }

        return new IntegrationConsent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProviderKind = kind,
            Purpose = purposeText,
            ScopeJson = string.IsNullOrWhiteSpace(scopeJson) ? "{}" : scopeJson.Trim(),
            Status = IntegrationConsentStatus.Granted,
            GrantedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Regrant(string? scopeJson, DateTimeOffset now)
    {
        Status = IntegrationConsentStatus.Granted;
        ScopeJson = string.IsNullOrWhiteSpace(scopeJson) ? ScopeJson : scopeJson.Trim();
        GrantedAtUtc = now;
        RevokedAtUtc = null;
        UpdatedAtUtc = now;
    }

    public void Revoke(DateTimeOffset now)
    {
        Status = IntegrationConsentStatus.Revoked;
        RevokedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public bool IsActive => Status == IntegrationConsentStatus.Granted && RevokedAtUtc is null;
}

public sealed class IntegrationDeliveryLog
{
    private IntegrationDeliveryLog()
    {
    }

    public Guid Id { get; private set; }

    public Guid? ProviderId { get; private set; }

    public IntegrationProviderKind ProviderKind { get; private set; }

    public string Operation { get; private set; } = string.Empty;

    public string IdempotencyKey { get; private set; } = string.Empty;

    public IntegrationDeliveryStatus Status { get; private set; }

    public string ReferenceType { get; private set; } = string.Empty;

    public Guid? ReferenceId { get; private set; }

    public string? ExternalReference { get; private set; }

    public string? ErrorCode { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static IntegrationDeliveryLog Start(
        IntegrationProviderKind kind,
        string operation,
        string idempotencyKey,
        string referenceType,
        Guid? referenceId,
        DateTimeOffset now,
        Guid? providerId = null)
    {
        var key = idempotencyKey.Trim();
        if (key.Length is < 4 or > 120)
        {
            throw DomainException.Validation("integration_idempotency_invalid", "Provide an integration idempotency key.");
        }

        return new IntegrationDeliveryLog
        {
            Id = Guid.NewGuid(),
            ProviderId = providerId,
            ProviderKind = kind,
            Operation = operation.Trim(),
            IdempotencyKey = key,
            Status = IntegrationDeliveryStatus.Pending,
            ReferenceType = referenceType.Trim(),
            ReferenceId = referenceId,
            AttemptCount = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void MarkSucceeded(string? externalReference, DateTimeOffset now)
    {
        Status = IntegrationDeliveryStatus.Succeeded;
        ExternalReference = externalReference;
        UpdatedAtUtc = now;
    }

    public void MarkFailed(string errorCode, DateTimeOffset now, bool deadLetter = false)
    {
        Status = deadLetter ? IntegrationDeliveryStatus.DeadLetter : IntegrationDeliveryStatus.Failed;
        ErrorCode = errorCode.Trim();
        AttemptCount += 1;
        UpdatedAtUtc = now;
    }
}

public sealed class IntegrationWebhookEvent
{
    private IntegrationWebhookEvent()
    {
    }

    public Guid Id { get; private set; }

    public IntegrationProviderKind ProviderKind { get; private set; }

    public string ProviderCode { get; private set; } = string.Empty;

    public string EventId { get; private set; } = string.Empty;

    public string EventType { get; private set; } = string.Empty;

    public string PayloadHash { get; private set; } = string.Empty;

    public bool SignatureValid { get; private set; }

    public DateTimeOffset ReceivedAtUtc { get; private set; }

    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    public static IntegrationWebhookEvent Record(
        IntegrationProviderKind kind,
        string providerCode,
        string eventId,
        string eventType,
        string rawBody,
        bool signatureValid,
        DateTimeOffset now)
    {
        var id = eventId.Trim();
        if (id.Length is < 4 or > 120)
        {
            throw DomainException.Validation("webhook_event_invalid", "Webhook event id is invalid.");
        }

        return new IntegrationWebhookEvent
        {
            Id = Guid.NewGuid(),
            ProviderKind = kind,
            ProviderCode = providerCode.Trim().ToLowerInvariant(),
            EventId = id,
            EventType = eventType.Trim(),
            PayloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody))),
            SignatureValid = signatureValid,
            ReceivedAtUtc = now
        };
    }

    public void MarkProcessed(DateTimeOffset now) => ProcessedAtUtc ??= now;
}

public sealed class LabOrder
{
    private LabOrder()
    {
    }

    public Guid Id { get; private set; }

    public Guid PatientUserId { get; private set; }

    public Guid? AppointmentId { get; private set; }

    public Guid? PrescriptionId { get; private set; }

    public string ProviderCode { get; private set; } = string.Empty;

    public string CatalogCode { get; private set; } = string.Empty;

    public string TestName { get; private set; } = string.Empty;

    public LabOrderStatus Status { get; private set; }

    public string? ExternalOrderId { get; private set; }

    public bool ResultsArePreliminary { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static LabOrder Create(
        Guid patientUserId,
        string providerCode,
        string catalogCode,
        string testName,
        DateTimeOffset now,
        Guid? appointmentId = null,
        Guid? prescriptionId = null)
    {
        return new LabOrder
        {
            Id = Guid.NewGuid(),
            PatientUserId = patientUserId,
            AppointmentId = appointmentId,
            PrescriptionId = prescriptionId,
            ProviderCode = providerCode.Trim().ToLowerInvariant(),
            CatalogCode = catalogCode.Trim().ToUpperInvariant(),
            TestName = testName.Trim(),
            Status = LabOrderStatus.Requested,
            ResultsArePreliminary = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void UpdateStatus(LabOrderStatus status, string? externalOrderId, DateTimeOffset now, bool preliminary = true)
    {
        Status = status;
        if (!string.IsNullOrWhiteSpace(externalOrderId))
        {
            ExternalOrderId = externalOrderId.Trim();
        }

        ResultsArePreliminary = preliminary || status != LabOrderStatus.Reported;
        UpdatedAtUtc = now;
    }
}

public sealed class LabReport
{
    private LabReport()
    {
    }

    public Guid Id { get; private set; }

    public Guid LabOrderId { get; private set; }

    public Guid PatientUserId { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    public string ResultJson { get; private set; } = "{}";

    public bool IsPreliminary { get; private set; }

    public string Disclaimer { get; private set; } = string.Empty;

    public DateTimeOffset ReportedAtUtc { get; private set; }

    public static LabReport Create(
        Guid labOrderId,
        Guid patientUserId,
        string summary,
        string resultJson,
        bool isPreliminary,
        DateTimeOffset now)
    {
        return new LabReport
        {
            Id = Guid.NewGuid(),
            LabOrderId = labOrderId,
            PatientUserId = patientUserId,
            Summary = summary.Trim(),
            ResultJson = string.IsNullOrWhiteSpace(resultJson) ? "{}" : resultJson,
            IsPreliminary = isPreliminary,
            Disclaimer = isPreliminary
                ? "Sandbox/preliminary laboratory data. Not confirmed clinical findings."
                : "Review with a licensed clinician before clinical decisions.",
            ReportedAtUtc = now
        };
    }
}

public sealed class PharmacyShare
{
    private PharmacyShare()
    {
    }

    public Guid Id { get; private set; }

    public Guid PatientUserId { get; private set; }

    public Guid PrescriptionId { get; private set; }

    public string ProviderCode { get; private set; } = string.Empty;

    public string PharmacyName { get; private set; } = string.Empty;

    public PharmacyShareStatus Status { get; private set; }

    public string? ExternalShareId { get; private set; }

    public DateTimeOffset SharedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static PharmacyShare Create(
        Guid patientUserId,
        Guid prescriptionId,
        string providerCode,
        string pharmacyName,
        DateTimeOffset now)
    {
        return new PharmacyShare
        {
            Id = Guid.NewGuid(),
            PatientUserId = patientUserId,
            PrescriptionId = prescriptionId,
            ProviderCode = providerCode.Trim().ToLowerInvariant(),
            PharmacyName = pharmacyName.Trim(),
            Status = PharmacyShareStatus.Shared,
            SharedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void UpdateStatus(PharmacyShareStatus status, string? externalShareId, DateTimeOffset now)
    {
        if (Status == PharmacyShareStatus.Withdrawn)
        {
            throw DomainException.Conflict("pharmacy_share_withdrawn", "This pharmacy share was withdrawn.");
        }

        Status = status;
        if (!string.IsNullOrWhiteSpace(externalShareId))
        {
            ExternalShareId = externalShareId.Trim();
        }

        UpdatedAtUtc = now;
    }

    public void Withdraw(DateTimeOffset now)
    {
        Status = PharmacyShareStatus.Withdrawn;
        UpdatedAtUtc = now;
    }
}

public sealed class AbhaLink
{
    private AbhaLink()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public AbhaLinkStatus Status { get; private set; }

    public string? SandboxHandle { get; private set; }

    public string Notes { get; private set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static AbhaLink Create(Guid userId, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = AbhaLinkStatus.NotLinked,
            Notes = "ABDM connectivity requires official onboarding. No production ABHA verification has been performed.",
            UpdatedAtUtc = now
        };

    public void MarkSandboxSimulated(string sandboxHandle, DateTimeOffset now)
    {
        Status = AbhaLinkStatus.SandboxSimulated;
        SandboxHandle = sandboxHandle.Trim();
        Notes = "Sandbox simulation only. This is not an authorized ABHA verification.";
        UpdatedAtUtc = now;
    }

    public void MarkPendingExternal(DateTimeOffset now)
    {
        Status = AbhaLinkStatus.PendingExternalAuthorization;
        Notes = "Awaiting authorized ABDM facility registration and consent manager connectivity.";
        UpdatedAtUtc = now;
    }

    public void Clear(DateTimeOffset now)
    {
        Status = AbhaLinkStatus.NotLinked;
        SandboxHandle = null;
        Notes = "ABHA link cleared by the user.";
        UpdatedAtUtc = now;
    }
}
