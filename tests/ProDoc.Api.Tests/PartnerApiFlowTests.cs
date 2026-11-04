using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ProDoc.Application.Partners;
using ProDoc.Domain.Partners;

namespace ProDoc.Api.Tests;

[Collection("api")]
public sealed class PartnerApiFlowTests(ProDocApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Partner_registration_oauth_scopes_sandbox_booking_and_webhook_security()
    {
        var developer = await SignInAsync("9000000192");
        var admin = await SignInAsync("9000000001");

        var deniedAdmin = await SendAsync(HttpMethod.Get, "/api/v1/admin/partners", developer.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, deniedAdmin.StatusCode);

        var registered = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/partners/register", developer.AccessToken, JsonContent.Create(new
        {
            name = "Acme Health Integrator",
            contactEmail = "partners@acme.test",
            website = "https://acme.test"
        })));
        var partnerOrgId = registered.GetProperty("id").GetGuid();
        Assert.Equal("Pending", registered.GetProperty("status").GetString());

        var app = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/partners/{partnerOrgId}/apps", developer.AccessToken, JsonContent.Create(new
        {
            name = "Acme Sandbox",
            environment = "Sandbox",
            scopes = new[]
            {
                PartnerScopes.DiscoveryRead,
                PartnerScopes.AvailabilityRead,
                PartnerScopes.AppointmentsRead,
                PartnerScopes.AppointmentsWrite,
                PartnerScopes.WebhooksManage,
                PartnerScopes.LabsRead
            }
        })));
        var appId = app.GetProperty("id").GetGuid();
        var clientId = app.GetProperty("clientId").GetString()!;
        Assert.StartsWith("pd_sandbox_", clientId);

        var issued = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/partners/apps/{appId}/credentials", developer.AccessToken));
        var clientSecret = issued.GetProperty("clientSecret").GetString()!;
        Assert.StartsWith("psk_", clientSecret);

        // Unauthorized partner API without token.
        var unauth = await _client.GetAsync("/api/partner/v1/doctors");
        Assert.Equal(HttpStatusCode.Unauthorized, unauth.StatusCode);

        // User JWT must not access partner API audience.
        var userJwtOnPartner = await SendAsync(HttpMethod.Get, "/api/partner/v1/doctors", developer.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, userJwtOnPartner.StatusCode);

        var tokenResponse = await _client.PostAsJsonAsync("/oauth/partner/token", new
        {
            grant_type = "client_credentials",
            client_id = clientId,
            client_secret = clientSecret,
            scope = $"{PartnerScopes.DiscoveryRead} {PartnerScopes.AppointmentsWrite} {PartnerScopes.AppointmentsRead}"
        });
        var tokenBody = await tokenResponse.Content.ReadAsStringAsync();
        Assert.True(tokenResponse.IsSuccessStatusCode, tokenBody);
        var token = JsonSerializer.Deserialize<JsonElement>(tokenBody);
        var accessToken = token.GetProperty("access_token").GetString()!;

        // Scope enforcement: labs.read not granted on this token.
        var labsDenied = await SendPartnerAsync(HttpMethod.Get, "/api/partner/v1/labs/catalog", accessToken);
        Assert.Equal(HttpStatusCode.Forbidden, labsDenied.StatusCode);

        var doctors = await ReadArrayAsync(await SendPartnerAsync(HttpMethod.Get, "/api/partner/v1/doctors", accessToken));
        Assert.NotNull(doctors);

        // Narrow token without appointments.write cannot book.
        var readOnlyTokenResponse = await _client.PostAsJsonAsync("/oauth/partner/token", new
        {
            grant_type = "client_credentials",
            client_id = clientId,
            client_secret = clientSecret,
            scope = PartnerScopes.DiscoveryRead
        });
        var readOnlyToken = JsonSerializer.Deserialize<JsonElement>(await readOnlyTokenResponse.Content.ReadAsStringAsync())
            .GetProperty("access_token").GetString()!;
        var writeDenied = await SendPartnerAsync(HttpMethod.Post, "/api/partner/v1/appointments", readOnlyToken, JsonContent.Create(new
        {
            doctorId = Guid.NewGuid(),
            startsAtUtc = DateTimeOffset.UtcNow.AddDays(1),
            endsAtUtc = DateTimeOffset.UtcNow.AddDays(1).AddMinutes(20),
            externalPatientRef = "ext-1",
            patientDisplayName = "Sandbox Patient"
        }));
        Assert.Equal(HttpStatusCode.Forbidden, writeDenied.StatusCode);

        var doctorId = doctors.Length > 0
            ? doctors[0].GetProperty("doctorId").GetGuid()
            : Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        var booking = await ReadAsync(await SendPartnerAsync(HttpMethod.Post, "/api/partner/v1/appointments", accessToken, JsonContent.Create(new
        {
            doctorId,
            startsAtUtc = DateTimeOffset.UtcNow.AddDays(2),
            endsAtUtc = DateTimeOffset.UtcNow.AddDays(2).AddMinutes(30),
            externalPatientRef = "ext-patient-42",
            patientDisplayName = "Sandbox Patient",
            reason = "API demo",
            idempotencyKey = "partner-book-1"
        })));
        Assert.True(booking.GetProperty("isSandbox").GetBoolean());
        var bookingId = booking.GetProperty("id").GetGuid();

        var duplicate = await ReadAsync(await SendPartnerAsync(HttpMethod.Post, "/api/partner/v1/appointments", accessToken, JsonContent.Create(new
        {
            doctorId,
            startsAtUtc = DateTimeOffset.UtcNow.AddDays(2),
            endsAtUtc = DateTimeOffset.UtcNow.AddDays(2).AddMinutes(30),
            externalPatientRef = "ext-patient-42",
            patientDisplayName = "Sandbox Patient",
            idempotencyKey = "partner-book-1"
        })));
        Assert.Equal(bookingId, duplicate.GetProperty("id").GetGuid());

        var webhook = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/partners/apps/{appId}/webhooks", developer.AccessToken, JsonContent.Create(new
        {
            targetUrl = "http://127.0.0.1:9/partner-hook",
            eventTypes = new[] { PartnerWebhookEvents.AppointmentCreated, PartnerWebhookEvents.AppointmentCancelled }
        })));
        var signingSecret = webhook.GetProperty("signingSecret").GetString()!;
        Assert.StartsWith("whsec_", signingSecret);
        // Secret must not appear in list payload.
        var listed = await ReadArrayAsync(await SendAsync(HttpMethod.Get, $"/api/v1/partners/apps/{appId}/webhooks", developer.AccessToken));
        Assert.DoesNotContain(listed, item => item.ToString().Contains(signingSecret, StringComparison.Ordinal));

        // Signature verification + replay protection.
        var body = """{"ok":true}""";
        var now = DateTimeOffset.UtcNow;
        var unix = now.ToUnixTimeSeconds();
        var good = PartnerWebhookEndpoint.Sign(signingSecret, unix, body);
        Assert.True(PartnerService.VerifyWebhookSignature(signingSecret, unix, body, good, now));
        Assert.False(PartnerService.VerifyWebhookSignature(signingSecret, unix - 600, body, PartnerWebhookEndpoint.Sign(signingSecret, unix - 600, body), now));
        Assert.False(PartnerService.VerifyWebhookSignature(signingSecret, unix, body, "deadbeef", now));

        // Credential rotation / revocation.
        await SendAsync(HttpMethod.Post, $"/api/v1/partners/apps/{appId}/credentials/{issued.GetProperty("credentialId").GetGuid()}/revoke", developer.AccessToken);
        var revoked = await _client.PostAsJsonAsync("/oauth/partner/token", new
        {
            grant_type = "client_credentials",
            client_id = clientId,
            client_secret = clientSecret
        });
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);

        var rotated = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/partners/apps/{appId}/credentials", developer.AccessToken));
        var newSecret = rotated.GetProperty("clientSecret").GetString()!;
        var freshToken = await _client.PostAsJsonAsync("/oauth/partner/token", new
        {
            grant_type = "client_credentials",
            client_id = clientId,
            client_secret = newSecret,
            scope = PartnerScopes.AppointmentsRead
        });
        Assert.True(freshToken.IsSuccessStatusCode);

        // Super Admin partner management.
        var approved = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/admin/partners/{partnerOrgId}/decision", admin.AccessToken, JsonContent.Create(new
        {
            decision = "approve",
            note = "Integration review passed"
        })));
        Assert.Equal("Approved", approved.GetProperty("status").GetString());

        var stats = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/admin/partners/stats", admin.AccessToken));
        Assert.True(stats.GetProperty("approvedPartners").GetInt32() >= 1);

        var configured = await ReadAsync(await SendAsync(HttpMethod.Put, $"/api/v1/admin/partners/apps/{appId}", admin.AccessToken, JsonContent.Create(new
        {
            scopes = new[] { PartnerScopes.DiscoveryRead, PartnerScopes.AppointmentsRead },
            rateLimitPerMinute = 30,
            dailyQuota = 500,
            isActive = true
        })));
        Assert.Equal(30, configured.GetProperty("rateLimitPerMinute").GetInt32());

        var usage = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/v1/partners/apps/{appId}/usage", developer.AccessToken));
        Assert.True(usage.GetProperty("todayRequests").GetInt32() >= 1);

        var docs = await _client.GetAsync("/api/partner/v1/docs");
        Assert.True(docs.IsSuccessStatusCode);

        // Cancel booking with fresh appointments.write token after admin narrowed scopes — recreate credential scopes via admin restore for write.
        await SendAsync(HttpMethod.Put, $"/api/v1/admin/partners/apps/{appId}", admin.AccessToken, JsonContent.Create(new
        {
            scopes = new[]
            {
                PartnerScopes.DiscoveryRead,
                PartnerScopes.AppointmentsRead,
                PartnerScopes.AppointmentsWrite,
                PartnerScopes.WebhooksManage
            },
            rateLimitPerMinute = 60,
            dailyQuota = 5000,
            isActive = true
        }));
        var writeTokenJson = JsonSerializer.Deserialize<JsonElement>(await (await _client.PostAsJsonAsync("/oauth/partner/token", new
        {
            grant_type = "client_credentials",
            client_id = clientId,
            client_secret = newSecret,
            scope = $"{PartnerScopes.AppointmentsWrite} {PartnerScopes.AppointmentsRead}"
        })).Content.ReadAsStringAsync());
        var writeToken = writeTokenJson.GetProperty("access_token").GetString()!;
        var cancelled = await ReadAsync(await SendPartnerAsync(HttpMethod.Post, $"/api/partner/v1/appointments/{bookingId}/cancel", writeToken));
        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());

        // Prescription exchange requires consent attestation.
        var rx = await SendPartnerAsync(HttpMethod.Get, $"/api/partner/v1/prescriptions/{Guid.NewGuid()}?patientConsentAttested=false", writeToken);
        Assert.True(rx.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);

        await using var scope = factory.Services.CreateAsyncScope();
        var partners = scope.ServiceProvider.GetRequiredService<IPartnerService>();
        await partners.ProcessWebhookDeliveriesAsync(CancellationToken.None);
    }

    [Fact]
    public void Webhook_hmac_is_deterministic_and_case_insensitive_hex()
    {
        var secret = "whsec_test";
        var body = "{\"a\":1}";
        var sig = PartnerWebhookEndpoint.Sign(secret, 1_700_000_000, body);
        Assert.Equal(64, sig.Length);
        Assert.True(PartnerWebhookEndpoint.Verify(secret, 1_700_000_000, body, sig.ToUpperInvariant(), DateTimeOffset.FromUnixTimeSeconds(1_700_000_000), TimeSpan.FromMinutes(5)));
    }

    private async Task<SessionBody> SignInAsync(string phone)
    {
        var otpResponse = await _client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phoneNumber = phone });
        var otpBody = await otpResponse.Content.ReadAsStringAsync();
        Assert.True(otpResponse.IsSuccessStatusCode, otpBody);
        var otp = JsonSerializer.Deserialize<OtpBody>(otpBody, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(otp?.DevelopmentCode);
        var verify = await _client.PostAsJsonAsync("/api/v1/auth/otp/verify", new
        {
            phoneNumber = phone,
            code = otp!.DevelopmentCode,
            deviceName = "partner-test"
        });
        var verifyBody = await verify.Content.ReadAsStringAsync();
        Assert.True(verify.IsSuccessStatusCode, verifyBody);
        var session = JsonSerializer.Deserialize<SessionBody>(verifyBody, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(session?.AccessToken);
        return session!;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string accessToken, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendPartnerAsync(HttpMethod method, string url, string accessToken, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private static async Task<JsonElement[]> ReadArrayAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<JsonElement[]>(body) ?? [];
    }

    private sealed record OtpBody(string? DevelopmentCode);

    private sealed record SessionBody(string AccessToken);
}
