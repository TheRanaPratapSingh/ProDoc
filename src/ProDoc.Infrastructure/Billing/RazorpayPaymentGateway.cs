using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ProDoc.Application.Billing;
using ProDoc.Domain.Common;

namespace ProDoc.Infrastructure.Billing;

public sealed class RazorpayPaymentGateway(IHttpClientFactory httpClientFactory, IOptions<PaymentGatewayOptions> options) : IPaymentGateway
{
    public string ProviderName => "Razorpay";

    public async Task<GatewayOrderResult> CreateOrderAsync(GatewayOrderRequest request, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var client = CreateClient();
        var paise = (int)decimal.Round(request.AmountInr * 100m, 0, MidpointRounding.AwayFromZero);
        using var content = new StringContent(JsonSerializer.Serialize(new
        {
            amount = paise,
            currency = request.Currency,
            receipt = request.Receipt,
            notes = new { note = request.Notes },
            payment_capture = 1
        }), Encoding.UTF8, "application/json");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.razorpay.com/v1/orders") { Content = content };
        httpRequest.Headers.TryAddWithoutValidation("X-Razorpay-Idempotency-Key", request.IdempotencyKey);
        using var response = await client.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw DomainException.Conflict("payment_provider_error", "Could not create a Razorpay order.");
        }

        using var document = JsonDocument.Parse(body);
        var orderId = document.RootElement.GetProperty("id").GetString()
            ?? throw DomainException.Conflict("payment_provider_error", "Razorpay order id was missing.");
        var checkout = JsonSerializer.Serialize(new
        {
            key = options.Value.RazorpayKeyId,
            orderId,
            amount = paise,
            currency = request.Currency,
            name = "ProDoc",
            description = request.Notes
        });
        return new GatewayOrderResult(ProviderName, orderId, checkout);
    }

    public async Task<GatewayPaymentVerification> VerifyPaymentAsync(
        string providerOrderId,
        string? providerPaymentId,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(providerPaymentId))
        {
            return new GatewayPaymentVerification(false, null, "Provider payment id is required for Razorpay verification.");
        }

        var client = CreateClient();
        using var response = await client.GetAsync($"https://api.razorpay.com/v1/payments/{providerPaymentId}", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new GatewayPaymentVerification(false, null, "Razorpay could not verify that payment.");
        }

        using var document = JsonDocument.Parse(body);
        var status = document.RootElement.GetProperty("status").GetString();
        var orderId = document.RootElement.TryGetProperty("order_id", out var order) ? order.GetString() : null;
        if (!string.Equals(orderId, providerOrderId, StringComparison.Ordinal)
            || status is not ("captured" or "authorized"))
        {
            return new GatewayPaymentVerification(false, providerPaymentId, $"Payment status is {status}.");
        }

        return new GatewayPaymentVerification(true, providerPaymentId, null);
    }

    public async Task<GatewayRefundResult> RefundAsync(
        string providerPaymentId,
        decimal amountInr,
        string reason,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var client = CreateClient();
        var paise = (int)decimal.Round(amountInr * 100m, 0, MidpointRounding.AwayFromZero);
        using var content = new StringContent(JsonSerializer.Serialize(new { amount = paise, notes = new { reason } }), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync($"https://api.razorpay.com/v1/payments/{providerPaymentId}/refund", content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new GatewayRefundResult(false, null, "Failed");
        }

        using var document = JsonDocument.Parse(body);
        var id = document.RootElement.GetProperty("id").GetString();
        var status = document.RootElement.GetProperty("status").GetString() ?? "Processing";
        return new GatewayRefundResult(true, id, status);
    }

    public GatewayWebhookParse ParseWebhook(string rawBody, IReadOnlyDictionary<string, string> headers)
    {
        EnsureConfigured();
        if (!headers.TryGetValue("X-Razorpay-Signature", out var signature) && !headers.TryGetValue("x-razorpay-signature", out signature))
        {
            return new GatewayWebhookParse(false, Guid.NewGuid().ToString("N"), "invalid", null, null, false);
        }

        var secret = options.Value.RazorpayWebhookSecret ?? string.Empty;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computed = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
        if (!string.Equals(computed, signature, StringComparison.OrdinalIgnoreCase))
        {
            return new GatewayWebhookParse(false, Guid.NewGuid().ToString("N"), "invalid", null, null, false);
        }

        using var document = JsonDocument.Parse(rawBody);
        var eventType = document.RootElement.TryGetProperty("event", out var evt) ? evt.GetString() ?? "unknown" : "unknown";
        var eventId = document.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N");
        string? orderId = null;
        string? paymentId = null;
        if (document.RootElement.TryGetProperty("payload", out var payload)
            && payload.TryGetProperty("payment", out var payment)
            && payment.TryGetProperty("entity", out var entity))
        {
            orderId = entity.TryGetProperty("order_id", out var order) ? order.GetString() : null;
            paymentId = entity.TryGetProperty("id", out var pay) ? pay.GetString() : null;
        }

        var captured = eventType.Contains("captured", StringComparison.OrdinalIgnoreCase);
        return new GatewayWebhookParse(true, eventId, eventType, orderId, paymentId, captured);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(options.Value.RazorpayKeyId) || string.IsNullOrWhiteSpace(options.Value.RazorpayKeySecret))
        {
            throw DomainException.Conflict("payment_provider_unconfigured", "Razorpay keys are not configured.");
        }
    }

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient(nameof(RazorpayPaymentGateway));
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Value.RazorpayKeyId}:{options.Value.RazorpayKeySecret}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        return client;
    }
}
