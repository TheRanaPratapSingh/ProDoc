using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProDoc.Api.Tests;

[Collection("api")]
public sealed class EnterpriseFlowTests(ProDocApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Organization_membership_isolates_clinics_and_blocks_cross_org_access()
    {
        var owner = await SignInAsync("9000000131");
        var outsider = await SignInAsync("9000000132");
        await SendAsync(HttpMethod.Put, "/api/v1/account/profile", owner.AccessToken, JsonContent.Create(new
        {
            fullName = "Org Owner",
            preferredLanguage = "en"
        }));

        var created = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/organizations", owner.AccessToken, JsonContent.Create(new
        {
            name = "Metro Care Group",
            kind = "ClinicChain"
        })));
        var organizationId = created.GetProperty("id").GetGuid();
        owner = await SignInAsync("9000000131");

        var mine = await ReadArrayAsync(await SendAsync(HttpMethod.Get, "/api/v1/organizations", owner.AccessToken));
        Assert.Contains(mine, item => item.GetProperty("id").GetGuid() == organizationId);

        var clinic = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/clinics", owner.AccessToken));
        var clinicId = clinic.GetProperty("id").GetGuid();
        await SendAsync(HttpMethod.Put, $"/api/v1/clinics/{clinicId}", owner.AccessToken, JsonContent.Create(new
        {
            name = "Metro Clinic",
            type = "Clinic",
            phoneNumber = "9876501311",
            registrationNumber = "CLN-ORG",
            establishmentYear = 2019
        }));

        var linked = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/organizations/{organizationId}/clinics", owner.AccessToken, JsonContent.Create(new
        {
            clinicId
        })));
        Assert.Equal(clinicId, linked.GetProperty("clinicId").GetGuid());

        var forbiddenList = await SendAsync(HttpMethod.Get, $"/api/v1/organizations/{organizationId}/clinics", outsider.AccessToken);
        Assert.True(forbiddenList.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden);

        var otherOrg = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/organizations", outsider.AccessToken, JsonContent.Create(new
        {
            name = "Other Group",
            kind = "Hospital"
        })));
        outsider = await SignInAsync("9000000132");
        var crossLink = await SendAsync(HttpMethod.Post, $"/api/v1/organizations/{otherOrg.GetProperty("id").GetGuid()}/clinics", outsider.AccessToken, JsonContent.Create(new
        {
            clinicId
        }));
        Assert.True(crossLink.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.Conflict);

        await SendAsync(HttpMethod.Put, "/api/v1/account/profile", outsider.AccessToken, JsonContent.Create(new
        {
            fullName = "Invitee",
            preferredLanguage = "en"
        }));
        var invited = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/organizations/{organizationId}/members", owner.AccessToken, JsonContent.Create(new
        {
            phoneNumber = "9000000132",
            role = "OrganizationAdmin"
        })));
        Assert.Equal("OrganizationAdmin", invited.GetProperty("role").GetString());

        outsider = await SignInAsync("9000000132");
        var shared = await ReadArrayAsync(await SendAsync(HttpMethod.Get, $"/api/v1/organizations/{organizationId}/clinics", outsider.AccessToken));
        Assert.Contains(shared, item => item.GetProperty("clinicId").GetGuid() == clinicId);

        var subscription = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/v1/organizations/{organizationId}/subscription", owner.AccessToken));
        Assert.Equal("STARTER", subscription.GetProperty("planCode").GetString());
        Assert.Contains("commission", subscription.GetProperty("note").GetString()!, StringComparison.OrdinalIgnoreCase);

        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)).ToString("yyyy-MM-dd");
        var to = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var analytics = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/v1/organizations/{organizationId}/analytics?from={from}&to={to}", owner.AccessToken));
        Assert.True(analytics.GetProperty("appointmentVolume").GetInt32() >= 0);

        await ReadAsync(await SendAsync(HttpMethod.Put, $"/api/v1/organizations/{organizationId}/settings", owner.AccessToken, JsonContent.Create(new
        {
            key = "appointments.patient_change_hours",
            value = "3"
        })));
        var settings = await ReadArrayAsync(await SendAsync(HttpMethod.Get, $"/api/v1/organizations/{organizationId}/settings", owner.AccessToken));
        Assert.Contains(settings, item => item.GetProperty("key").GetString() == "appointments.patient_change_hours");
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
