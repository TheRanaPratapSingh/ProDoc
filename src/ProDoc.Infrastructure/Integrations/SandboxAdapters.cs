using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ProDoc.Application.Integrations;
using ProDoc.Domain.Integrations;

namespace ProDoc.Infrastructure.Integrations;

public sealed class IntegrationOptions
{
    public const string SectionName = "Integrations";

    /// <summary>
    /// Shared HMAC secret for sandbox webhook verification only. Never a production partner credential.
    /// </summary>
    public string SandboxWebhookSecret { get; set; } = "prodoc-sandbox-webhook";
}

public sealed class SandboxAbdmAdapter : IAbdmIntegrationAdapter
{
    public string ProviderCode => "abdm-sandbox";

    public Task<AbdmReadinessResult> GetReadinessAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new AbdmReadinessResult(
            OfficialOnboardingComplete: false,
            SandboxAvailable: true,
            Message: "ABDM sandbox readiness only. Official ABHA verification, HIP/HIU registration, and Consent Manager connectivity require NHA onboarding and are not active.",
            RequiredApprovals:
            [
                "Facility registration with ABDM / NHA",
                "HIP and/or HIU role approval",
                "Consent Manager integration credentials",
                "Production bridge certificates — never store plaintext secrets in ProDoc config"
            ]));

    public Task<(bool Simulated, string? SandboxHandle, string Message)> SimulateSandboxLinkAsync(
        Guid userId,
        string? preferredHandle,
        CancellationToken cancellationToken)
    {
        var handle = string.IsNullOrWhiteSpace(preferredHandle)
            ? $"sandbox.{userId:N}@sbx"
            : preferredHandle.Trim();
        return Task.FromResult<(bool, string?, string)>((
            true,
            handle,
            "Sandbox ABHA handle simulated locally. This is not an authorized ABHA verification."));
    }
}

public sealed class SandboxLabAdapter(IOptions<IntegrationOptions> options) : ILabIntegrationAdapter
{
    public string ProviderCode => "lab-sandbox";

    public Task<IReadOnlyList<LabCatalogItem>> ListCatalogAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<LabCatalogItem>>(
        [
            new("CBC", "Complete blood count", "Whole blood", "24h"),
            new("LFT", "Liver function panel", "Serum", "24h"),
            new("HBA1C", "Glycated haemoglobin", "Whole blood", "24h"),
            new("TSH", "Thyroid stimulating hormone", "Serum", "48h")
        ]);

    public Task<LabOrderAdapterResult> SubmitOrderAsync(LabOrderRequest request, CancellationToken cancellationToken)
    {
        var externalId = $"LAB-{request.IdempotencyKey.GetHashCode():X8}";
        return Task.FromResult(new LabOrderAdapterResult(true, externalId, null, LabOrderStatus.Requested));
    }

    public LabWebhookParse ParseWebhook(string rawBody, IReadOnlyDictionary<string, string> headers)
    {
        var valid = VerifySignature(rawBody, headers, options.Value.SandboxWebhookSecret);
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(rawBody) ? "{}" : rawBody);
        var root = document.RootElement;
        var eventId = root.TryGetProperty("eventId", out var eid) ? eid.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N");
        var eventType = root.TryGetProperty("eventType", out var et) ? et.GetString() ?? "lab.status" : "lab.status";
        var externalOrderId = root.TryGetProperty("externalOrderId", out var oid) ? oid.GetString() : null;
        LabOrderStatus? status = null;
        if (root.TryGetProperty("status", out var st) && Enum.TryParse<LabOrderStatus>(st.GetString(), true, out var parsed))
        {
            status = parsed;
        }

        var summary = root.TryGetProperty("reportSummary", out var sum) ? sum.GetString() : null;
        var reportJson = root.TryGetProperty("report", out var report) ? report.GetRawText() : null;
        var preliminary = !root.TryGetProperty("preliminary", out var prelim) || prelim.GetBoolean();
        return new LabWebhookParse(valid, eventId, eventType, externalOrderId, status, summary, reportJson, preliminary);
    }

    internal static bool VerifySignature(string rawBody, IReadOnlyDictionary<string, string> headers, string secret)
    {
        if (!headers.TryGetValue("X-ProDoc-Signature", out var signature)
            && !headers.TryGetValue("x-prodoc-signature", out signature))
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computed = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody)));
        return string.Equals(computed, signature, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class SandboxPharmacyAdapter(IOptions<IntegrationOptions> options) : IPharmacyIntegrationAdapter
{
    public string ProviderCode => "pharmacy-sandbox";

    public Task<IReadOnlyList<PharmacyDirectoryItem>> ListPharmaciesAsync(string? city, CancellationToken cancellationToken)
    {
        IReadOnlyList<PharmacyDirectoryItem> all =
        [
            new("PHARM-SBX-01", "Sandbox Care Pharmacy", "Bengaluru", true),
            new("PHARM-SBX-02", "Demo Meds Counter", "Mumbai", false),
            new("PHARM-SBX-03", "Synthetic Rx Hub", "Delhi", true)
        ];

        if (string.IsNullOrWhiteSpace(city))
        {
            return Task.FromResult(all);
        }

        var filtered = all
            .Where(item => item.City.Contains(city.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return Task.FromResult<IReadOnlyList<PharmacyDirectoryItem>>(filtered);
    }

    public Task<PharmacyShareAdapterResult> SharePrescriptionAsync(PharmacyShareRequest request, CancellationToken cancellationToken)
    {
        var externalId = $"RXS-{request.IdempotencyKey.GetHashCode():X8}";
        return Task.FromResult(new PharmacyShareAdapterResult(true, externalId, null, PharmacyShareStatus.Shared));
    }

    public PharmacyWebhookParse ParseWebhook(string rawBody, IReadOnlyDictionary<string, string> headers)
    {
        var valid = SandboxLabAdapter.VerifySignature(rawBody, headers, options.Value.SandboxWebhookSecret);
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(rawBody) ? "{}" : rawBody);
        var root = document.RootElement;
        var eventId = root.TryGetProperty("eventId", out var eid) ? eid.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N");
        var eventType = root.TryGetProperty("eventType", out var et) ? et.GetString() ?? "pharmacy.status" : "pharmacy.status";
        var externalShareId = root.TryGetProperty("externalShareId", out var sid) ? sid.GetString() : null;
        PharmacyShareStatus? status = null;
        if (root.TryGetProperty("status", out var st) && Enum.TryParse<PharmacyShareStatus>(st.GetString(), true, out var parsed))
        {
            status = parsed;
        }

        return new PharmacyWebhookParse(valid, eventId, eventType, externalShareId, status);
    }
}
