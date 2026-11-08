using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ProDoc.Application.Automation;
using ProDoc.Domain.Automation;

namespace ProDoc.Api.Tests;

[Collection("api")]
public sealed class AutomationFlowTests(ProDocApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Workflows_preferences_outbox_processing_and_cancel_suppress_reminders()
    {
        var admin = await SignInAsync("9000000001", purpose: "admin");
        var patient = await SignInAsync("9000000191");
        await SendAsync(HttpMethod.Put, "/api/v1/account/profile", patient.AccessToken, JsonContent.Create(new
        {
            fullName = "Automation Patient",
            preferredLanguage = "en"
        }));

        var denied = await SendAsync(HttpMethod.Get, "/api/v1/automation/workflows", patient.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var catalog = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/automation/catalog", admin.AccessToken));
        Assert.Contains(catalog.GetProperty("triggers").EnumerateArray(), item => item.GetString() == "appointment.confirmed");
        Assert.Contains(catalog.GetProperty("actions").EnumerateArray(), item => item.GetString() == "send_in_app");

        var workflows = await ReadArrayAsync(await SendAsync(HttpMethod.Get, "/api/v1/automation/workflows", admin.AccessToken));
        Assert.NotEmpty(workflows);
        Assert.Contains(workflows, item => item.GetProperty("isEnabled").GetBoolean());

        var bad = await SendAsync(HttpMethod.Post, "/api/v1/automation/workflows", admin.AccessToken, JsonContent.Create(new
        {
            name = "Hack",
            triggerType = "appointment.confirmed",
            conditions = Array.Empty<object>(),
            actions = new[] { new { type = "eval_code", titleTemplate = "x" } }
        }));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var created = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/automation/workflows", admin.AccessToken, JsonContent.Create(new
        {
            name = "Payment failed SMS",
            triggerType = "billing.payment_failed",
            conditions = Array.Empty<object>(),
            actions = new[]
            {
                new { type = "send_in_app", titleTemplate = "Pay failed", bodyTemplate = "Try again" }
            },
            delayMinutes = 0
        })));
        var workflowId = created.GetProperty("id").GetGuid();
        await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/automation/workflows/{workflowId}/enabled", admin.AccessToken, JsonContent.Create(new
        {
            enabled = true
        })));

        var prefs = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/account/notification-preferences", patient.AccessToken));
        Assert.True(prefs.GetProperty("inAppEnabled").GetBoolean());
        var updatedPrefs = await ReadAsync(await SendAsync(HttpMethod.Put, "/api/v1/account/notification-preferences", patient.AccessToken, JsonContent.Create(new
        {
            inAppEnabled = true,
            emailEnabled = false,
            smsEnabled = false,
            pushEnabled = true,
            marketingEnabled = false,
            appointmentRemindersEnabled = true,
            queueUpdatesEnabled = true
        })));
        Assert.False(updatedPrefs.GetProperty("emailEnabled").GetBoolean());
        Assert.False(updatedPrefs.GetProperty("marketingEnabled").GetBoolean());

        var analytics = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/automation/analytics", admin.AccessToken));
        Assert.True(analytics.GetProperty("enabledWorkflows").GetInt32() >= 1);

        var me = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/account/me", patient.AccessToken));
        var patientUserId = me.GetProperty("userId").GetGuid();
        var appointmentId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IAutomationEventBus>();
            var automation = scope.ServiceProvider.GetRequiredService<IAutomationService>();
            await bus.PublishAsync(
                AutomationTriggerTypes.AppointmentConfirmed,
                new
                {
                    appointmentId,
                    accountUserId = patientUserId,
                    userId = patientUserId,
                    patientName = "Automation Patient",
                    doctorName = "Dr Test",
                    clinicName = "Test Clinic",
                    clinicId = Guid.NewGuid(),
                    branchId = Guid.NewGuid(),
                    doctorId = Guid.NewGuid(),
                    status = "Confirmed",
                    startsAtUtc = DateTimeOffset.UtcNow.AddDays(1),
                    endsAtUtc = DateTimeOffset.UtcNow.AddDays(1).AddMinutes(15),
                    timeZoneId = "Asia/Kolkata",
                    bookingMode = "TimedSlots"
                },
                $"test.confirm:{appointmentId:N}",
                CancellationToken.None,
                subjectId: appointmentId,
                userId: patientUserId);
            await automation.ProcessDueAsync(CancellationToken.None);

            await bus.PublishAsync(
                AutomationTriggerTypes.AppointmentCancelled,
                new
                {
                    appointmentId,
                    accountUserId = patientUserId,
                    userId = patientUserId,
                    patientName = "Automation Patient",
                    doctorName = "Dr Test",
                    clinicName = "Test Clinic",
                    clinicId = Guid.NewGuid(),
                    branchId = Guid.NewGuid(),
                    doctorId = Guid.NewGuid(),
                    status = "Cancelled",
                    startsAtUtc = DateTimeOffset.UtcNow.AddDays(1),
                    endsAtUtc = DateTimeOffset.UtcNow.AddDays(1).AddMinutes(15),
                    timeZoneId = "Asia/Kolkata",
                    bookingMode = "TimedSlots"
                },
                $"test.cancel:{appointmentId:N}",
                CancellationToken.None,
                subjectId: appointmentId,
                userId: patientUserId);
            await automation.ProcessDueAsync(CancellationToken.None);

            // Duplicate publish is idempotent.
            await bus.PublishAsync(
                AutomationTriggerTypes.AppointmentCancelled,
                new { appointmentId, status = "Cancelled" },
                $"test.cancel:{appointmentId:N}",
                CancellationToken.None,
                subjectId: appointmentId);
        }

        var inbox = await ReadArrayAsync(await SendAsync(HttpMethod.Get, "/api/v1/notifications/me", patient.AccessToken));
        Assert.Contains(inbox, item => item.GetProperty("kind").GetString() == "appointment.confirmed"
            || item.GetProperty("kind").GetString() == "appointment.cancelled");

        var unread = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/notifications/me/unread-count", patient.AccessToken));
        Assert.True(unread.GetProperty("count").GetInt32() >= 1);

        var outbox = await ReadArrayAsync(await SendAsync(HttpMethod.Get, "/api/v1/automation/outbox", admin.AccessToken));
        Assert.Contains(outbox, item => item.GetProperty("eventType").GetString() == "appointment.confirmed");
        Assert.Contains(outbox, item => item.GetProperty("eventType").GetString() == "appointment.cancelled");

        var runs = await ReadArrayAsync(await SendAsync(HttpMethod.Get, "/api/v1/automation/runs", admin.AccessToken));
        Assert.NotEmpty(runs);
    }

    private async Task<SessionBody> SignInAsync(string phone, string? purpose = null)
    {
        var otpResponse = await _client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phoneNumber = phone, purpose });
        var otpBody = await otpResponse.Content.ReadAsStringAsync();
        Assert.True(otpResponse.IsSuccessStatusCode, otpBody);
        var otp = JsonSerializer.Deserialize<OtpBody>(otpBody, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(otp?.DevelopmentCode);
        var verify = await _client.PostAsJsonAsync("/api/v1/auth/otp/verify", new
        {
            phoneNumber = phone,
            code = otp!.DevelopmentCode,
            deviceName = "automation-test",
            purpose
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
