using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Api.Tests;

public sealed class ProDocApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString =
        (Environment.GetEnvironmentVariable("PRODOC_TEST_SQL_SERVER") is { Length: > 0 } server
            ? $"Server={server};Database=ProDocTests_{Guid.NewGuid():N};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
            : $"Server=localhost;Database=ProDocTests_{Guid.NewGuid():N};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True");

    public ProDocApiFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__ProDoc", _connectionString);
        Environment.SetEnvironmentVariable("Auth__DevelopmentAdminPhones__0", "9000000001");
        Environment.SetEnvironmentVariable("Auth__OtpResendCooldownSeconds", "0");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ProDoc"] = _connectionString,
                ["Auth:OtpResendCooldownSeconds"] = "0",
                ["Auth:OtpMaxRequestsPerWindow"] = "100",
                ["Auth:DevelopmentAdminPhones:0"] = "9000000001"
            });
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProDocDbContext>();
        await db.Database.EnsureDeletedAsync();
        await base.DisposeAsync();
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ProDocApiFactory>;

[Collection("api")]
public sealed class AuthFlowTests
{
    private readonly HttpClient _client;

    public AuthFlowTests(ProDocApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Patient_can_sign_in_update_profile_and_request_deletion()
    {
        var session = await SignInAsync("9876543210");
        using var meRequest = Authorized(HttpMethod.Get, "/api/v1/account/me", session.AccessToken);
        var me = await _client.SendAsync(meRequest);
        var account = await me.Content.ReadFromJsonAsync<AccountBody>();

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal("+919876543210", account!.PhoneNumber);
        Assert.Contains("Patient", account.Roles);
        Assert.Contains("account.read", account.Permissions);

        using var profileRequest = Authorized(HttpMethod.Put, "/api/v1/account/profile", session.AccessToken);
        profileRequest.Content = JsonContent.Create(new { fullName = "Asha Rao", preferredLanguage = "hi" });
        var profileResponse = await _client.SendAsync(profileRequest);
        var profile = await profileResponse.Content.ReadFromJsonAsync<AccountBody>();

        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
        Assert.Equal("Asha Rao", profile!.FullName);
        Assert.Equal("hi", profile.PreferredLanguage);

        using var deletionRequest = Authorized(HttpMethod.Post, "/api/v1/account/deletion-request", session.AccessToken);
        var deletion = await _client.SendAsync(deletionRequest);
        Assert.Equal(HttpStatusCode.Accepted, deletion.StatusCode);

        using var blocked = Authorized(HttpMethod.Get, "/api/v1/account/me", session.AccessToken);
        var blockedResponse = await _client.SendAsync(blocked);
        Assert.Equal(HttpStatusCode.Forbidden, blockedResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotation_rejects_a_reused_token_and_its_replacement()
    {
        var session = await SignInAsync("9123456780");
        var rotatedResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = session.RefreshToken });
        var rotated = await rotatedResponse.Content.ReadFromJsonAsync<SessionBody>();
        Assert.Equal(HttpStatusCode.OK, rotatedResponse.StatusCode);

        var reuse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = session.RefreshToken });
        var replacement = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = rotated!.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, replacement.StatusCode);
    }

    [Fact]
    public async Task Incorrect_otp_returns_controlled_validation_problem_not_500()
    {
        var otpResponse = await _client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phoneNumber = "9765432100" });
        Assert.Equal(HttpStatusCode.OK, otpResponse.StatusCode);

        var verifyResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/otp/verify",
            new { phoneNumber = "9765432100", code = "000000", deviceName = "integration-test" });
        var problem = await verifyResponse.Content.ReadFromJsonAsync<ProblemBody>();

        Assert.Equal(HttpStatusCode.BadRequest, verifyResponse.StatusCode);
        Assert.Equal("otp_invalid", problem!.Code);
        Assert.Equal("Incorrect OTP. Please try again.", problem.Title);
    }

    [Fact]
    public async Task Invalid_phone_on_otp_request_returns_400_without_server_error()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phoneNumber = "123" });
        var problem = await response.Content.ReadFromJsonAsync<ProblemBody>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("phone_invalid", problem!.Code);
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    private async Task<SessionBody> SignInAsync(string phone)
    {
        var otpResponse = await _client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phoneNumber = phone });
        var otp = await otpResponse.Content.ReadFromJsonAsync<OtpBody>();
        Assert.Equal(HttpStatusCode.OK, otpResponse.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(otp!.DevelopmentCode));

        var verifyResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/otp/verify",
            new { phoneNumber = phone, code = otp.DevelopmentCode, deviceName = "integration-test" });
        var session = await verifyResponse.Content.ReadFromJsonAsync<SessionBody>();
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        return session!;
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string accessToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private sealed record OtpBody(string? DevelopmentCode);

    private sealed record SessionBody(string AccessToken, string RefreshToken);

    private sealed record AccountBody(
        string PhoneNumber,
        string? FullName,
        string PreferredLanguage,
        IReadOnlyList<string> Roles,
        IReadOnlyList<string> Permissions);

    private sealed record ProblemBody(string? Title, string? Detail, string? Code, string? Type);
}
