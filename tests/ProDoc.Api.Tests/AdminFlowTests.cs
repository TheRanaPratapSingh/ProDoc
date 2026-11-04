using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProDoc.Api.Tests;

[Collection("api")]
public sealed class AdminFlowTests(ProDocApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Admin_dashboard_users_support_config_and_suspension_are_permission_gated()
    {
        var admin = await SignInAsync("9000000001");
        var patient = await SignInAsync("9000000081");
        await SendAsync(HttpMethod.Put, "/api/v1/account/profile", patient.AccessToken, JsonContent.Create(new
        {
            fullName = "Neha Shah",
            preferredLanguage = "en"
        }));

        var denied = await SendAsync(HttpMethod.Get, "/api/v1/admin/dashboard", patient.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var dashboard = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/admin/dashboard", admin.AccessToken));
        Assert.True(dashboard.GetProperty("totalPatients").GetInt32() >= 1);
        Assert.True(dashboard.TryGetProperty("commissionRevenue", out _));
        Assert.True(dashboard.TryGetProperty("monthlyRevenue", out _));

        var users = await ReadArrayAsync(await SendAsync(HttpMethod.Get, "/api/v1/admin/users?q=9000000081", admin.AccessToken));
        Assert.Contains(users, item => item.GetProperty("phoneNumber").GetString()!.Contains("9000000081"));
        var targetUserId = users.Single(item => item.GetProperty("phoneNumber").GetString()!.Contains("9000000081")).GetProperty("userId").GetGuid();

        var ticket = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/support/tickets", patient.AccessToken, JsonContent.Create(new
        {
            category = "PaymentIssue",
            priority = "High",
            subject = "Payment stuck",
            description = "Online payment verified but visit still shows unpaid."
        })));
        var ticketId = ticket.GetProperty("id").GetGuid();
        Assert.Equal("Open", ticket.GetProperty("status").GetString());

        var mine = await ReadArrayAsync(await SendAsync(HttpMethod.Get, "/api/v1/support/tickets/mine", patient.AccessToken));
        Assert.Contains(mine, item => item.GetProperty("id").GetGuid() == ticketId);

        var patientTicket = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/v1/support/tickets/{ticketId}", patient.AccessToken));
        Assert.DoesNotContain(patientTicket.GetProperty("notes").EnumerateArray(), note => note.GetProperty("isInternal").GetBoolean());

        var updated = await ReadAsync(await SendAsync(HttpMethod.Put, $"/api/v1/admin/support/tickets/{ticketId}", admin.AccessToken, JsonContent.Create(new
        {
            status = "InProgress",
            note = "Checking gateway capture.",
            internalNote = true
        })));
        Assert.Equal("InProgress", updated.GetProperty("status").GetString());
        Assert.Contains(updated.GetProperty("notes").EnumerateArray(), note => note.GetProperty("isInternal").GetBoolean());

        var patientAfterInternal = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/v1/support/tickets/{ticketId}", patient.AccessToken));
        Assert.DoesNotContain(patientAfterInternal.GetProperty("notes").EnumerateArray(), note => note.GetProperty("isInternal").GetBoolean());

        var settings = await ReadArrayAsync(await SendAsync(HttpMethod.Get, "/api/v1/admin/settings", admin.AccessToken));
        Assert.NotEmpty(settings);
        var key = settings[0].GetProperty("key").GetString()!;
        var saved = await ReadAsync(await SendAsync(HttpMethod.Put, $"/api/v1/admin/settings/{key}", admin.AccessToken, JsonContent.Create(new
        {
            value = settings[0].GetProperty("value").GetString(),
            description = settings[0].GetProperty("description").GetString()
        })));
        Assert.Equal(key, saved.GetProperty("key").GetString());

        var flags = await ReadArrayAsync(await SendAsync(HttpMethod.Get, "/api/v1/admin/feature-flags", admin.AccessToken));
        Assert.NotEmpty(flags);

        var patientDeniedConfig = await SendAsync(HttpMethod.Get, "/api/v1/admin/settings", patient.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, patientDeniedConfig.StatusCode);

        var suspend = await SendAsync(HttpMethod.Post, $"/api/v1/admin/users/{targetUserId}/suspend", admin.AccessToken, JsonContent.Create(new
        {
            reason = "Abuse report verified"
        }));
        Assert.Equal(HttpStatusCode.NoContent, suspend.StatusCode);

        var suspendedSignIn = await _client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phoneNumber = "9000000081" });
        Assert.True(suspendedSignIn.IsSuccessStatusCode);
        var otpBody = JsonSerializer.Deserialize<OtpBody>(await suspendedSignIn.Content.ReadAsStringAsync(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var verifySuspended = await _client.PostAsJsonAsync("/api/v1/auth/otp/verify", new
        {
            phoneNumber = "9000000081",
            code = otpBody!.DevelopmentCode,
            deviceName = "integration-test"
        });
        Assert.Equal(HttpStatusCode.BadRequest, verifySuspended.StatusCode);

        var reactivate = await SendAsync(HttpMethod.Post, $"/api/v1/admin/users/{targetUserId}/reactivate", admin.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, reactivate.StatusCode);

        var audit = await ReadArrayAsync(await SendAsync(HttpMethod.Get, "/api/v1/admin/audit?action=admin.account", admin.AccessToken));
        Assert.Contains(audit, item => item.GetProperty("action").GetString()!.Contains("suspended", StringComparison.OrdinalIgnoreCase)
            || item.GetProperty("action").GetString()!.Contains("reactivated", StringComparison.OrdinalIgnoreCase));

        var providers = await ReadArrayAsync(await SendAsync(HttpMethod.Get, "/api/v1/admin/providers?kind=doctor", admin.AccessToken));
        Assert.NotNull(providers);

        var financeReport = await SendAsync(HttpMethod.Get, "/api/v1/admin/finance/report", admin.AccessToken);
        Assert.Equal(HttpStatusCode.OK, financeReport.StatusCode);

        var csv = await SendAsync(HttpMethod.Get, "/api/v1/admin/finance/export.csv", admin.AccessToken);
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
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
        if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent or HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
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
