namespace ProDoc.Application.Billing;

public sealed class PaymentGatewayOptions
{
    public const string SectionName = "Payments";

    public string Provider { get; set; } = "Development";

    public string? RazorpayKeyId { get; set; }

    public string? RazorpayKeySecret { get; set; }

    public string? RazorpayWebhookSecret { get; set; }

    public decimal EstimatedGatewayFeePercent { get; set; } = 2.0m;
}

public sealed record GatewayOrderRequest(
    string Receipt,
    decimal AmountInr,
    string Currency,
    string Notes,
    string IdempotencyKey);

public sealed record GatewayOrderResult(
    string Provider,
    string OrderId,
    string CheckoutPayloadJson);

public sealed record GatewayPaymentVerification(
    bool Paid,
    string? ProviderPaymentId,
    string? FailureReason);

public sealed record GatewayRefundResult(bool Accepted, string? ProviderRefundId, string Status);

public sealed record GatewayWebhookParse(
    bool SignatureValid,
    string EventId,
    string EventType,
    string? ProviderOrderId,
    string? ProviderPaymentId,
    bool PaymentCaptured);

public interface IPaymentGateway
{
    string ProviderName { get; }

    Task<GatewayOrderResult> CreateOrderAsync(GatewayOrderRequest request, CancellationToken cancellationToken);

    Task<GatewayPaymentVerification> VerifyPaymentAsync(string providerOrderId, string? providerPaymentId, CancellationToken cancellationToken);

    Task<GatewayRefundResult> RefundAsync(string providerPaymentId, decimal amountInr, string reason, CancellationToken cancellationToken);

    GatewayWebhookParse ParseWebhook(string rawBody, IReadOnlyDictionary<string, string> headers);
}
