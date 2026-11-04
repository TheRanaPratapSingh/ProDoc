using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProDoc.Api.Tests;

[Collection("api")]
public sealed class MobileFlowTests(ProDocApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Push_device_registration_and_notification_deep_links_are_available()
    {
        var patient = await SignInAsync("9000000151");
        var registered = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/devices/push", patient.AccessToken, JsonContent.Create(new
        {
            platform = "android",
            token = "ExponentPushToken[sandbox-mobile-flow-test-device-001]",
            appVersion = "1.0.0"
        })));
        Assert.Equal("android", registered.GetProperty("platform").GetString());
        Assert.True(registered.GetProperty("isActive").GetBoolean());

        var again = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/devices/push", patient.AccessToken, JsonContent.Create(new
        {
            platform = "android",
            token = "ExponentPushToken[sandbox-mobile-flow-test-device-001]",
            appVersion = "1.0.1"
        })));
        Assert.Equal(registered.GetProperty("id").GetGuid(), again.GetProperty("id").GetGuid());

        var notifications = await SendAsync(HttpMethod.Get, "/api/v1/patients/me/notifications", patient.AccessToken);
        Assert.Equal(HttpStatusCode.OK, notifications.StatusCode);

        var removed = await SendAsync(HttpMethod.Delete, "/api/v1/devices/push", patient.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
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
            deviceName = "mobile-test"
        });
        var verifyBody = await verifyResponse.Content.ReadAsStringAsync();
        Assert.True(verifyResponse.IsSuccessStatusCode, verifyBody);
        return JsonSerializer.Deserialize<SessionBody>(verifyBody, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string accessToken, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private sealed record OtpBody(string? DevelopmentCode);

    private sealed record SessionBody(string AccessToken, string RefreshToken);
}
