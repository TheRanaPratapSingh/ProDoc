using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ProDoc.Api.Tests;

[Collection("api")]
public sealed class SupportFlowTests(ProDocApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Help_center_tickets_internal_notes_and_disputes_are_secured()
    {
        var patient = await SignInAsync("9000000161");
        var admin = await SignInAsync("9000000001");
        await SendAsync(HttpMethod.Put, "/api/v1/account/profile", patient.AccessToken, JsonContent.Create(new
        {
            fullName = "Support Patient",
            preferredLanguage = "en"
        }));

        var categories = await ReadArrayAsync(await _client.GetAsync("/api/v1/help/categories"));
        Assert.NotEmpty(categories);
        var articles = await ReadArrayAsync(await _client.GetAsync("/api/v1/help/articles?q=booking"));
        Assert.Contains(articles, item => item.GetProperty("slug").GetString() == "how-to-book");
        var article = await ReadAsync(await _client.GetAsync("/api/v1/help/articles/how-to-book"));
        Assert.Contains("book", article.GetProperty("titleEn").GetString()!, StringComparison.OrdinalIgnoreCase);

        var ticket = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/support/tickets", patient.AccessToken, JsonContent.Create(new
        {
            category = "PaymentIssue",
            priority = "High",
            subject = "Payment not reflecting",
            description = "I paid online but the visit still shows unpaid."
        })));
        var ticketId = ticket.GetProperty("id").GetGuid();
        Assert.StartsWith("TCK", ticket.GetProperty("number").GetString());

        using var upload = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("receipt-proof"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        upload.Add(fileContent, "file", "receipt.txt");
        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/support/tickets/{ticketId}/attachments") { Content = upload };
        uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", patient.AccessToken);
        var uploadResponse = await _client.SendAsync(uploadRequest);
        var uploadBody = await uploadResponse.Content.ReadAsStringAsync();
        Assert.True(uploadResponse.StatusCode == HttpStatusCode.OK, uploadBody);

        var managed = await ReadAsync(await SendAsync(HttpMethod.Put, $"/api/v1/admin/support/tickets/{ticketId}/manage", admin.AccessToken, JsonContent.Create(new
        {
            status = "InProgress",
            assignedToUserId = (string?)null,
            note = "Checking gateway logs",
            internalNote = true,
            escalate = true
        })));
        Assert.Equal("InProgress", managed.GetProperty("status").GetString());
        Assert.Contains(managed.GetProperty("notes").EnumerateArray(), item => item.GetProperty("isInternal").GetBoolean());

        var customerView = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/v1/support/tickets/{ticketId}", patient.AccessToken));
        Assert.DoesNotContain(customerView.GetProperty("notes").EnumerateArray(), item => item.GetProperty("isInternal").GetBoolean());

        var outsider = await SignInAsync("9000000162");
        var forbidden = await SendAsync(HttpMethod.Get, $"/api/v1/support/tickets/{ticketId}", outsider.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);

        var dispute = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/admin/support/tickets/{ticketId}/disputes", admin.AccessToken, JsonContent.Create(new
        {
            kind = "Payment",
            summary = "Customer reports missing payment confirmation after checkout."
        })));
        Assert.StartsWith("DSP", dispute.GetProperty("number").GetString());

        var evidence = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/admin/support/disputes/{dispute.GetProperty("id").GetGuid()}/actions", admin.AccessToken, JsonContent.Create(new
        {
            action = "request-evidence",
            evidenceRequest = "Please share the payment reference from your bank SMS."
        })));
        Assert.Equal("EvidenceRequested", evidence.GetProperty("status").GetString());

        var workspace = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/admin/support/workspace?filter=high", admin.AccessToken));
        Assert.True(workspace.GetProperty("highPriorityCount").GetInt32() >= 1);

        var analytics = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/admin/support/analytics", admin.AccessToken));
        Assert.True(analytics.GetProperty("openTickets").GetInt32() >= 1);
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
            deviceName = "support-test"
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

    private static async Task<JsonElement[]> ReadArrayAsync(HttpResponseMessage response)
    {
        var root = await ReadAsync(response);
        return root.EnumerateArray().Select(item => item.Clone()).ToArray();
    }

    private sealed record OtpBody(string? DevelopmentCode);

    private sealed record SessionBody(string AccessToken, string RefreshToken);
}
