using System.Text;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Partners;
using ProDoc.Domain.Partners;

namespace ProDoc.Infrastructure.Partners;

public sealed class PartnerWebhookSender(IHttpClientFactory httpClientFactory, IClock clock) : IPartnerWebhookSender
{
    public const string HttpClientName = "PartnerWebhooks";
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    public async Task<(bool Success, int? StatusCode, string? Error)> SendAsync(
        string targetUrl,
        string signingSecret,
        string eventType,
        Guid deliveryId,
        string idempotencyKey,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var unix = clock.UtcNow.ToUnixTimeSeconds();
            var signature = PartnerWebhookEndpoint.Sign(signingSecret, unix, payloadJson);
            using var client = httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, targetUrl)
            {
                Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("X-ProDoc-Signature", signature);
            request.Headers.TryAddWithoutValidation("X-ProDoc-Timestamp", unix.ToString());
            request.Headers.TryAddWithoutValidation("X-ProDoc-Event", eventType);
            request.Headers.TryAddWithoutValidation("X-ProDoc-Delivery", deliveryId.ToString("N"));
            request.Headers.TryAddWithoutValidation("X-ProDoc-Idempotency-Key", idempotencyKey);

            // Link host stop with a per-attempt timeout so shutdown cancels cleanly while
            // slow endpoints surface as retryable timeouts (not silent success).
            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            attemptCts.CancelAfter(RequestTimeout);

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, attemptCts.Token);
            var code = (int)response.StatusCode;
            return response.IsSuccessStatusCode
                ? (true, code, null)
                : (false, code, $"HTTP {code}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Host is stopping — let the background service exit without treating this as a delivery failure.
            throw;
        }
        catch (OperationCanceledException)
        {
            return (false, null, "Webhook request timed out.");
        }
        catch (HttpRequestException ex)
        {
            return (false, null, TrimError(ex.Message));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, null, TrimError(ex.Message));
        }
    }

    private static string TrimError(string value)
    {
        var text = value.Trim();
        return text.Length <= 500 ? text : text[..500];
    }
}
