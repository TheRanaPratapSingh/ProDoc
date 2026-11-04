using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProDoc.Api.Tests;

[Collection("api")]
public sealed class AnalyticsFlowTests(ProDocApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Analytics_executive_operational_predictive_and_exports_are_permission_gated()
    {
        var admin = await SignInAsync("9000000001");
        var patient = await SignInAsync("9000000171");
        await SendAsync(HttpMethod.Put, "/api/v1/account/profile", patient.AccessToken, JsonContent.Create(new
        {
            fullName = "Analytics Patient",
            preferredLanguage = "en"
        }));

        var denied = await SendAsync(HttpMethod.Get, "/api/v1/analytics/executive", patient.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var to = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = to.AddDays(-30);
        var range = $"from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";

        var executive = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/v1/analytics/executive?{range}", admin.AccessToken));
        Assert.True(executive.GetProperty("totalPatients").GetInt32() >= 1);
        Assert.True(executive.TryGetProperty("commissionRevenue", out _));
        Assert.True(executive.TryGetProperty("completionRatePercent", out _));
        Assert.True(executive.TryGetProperty("patientRetentionPercent", out _));
        Assert.Contains("actual", executive.GetProperty("dataFreshnessNote").GetString()!, StringComparison.OrdinalIgnoreCase);

        var operational = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/v1/analytics/operational?{range}", admin.AccessToken));
        Assert.True(operational.TryGetProperty("slotUtilizationPercent", out _));
        Assert.True(operational.TryGetProperty("walkInBookings", out _));
        Assert.True(operational.TryGetProperty("onlineBookings", out _));
        Assert.True(operational.TryGetProperty("peakBookingHoursUtc", out _));
        Assert.Contains("UTC", operational.GetProperty("methodologyNote").GetString()!, StringComparison.OrdinalIgnoreCase);

        var predictive = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/analytics/predictive", admin.AccessToken));
        Assert.Equal(7, predictive.GetProperty("demandForecastNext7Days").GetArrayLength());
        Assert.Contains("FORECAST ONLY", predictive.GetProperty("disclaimer").GetString()!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not actual", predictive.GetProperty("disclaimer").GetString()!, StringComparison.OrdinalIgnoreCase);

        var patientExport = await SendAsync(
            HttpMethod.Get,
            $"/api/v1/analytics/export?reportType=executive&format=csv&{range}",
            patient.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, patientExport.StatusCode);

        var csv = await SendAsync(
            HttpMethod.Get,
            $"/api/v1/analytics/export?reportType=executive&format=csv&{range}",
            admin.AccessToken);
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
        var csvBody = await csv.Content.ReadAsStringAsync();
        Assert.Contains("commission_revenue", csvBody);
        Assert.DoesNotContain("PatientName", csvBody, StringComparison.OrdinalIgnoreCase);

        var pdf = await SendAsync(
            HttpMethod.Get,
            $"/api/v1/analytics/export?reportType=financial&format=pdf&{range}",
            admin.AccessToken);
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        var pdfBytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.True(pdfBytes.Length > 100);
        Assert.Equal(0x25, pdfBytes[0]); // %
        Assert.Equal(0x50, pdfBytes[1]); // P

        var badRange = await SendAsync(
            HttpMethod.Get,
            $"/api/v1/analytics/executive?from={to.AddDays(-400):yyyy-MM-dd}&to={to:yyyy-MM-dd}",
            admin.AccessToken);
        Assert.Equal(HttpStatusCode.BadRequest, badRange.StatusCode);

        var audit = await ReadArrayAsync(await SendAsync(HttpMethod.Get, "/api/v1/admin/audit?action=analytics.report", admin.AccessToken));
        Assert.Contains(audit, item => item.GetProperty("action").GetString() == "analytics.report.exported");
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
            deviceName = "analytics-test"
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
