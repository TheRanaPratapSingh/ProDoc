using ProDoc.Domain.Integrations;

namespace ProDoc.Application.Integrations;

public sealed record LabOrderRequest(
    string CatalogCode,
    string TestName,
    Guid PatientUserId,
    Guid? AppointmentId,
    Guid? PrescriptionId,
    string IdempotencyKey);

public sealed record LabOrderAdapterResult(bool Accepted, string? ExternalOrderId, string? ErrorCode, LabOrderStatus? NextStatus);

public sealed record LabWebhookParse(bool SignatureValid, string EventId, string EventType, string? ExternalOrderId, LabOrderStatus? Status, string? ReportSummary, string? ReportJson, bool Preliminary);

public interface ILabIntegrationAdapter
{
    string ProviderCode { get; }

    Task<IReadOnlyList<LabCatalogItem>> ListCatalogAsync(CancellationToken cancellationToken);

    Task<LabOrderAdapterResult> SubmitOrderAsync(LabOrderRequest request, CancellationToken cancellationToken);

    LabWebhookParse ParseWebhook(string rawBody, IReadOnlyDictionary<string, string> headers);
}

public sealed record PharmacyShareRequest(
    Guid PatientUserId,
    Guid PrescriptionId,
    string PharmacyCode,
    string PharmacyName,
    string IdempotencyKey,
    object PrescriptionSnapshot);

public sealed record PharmacyShareAdapterResult(bool Accepted, string? ExternalShareId, string? ErrorCode, PharmacyShareStatus? NextStatus);

public sealed record PharmacyWebhookParse(bool SignatureValid, string EventId, string EventType, string? ExternalShareId, PharmacyShareStatus? Status);

public interface IPharmacyIntegrationAdapter
{
    string ProviderCode { get; }

    Task<IReadOnlyList<PharmacyDirectoryItem>> ListPharmaciesAsync(string? city, CancellationToken cancellationToken);

    Task<PharmacyShareAdapterResult> SharePrescriptionAsync(PharmacyShareRequest request, CancellationToken cancellationToken);

    PharmacyWebhookParse ParseWebhook(string rawBody, IReadOnlyDictionary<string, string> headers);
}

public sealed record AbdmReadinessResult(
    bool OfficialOnboardingComplete,
    bool SandboxAvailable,
    string Message,
    IReadOnlyList<string> RequiredApprovals);

public interface IAbdmIntegrationAdapter
{
    string ProviderCode { get; }

    Task<AbdmReadinessResult> GetReadinessAsync(CancellationToken cancellationToken);

    Task<(bool Simulated, string? SandboxHandle, string Message)> SimulateSandboxLinkAsync(Guid userId, string? preferredHandle, CancellationToken cancellationToken);
}
