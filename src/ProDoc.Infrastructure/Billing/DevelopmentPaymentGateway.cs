using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ProDoc.Application.Billing;

namespace ProDoc.Infrastructure.Billing;

public sealed class DevelopmentPaymentGateway : IPaymentGateway
{
    public string ProviderName => "Development";

    public Task<GatewayOrderResult> CreateOrderAsync(GatewayOrderRequest request, CancellationToken cancellationToken)
    {
        var orderId = $"order_dev_{Guid.NewGuid():N}";
        var payload = JsonSerializer.Serialize(new
        {
            provider = ProviderName,
            orderId,
            amount = request.AmountInr,
            currency = request.Currency,
            receipt = request.Receipt,
            checkoutMode = "development-simulate"
        });
        return Task.FromResult(new GatewayOrderResult(ProviderName, orderId, payload));
    }

    public Task<GatewayPaymentVerification> VerifyPaymentAsync(
        string providerOrderId,
        string? providerPaymentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providerOrderId))
        {
            return Task.FromResult(new GatewayPaymentVerification(false, null, "Missing provider order."));
        }

        if (string.Equals(providerPaymentId, "fail", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new GatewayPaymentVerification(false, null, "Simulated payment failure."));
        }

        var paymentId = string.IsNullOrWhiteSpace(providerPaymentId)
            ? $"pay_dev_{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(providerOrderId)))[..16].ToLowerInvariant()}"
            : providerPaymentId;
        return Task.FromResult(new GatewayPaymentVerification(true, paymentId, null));
    }

    public Task<GatewayRefundResult> RefundAsync(
        string providerPaymentId,
        decimal amountInr,
        string reason,
        CancellationToken cancellationToken)
    {
        _ = amountInr;
        _ = reason;
        return Task.FromResult(new GatewayRefundResult(true, $"rfnd_dev_{providerPaymentId}", "Completed"));
    }

    public GatewayWebhookParse ParseWebhook(string rawBody, IReadOnlyDictionary<string, string> headers)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(rawBody) ? "{}" : rawBody);
        var root = document.RootElement;
        var eventId = root.TryGetProperty("eventId", out var id) ? id.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N");
        var eventType = root.TryGetProperty("eventType", out var type) ? type.GetString() ?? "payment.captured" : "payment.captured";
        var orderId = root.TryGetProperty("orderId", out var order) ? order.GetString() : null;
        var paymentId = root.TryGetProperty("paymentId", out var pay) ? pay.GetString() : null;
        var captured = !root.TryGetProperty("captured", out var cap) || cap.GetBoolean();
        _ = headers;
        return new GatewayWebhookParse(true, eventId, eventType, orderId, paymentId, captured);
    }
}
