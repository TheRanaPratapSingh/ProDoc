using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProDoc.Api.Tests;

/// <summary>
/// Shared helpers for integration tests after clinic/doctor create became Super-Admin-only.
/// </summary>
internal static class ProviderSeed
{
    public static async Task EnsureDoctorRoleAsync(
        HttpClient client,
        string doctorAccessToken,
        string adminAccessToken)
    {
        var account = await ReadOkAsync(client, doctorAccessToken, HttpMethod.Get, "/api/v1/account/me");
        var userId = account.TryGetProperty("userId", out var camel)
            ? camel.GetGuid()
            : account.GetProperty("UserId").GetGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{userId}/roles/Doctor");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAccessToken);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Conflict,
            $"grant Doctor role failed {(int)response.StatusCode}: {body}");
    }

    public static async Task<Guid> CreateClinicOwnedByAsync(
        HttpClient client,
        string adminAccessToken,
        Guid ownerUserId)
    {
        var clinic = await ReadOkAsync(
            client,
            adminAccessToken,
            HttpMethod.Post,
            "/api/v1/clinics",
            JsonContent.Create(new { ownerUserId }));
        return clinic.GetProperty("id").GetGuid();
    }

    public static async Task<Guid> GetUserIdAsync(HttpClient client, string accessToken)
    {
        var account = await ReadOkAsync(client, accessToken, HttpMethod.Get, "/api/v1/account/me");
        return account.TryGetProperty("userId", out var camel)
            ? camel.GetGuid()
            : account.GetProperty("UserId").GetGuid();
    }

    private static async Task<JsonElement> ReadOkAsync(
        HttpClient client,
        string accessToken,
        HttpMethod method,
        string path,
        HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }
}
