using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProDoc.Application.Abstractions;

namespace ProDoc.Infrastructure.Auth;

public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    /// <summary>Development | Http. Outside Development, Http is required with EndpointUrl.</summary>
    public string Provider { get; set; } = "Development";

    /// <summary>POST endpoint that accepts JSON { phoneE164, message, purpose }.</summary>
    public string? EndpointUrl { get; set; }

    public string? ApiKeyHeaderName { get; set; } = "X-Api-Key";

    public string? ApiKey { get; set; }

    public string MessageTemplate { get; set; } = "ProDoc login code: {code}. Valid for a few minutes. Do not share.";
}

public sealed class HttpSmsOtpSender(
    IHttpClientFactory httpClientFactory,
    IOptions<SmsOptions> options,
    ILogger<HttpSmsOtpSender> logger) : IOtpSender
{
    public async Task SendSignInCodeAsync(string phoneE164, string code, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.EndpointUrl))
        {
            throw new InvalidOperationException("Sms:EndpointUrl is required when Sms:Provider=Http.");
        }

        var message = settings.MessageTemplate.Replace("{code}", code, StringComparison.Ordinal);
        using var client = httpClientFactory.CreateClient("ProDocSms");
        using var request = new HttpRequestMessage(HttpMethod.Post, settings.EndpointUrl)
        {
            Content = JsonContent.Create(new
            {
                phoneE164,
                message,
                purpose = "sign_in_otp"
            })
        };
        if (!string.IsNullOrWhiteSpace(settings.ApiKey) && !string.IsNullOrWhiteSpace(settings.ApiKeyHeaderName))
        {
            request.Headers.TryAddWithoutValidation(settings.ApiKeyHeaderName, settings.ApiKey);
        }

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("SMS provider returned {StatusCode} for masked delivery failure.", (int)response.StatusCode);
            throw new InvalidOperationException($"SMS provider failed with HTTP {(int)response.StatusCode}. {Trim(body)}");
        }
    }

    private static string Trim(string value) =>
        value.Length <= 200 ? value : value[..200];
}
