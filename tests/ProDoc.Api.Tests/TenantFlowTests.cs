using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProDoc.Api.Tests;

[Collection("api")]
public sealed class TenantFlowTests(ProDocApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Tenant_isolation_plans_branding_subscription_billing_and_admin_controls()
    {
        var admin = await SignInAsync("9000000001");
        var owner = await SignInAsync("9000000181");
        var outsider = await SignInAsync("9000000182");
        await SendAsync(HttpMethod.Put, "/api/v1/account/profile", owner.AccessToken, JsonContent.Create(new
        {
            fullName = "Tenant Owner",
            preferredLanguage = "en"
        }));

        var plans = await ReadArrayAsync(await _client.GetAsync("/api/v1/saas/plans"));
        Assert.Contains(plans, item => item.GetProperty("code").GetString() == "STARTER");
        Assert.Contains(plans, item => item.GetProperty("code").GetString() == "PROFESSIONAL");
        Assert.Contains(plans, item => item.GetProperty("code").GetString() == "ENTERPRISE");

        var created = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/organizations", owner.AccessToken, JsonContent.Create(new
        {
            name = "White Label Care",
            kind = "ClinicChain"
        })));
        var organizationId = created.GetProperty("id").GetGuid();
        owner = await SignInAsync("9000000181");

        var usage = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/v1/organizations/{organizationId}/usage", owner.AccessToken));
        Assert.Equal("STARTER", usage.GetProperty("planCode").GetString());
        Assert.Equal(1, usage.GetProperty("clinicsLimit").GetInt32());

        var crossUsage = await SendAsync(HttpMethod.Get, $"/api/v1/organizations/{organizationId}/usage", outsider.AccessToken);
        Assert.True(crossUsage.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden);

        var brandingDenied = await SendAsync(HttpMethod.Put, $"/api/v1/organizations/{organizationId}/branding", owner.AccessToken, JsonContent.Create(new
        {
            primaryColorHex = "#0F766E",
            accentColorHex = "#134E4A",
            loginHeadline = "White Label Care",
            subdomainSlug = "wlcare"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, brandingDenied.StatusCode);

        await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/organizations/{organizationId}/saas/change-plan", owner.AccessToken, JsonContent.Create(new
        {
            planCode = "PROFESSIONAL",
            billingInterval = "Monthly",
            applyImmediately = true
        })));

        var branding = await ReadAsync(await SendAsync(HttpMethod.Put, $"/api/v1/organizations/{organizationId}/branding", owner.AccessToken, JsonContent.Create(new
        {
            primaryColorHex = "#0F766E",
            accentColorHex = "#134E4A",
            loginHeadline = "White Label Care",
            loginSubheadline = "Your clinic brand",
            emailFromName = "White Label Care",
            emailFooterText = "Powered by ProDoc",
            subdomainSlug = "wlcare18"
        })));
        Assert.Equal("wlcare18", branding.GetProperty("subdomainSlug").GetString());
        Assert.True(branding.GetProperty("whiteLabelEnabled").GetBoolean());

        var publicBranding = await ReadAsync(await _client.GetAsync("/api/v1/public/branding?slug=wlcare18"));
        Assert.Equal(organizationId, publicBranding.GetProperty("organizationId").GetGuid());
        Assert.Equal("slug", publicBranding.GetProperty("resolvedBy").GetString());

        var charge = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/organizations/{organizationId}/saas/charges", owner.AccessToken, JsonContent.Create(new
        {
            idempotencyKey = $"test-saas-{organizationId:N}"
        })));
        var chargeId = charge.GetProperty("id").GetGuid();
        Assert.Equal("Pending", charge.GetProperty("status").GetString());

        var replay = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/organizations/{organizationId}/saas/charges", owner.AccessToken, JsonContent.Create(new
        {
            idempotencyKey = $"test-saas-{organizationId:N}"
        })));
        Assert.Equal(chargeId, replay.GetProperty("id").GetGuid());

        var paid = await ReadAsync(await SendAsync(
            HttpMethod.Post,
            $"/api/v1/organizations/{organizationId}/saas/charges/{chargeId}/confirm",
            owner.AccessToken,
            JsonContent.Create(new { providerPaymentId = (string?)null })));
        Assert.Equal("Paid", paid.GetProperty("status").GetString());

        var failed = await SendAsync(HttpMethod.Post, $"/api/v1/organizations/{organizationId}/saas/charges", owner.AccessToken, JsonContent.Create(new
        {
            idempotencyKey = $"test-saas-fail-{organizationId:N}"
        }));
        var failCharge = await ReadAsync(failed);
        var failConfirm = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/organizations/{organizationId}/saas/charges/{failCharge.GetProperty("id").GetGuid()}/confirm",
            owner.AccessToken,
            JsonContent.Create(new { providerPaymentId = "fail" }));
        Assert.Equal(HttpStatusCode.BadRequest, failConfirm.StatusCode);

        var platform = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/admin/tenants", admin.AccessToken));
        Assert.True(platform.GetProperty("saaSRevenueCollected").GetDecimal() >= paid.GetProperty("amount").GetDecimal());
        Assert.True(platform.GetProperty("appointmentCommissionRevenue").GetDecimal() >= 0);
        Assert.Contains(
            platform.GetProperty("tenants").EnumerateArray(),
            item => item.GetProperty("organizationId").GetGuid() == organizationId);

        var patientDenied = await SendAsync(HttpMethod.Get, "/api/v1/admin/tenants", outsider.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, patientDenied.StatusCode);

        await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/admin/tenants/{organizationId}/suspend", admin.AccessToken, JsonContent.Create(new
        {
            suspend = true
        })));
        var suspendedBranding = await SendAsync(HttpMethod.Put, $"/api/v1/organizations/{organizationId}/branding", owner.AccessToken, JsonContent.Create(new
        {
            primaryColorHex = "#111111",
            accentColorHex = "#222222",
            loginHeadline = "Blocked"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, suspendedBranding.StatusCode);

        await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/admin/tenants/{organizationId}/suspend", admin.AccessToken, JsonContent.Create(new
        {
            suspend = false
        })));

        await ReadAsync(await SendAsync(
            HttpMethod.Put,
            $"/api/v1/admin/tenants/{organizationId}/entitlements/feature.custom_domain",
            admin.AccessToken,
            JsonContent.Create(new { enabled = true, limitValue = (int?)null })));
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
            deviceName = "tenant-test"
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
