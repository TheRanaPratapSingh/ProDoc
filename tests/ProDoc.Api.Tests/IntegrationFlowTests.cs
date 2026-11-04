using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ProDoc.Api.Tests;

[Collection("api")]
public sealed class IntegrationFlowTests(ProDocApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Consent_fhir_abdm_lab_webhooks_and_admin_config_are_sandbox_safe()
    {
        var patient = await SignInAsync("9000000141");
        var admin = await SignInAsync("9000000001");
        await SendAsync(HttpMethod.Put, "/api/v1/account/profile", patient.AccessToken, JsonContent.Create(new
        {
            fullName = "Integration Patient",
            preferredLanguage = "en"
        }));

        var blocked = await SendAsync(HttpMethod.Post, "/api/v1/integrations/fhir/export", patient.AccessToken, JsonContent.Create(new { }));
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);

        await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/integrations/consents", patient.AccessToken, JsonContent.Create(new
        {
            providerKind = "FhirExchange",
            purpose = "fhir.export"
        })));
        await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/integrations/consents", patient.AccessToken, JsonContent.Create(new
        {
            providerKind = "Laboratory",
            purpose = "lab.orders"
        })));
        await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/integrations/consents", patient.AccessToken, JsonContent.Create(new
        {
            providerKind = "Abdm",
            purpose = "abdm.exchange"
        })));

        var fhir = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/integrations/fhir/export", patient.AccessToken, JsonContent.Create(new { })));
        Assert.Equal("Bundle", fhir.GetProperty("resourceType").GetString());
        Assert.True(fhir.GetProperty("entry").GetArrayLength() >= 1);
        Assert.Contains("not a certified", fhir.GetProperty("disclaimer").GetString()!, StringComparison.OrdinalIgnoreCase);

        await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/integrations/consents/revoke", patient.AccessToken, JsonContent.Create(new
        {
            providerKind = "FhirExchange",
            purpose = "fhir.export"
        })));
        var afterRevoke = await SendAsync(HttpMethod.Post, "/api/v1/integrations/fhir/export", patient.AccessToken, JsonContent.Create(new { }));
        Assert.Equal(HttpStatusCode.BadRequest, afterRevoke.StatusCode);

        var readiness = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/integrations/abdm/readiness", patient.AccessToken));
        Assert.False(readiness.GetProperty("officialOnboardingComplete").GetBoolean());
        Assert.True(readiness.GetProperty("sandboxAvailable").GetBoolean());

        var abha = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/integrations/abha/sandbox-link", patient.AccessToken, JsonContent.Create(new
        {
            preferredHandle = "sandbox.patient@sbx"
        })));
        Assert.Equal("SandboxSimulated", abha.GetProperty("status").GetString());
        Assert.False(abha.GetProperty("productionConnected").GetBoolean());
        Assert.Contains("not an authorized", abha.GetProperty("notes").GetString()!, StringComparison.OrdinalIgnoreCase);

        var catalog = await ReadArrayAsync(await SendAsync(HttpMethod.Get, "/api/v1/integrations/labs/catalog?providerCode=lab-sandbox", patient.AccessToken));
        Assert.Contains(catalog, item => item.GetProperty("code").GetString() == "CBC");

        var order = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/integrations/labs/orders", patient.AccessToken, JsonContent.Create(new
        {
            providerCode = "lab-sandbox",
            catalogCode = "CBC",
            idempotencyKey = "lab-cbc-001"
        })));
        Assert.Equal("Requested", order.GetProperty("status").GetString());
        Assert.True(order.GetProperty("resultsArePreliminary").GetBoolean());
        var externalOrderId = order.GetProperty("externalOrderId").GetString()!;

        var duplicate = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/integrations/labs/orders", patient.AccessToken, JsonContent.Create(new
        {
            providerCode = "lab-sandbox",
            catalogCode = "CBC",
            idempotencyKey = "lab-cbc-001"
        })));
        Assert.Equal(order.GetProperty("id").GetGuid(), duplicate.GetProperty("id").GetGuid());

        var payload = JsonSerializer.Serialize(new
        {
            eventId = "lab-evt-1",
            eventType = "lab.reported",
            externalOrderId,
            status = "Reported",
            reportSummary = "CBC within synthetic reference ranges",
            report = new { haemoglobin = "13.2", units = "g/dL" },
            preliminary = true
        });
        var signature = Sign(payload);
        var webhook = await SendWebhookAsync("/api/v1/integrations/labs/webhooks/lab-sandbox", payload, signature);
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        var duplicateWebhook = await SendWebhookAsync("/api/v1/integrations/labs/webhooks/lab-sandbox", payload, signature);
        Assert.Equal(HttpStatusCode.OK, duplicateWebhook.StatusCode);

        var badSig = await SendWebhookAsync("/api/v1/integrations/labs/webhooks/lab-sandbox", payload, "deadbeef");
        Assert.Equal(HttpStatusCode.Unauthorized, badSig.StatusCode);

        var reports = await ReadArrayAsync(await SendAsync(HttpMethod.Get, "/api/v1/integrations/labs/reports", patient.AccessToken));
        Assert.Contains(reports, item => item.GetProperty("isPreliminary").GetBoolean()
            && item.GetProperty("disclaimer").GetString()!.Contains("Not confirmed", StringComparison.OrdinalIgnoreCase));

        var pharmacies = await ReadArrayAsync(await SendAsync(HttpMethod.Get, "/api/v1/integrations/pharmacies?providerCode=pharmacy-sandbox", patient.AccessToken));
        Assert.NotEmpty(pharmacies);

        var providers = await ReadArrayAsync(await SendAsync(HttpMethod.Get, "/api/v1/integrations/providers", admin.AccessToken));
        var labProvider = providers.Single(item => item.GetProperty("code").GetString() == "lab-sandbox");
        var configured = await ReadAsync(await SendAsync(HttpMethod.Put, $"/api/v1/admin/integrations/providers/{labProvider.GetProperty("id").GetGuid()}", admin.AccessToken, JsonContent.Create(new
        {
            mode = "Sandbox",
            isActive = true,
            baseUrl = "https://sandbox.example.invalid/lab",
            credentialReference = "vault://integrations/lab-sandbox"
        })));
        Assert.Equal("Sandbox", configured.GetProperty("mode").GetString());
        Assert.Equal("vault://integrations/lab-sandbox", configured.GetProperty("credentialReference").GetString());

        var status = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/integrations/status", patient.AccessToken));
        Assert.False(status.GetProperty("abha").GetProperty("productionConnected").GetBoolean());
        Assert.Contains(status.GetProperty("providers").EnumerateArray(), item => item.GetProperty("code").GetString() == "abdm-sandbox");
    }

    private static string Sign(string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("prodoc-sandbox-webhook"));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private async Task<HttpResponseMessage> SendWebhookAsync(string path, string payload, string signature)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("X-ProDoc-Signature", signature);
        return await _client.SendAsync(request);
    }

    private async Task<SessionBody> SignInAsync(string phone)
    {
        var otpResponse = await _client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phoneNumber = phone });
        var otpBody = await otpResponse.Content.ReadAsStringAsync();
        Assert.True(otpResponse.IsSuccessStatusCode, otpBody);
        var otp = JsonSerializer.Deserialize<OtpBody>(otpBody, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var verifyResponse = await _client.PostAsJsonAsync("/api/v1/auth/otp/verify", new
        {
            phoneNumber = phone,
            code = otp!.DevelopmentCode,
            deviceName = "integration-test"
        });
        var verifyBody = await verifyResponse.Content.ReadAsStringAsync();
        Assert.True(verifyResponse.IsSuccessStatusCode, verifyBody);
        return JsonSerializer.Deserialize<SessionBody>(verifyBody, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string accessToken, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request);
        if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent or HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.Unauthorized)
        {
            return response;
        }

        Assert.Fail($"{method} {path} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return response;
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static async Task<JsonElement[]> ReadArrayAsync(HttpResponseMessage response)
    {
        var root = await ReadAsync(response);
        return root.EnumerateArray().Select(item => item.Clone()).ToArray();
    }

    private sealed record OtpBody(string? DevelopmentCode);

    private sealed record SessionBody(string AccessToken, string RefreshToken);
}
