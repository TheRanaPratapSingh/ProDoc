using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProDoc.Api.Tests;

[Collection("api")]
public sealed class BillingFlowTests(ProDocApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Online_payment_is_idempotent_verified_server_side_and_walk_in_skips_commission()
    {
        var doctor = await SignInAsync("9000000061");
        var patient = await SignInAsync("9000000062");
        var admin = await SignInAsync("9000000001");
        await SendAsync(HttpMethod.Put, "/api/v1/account/profile", patient.AccessToken, JsonContent.Create(new { fullName = "Asha Pai", preferredLanguage = "en" }));
        var specializations = await SendAsync(HttpMethod.Get, "/api/v1/specializations", doctor.AccessToken);
        var cardiology = (await specializations.Content.ReadFromJsonAsync<SpecializationBody[]>())!.Single(item => item.Code == "cardiology");
        var (scheduleId, clinicId, branchId, _) = await PublishAsync(doctor, admin, cardiology.Id);
        doctor = await SignInAsync("9000000061");
        await ReadAsync(await SendAsync(HttpMethod.Put, $"/api/v1/clinics/{clinicId}/payment-settings", doctor.AccessToken, JsonContent.Create(new
        {
            policy = "OnlineOptional",
            advancePercent = 0,
            chargeCommissionOnWalkIns = false
        })));
        await ReadAsync(await SendAsync(HttpMethod.Put, $"/api/v1/clinics/{clinicId}/branches/{branchId}/queue-settings", doctor.AccessToken, JsonContent.Create(new
        {
            tokenPrefix = "B",
            scheduledPatientPriority = true,
            earlyCheckInMinutes = 10080,
            lateCheckInMinutes = 10080,
            allowWalkIns = true,
            allowPriorityPatients = false
        })));

        var date = ClinicDate(2);
        var open = await SlotsAsync(scheduleId, date, patient.AccessToken);
        var held = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/appointments/holds", patient.AccessToken, JsonContent.Create(new
        {
            scheduleId,
            date = date.ToString("yyyy-MM-dd"),
            start = open[0]
        })));
        var confirmed = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/appointments/{held.GetProperty("id").GetGuid()}/confirm", patient.AccessToken, JsonContent.Create(new { reason = "Fever" })));
        var appointmentId = confirmed.GetProperty("id").GetGuid();

        var key = $"idem-{appointmentId:N}";
        var payment = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/appointments/{appointmentId}/payments", patient.AccessToken, JsonContent.Create(new
        {
            paymentMethod = "PayOnline",
            idempotencyKey = key
        })));
        var paymentId = payment.GetProperty("id").GetGuid();
        var replay = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/appointments/{appointmentId}/payments", patient.AccessToken, JsonContent.Create(new
        {
            paymentMethod = "PayOnline",
            idempotencyKey = key
        })));
        Assert.Equal(paymentId, replay.GetProperty("id").GetGuid());

        var verified = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/payments/{paymentId}/verify", patient.AccessToken, JsonContent.Create(new { })));
        Assert.Equal("Paid", verified.GetProperty("status").GetString());
        var duplicate = await SendAsync(HttpMethod.Post, $"/api/v1/appointments/{appointmentId}/payments", patient.AccessToken, JsonContent.Create(new
        {
            paymentMethod = "PayOnline",
            idempotencyKey = $"other-{appointmentId:N}"
        }));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var qr = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/appointments/{appointmentId}/check-in/qr-token", patient.AccessToken));
        var checkedIn = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/check-ins/qr", doctor.AccessToken, JsonContent.Create(new { token = qr.GetProperty("token").GetString() })));
        await SendAsync(HttpMethod.Post, $"/api/v1/queue-entries/{checkedIn.GetProperty("id").GetGuid()}/call", doctor.AccessToken);
        await SendAsync(HttpMethod.Post, $"/api/v1/queue-entries/{checkedIn.GetProperty("id").GetGuid()}/start-consultation", doctor.AccessToken);
        await SendAsync(HttpMethod.Post, $"/api/v1/queue-entries/{checkedIn.GetProperty("id").GetGuid()}/complete-consultation", doctor.AccessToken);

        var adminFinance = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/admin/finance", admin.AccessToken));
        Assert.True(adminFinance.GetProperty("commissionEarned").GetDecimal() > 0);
        Assert.True(adminFinance.GetProperty("grossPlatformBookingValue").GetDecimal() > 0);

        var walkIn = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/clinics/{clinicId}/branches/{branchId}/walk-ins", doctor.AccessToken, JsonContent.Create(new
        {
            doctorId = checkedIn.GetProperty("doctorId").GetGuid(),
            patientName = "Desk Guest"
        })));
        var walkEntry = walkIn.GetProperty("queueEntryId").GetGuid();
        await SendAsync(HttpMethod.Post, $"/api/v1/queue-entries/{walkEntry}/call", doctor.AccessToken);
        await SendAsync(HttpMethod.Post, $"/api/v1/queue-entries/{walkEntry}/start-consultation", doctor.AccessToken);
        await SendAsync(HttpMethod.Post, $"/api/v1/queue-entries/{walkEntry}/complete-consultation", doctor.AccessToken);

        var afterWalkIn = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/admin/finance", admin.AccessToken));
        Assert.Equal(adminFinance.GetProperty("commissionEarned").GetDecimal(), afterWalkIn.GetProperty("commissionEarned").GetDecimal());

        var settlement = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/admin/settlements", admin.AccessToken, JsonContent.Create(new
        {
            clinicId,
            periodFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd"),
            periodTo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd")
        })));
        Assert.Equal("Finalized", settlement.GetProperty("status").GetString());
        Assert.True(settlement.GetProperty("totalCommission").GetDecimal() > 0);
    }

    private async Task<(Guid ScheduleId, Guid ClinicId, Guid BranchId, Guid DoctorId)> PublishAsync(SessionBody doctor, SessionBody admin, Guid specializationId)
    {
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/basics", doctor.AccessToken, JsonContent.Create(new
        {
            firstName = "Dev",
            lastName = "Shah",
            dateOfBirth = "1980-01-01",
            gender = "male",
            email = "dev@example.com",
            preferredLanguage = "en"
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/professional", doctor.AccessToken, JsonContent.Create(new
        {
            displayName = "Dr. Dev Shah",
            biography = "Billing clinic",
            yearsOfExperience = 12,
            languages = new[] { "en" },
            expertise = new[] { "General" }
        }));
        await SendAsync(HttpMethod.Post, "/api/v1/doctors/me/qualifications", doctor.AccessToken, JsonContent.Create(new
        {
            name = "MBBS",
            institution = "Grant",
            graduationYear = 2006
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/specializations", doctor.AccessToken, JsonContent.Create(new
        {
            specializationIds = new[] { specializationId },
            primarySpecializationId = specializationId
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/registration", doctor.AccessToken, JsonContent.Create(new
        {
            number = "MCI-DEV",
            council = "National Medical Commission",
            state = "Maharashtra",
            registrationType = "permanent",
            registeredOn = "2008-01-01",
            expiresOn = "2030-01-01"
        }));
        await UploadAsync(doctor.AccessToken, "/api/v1/doctors/me/documents", "photo.png", "image/png", Png, "ProfilePhoto");
        await UploadAsync(doctor.AccessToken, "/api/v1/doctors/me/documents", "registration.pdf", "application/pdf", "%PDF-1.4\n"u8.ToArray(), "RegistrationCertificate");
        var submitted = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/doctors/me/submit", doctor.AccessToken));
        var doctorId = submitted.GetProperty("id").GetGuid();
        await SendAsync(HttpMethod.Post, $"/api/v1/admin/verifications/doctors/{doctorId}", admin.AccessToken, JsonContent.Create(new { decision = "Approve" }));

        var clinic = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/clinics", doctor.AccessToken));
        var clinicId = clinic.GetProperty("id").GetGuid();
        await SendAsync(HttpMethod.Put, $"/api/v1/clinics/{clinicId}", doctor.AccessToken, JsonContent.Create(new
        {
            name = "Shah Clinic",
            type = "Clinic",
            phoneNumber = "9876506611",
            registrationNumber = "CLN-DEV",
            establishmentYear = 2014
        }));
        var branched = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/clinics/{clinicId}/branches", doctor.AccessToken, JsonContent.Create(new
        {
            name = "Andheri",
            addressLine = "9 Link Road",
            city = "Mumbai",
            state = "Maharashtra",
            postalCode = "400053",
            isPrimary = true
        })));
        var branchId = branched.GetProperty("branches").EnumerateArray().Single().GetProperty("id").GetGuid();
        await UploadAsync(doctor.AccessToken, $"/api/v1/clinics/{clinicId}/documents", "clinic.pdf", "application/pdf", "%PDF-1.4\n"u8.ToArray(), "ClinicRegistration");
        await SendAsync(HttpMethod.Post, $"/api/v1/clinics/{clinicId}/submit", doctor.AccessToken);
        await SendAsync(HttpMethod.Post, $"/api/v1/admin/verifications/clinics/{clinicId}", admin.AccessToken, JsonContent.Create(new { decision = "Approve" }));
        await SendAsync(HttpMethod.Post, "/api/v1/doctors/me/affiliations", doctor.AccessToken, JsonContent.Create(new { clinicId, branchId, kind = "Independent" }));
        var created = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/schedules", doctor.AccessToken, JsonContent.Create(new
        {
            clinicId,
            branchId,
            consultationFee = 600,
            slotDurationMinutes = 20
        })));
        var scheduleId = created.GetProperty("id").GetGuid();
        var days = Enum.GetNames<DayOfWeek>();
        await SendAsync(HttpMethod.Put, $"/api/v1/schedules/{scheduleId}/hours", doctor.AccessToken, JsonContent.Create(new
        {
            periods = days.Select(day => new { day, startsAt = "09:00", endsAt = "12:00" })
        }));
        await SendAsync(HttpMethod.Post, $"/api/v1/schedules/{scheduleId}/publication", doctor.AccessToken, JsonContent.Create(new { isPublished = true }));
        return (scheduleId, clinicId, branchId, doctorId);
    }

    private static DateOnly ClinicDate(int daysAhead)
    {
        var zone = TimeZoneInfo.TryFindSystemTimeZoneById("Asia/Kolkata", out var resolved)
            ? resolved
            : TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime).AddDays(daysAhead);
    }

    private async Task<string[]> SlotsAsync(Guid scheduleId, DateOnly date, string accessToken)
    {
        var body = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/v1/schedules/{scheduleId}/slots?date={date:yyyy-MM-dd}", accessToken));
        return body.GetProperty("slots").EnumerateArray().Select(slot => slot.GetProperty("start").GetString()!).ToArray();
    }

    private async Task<SessionBody> SignInAsync(string phone)
    {
        var otpResponse = await _client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phoneNumber = phone });
        var otpBody = await otpResponse.Content.ReadAsStringAsync();
        Assert.True(otpResponse.IsSuccessStatusCode, otpBody);
        var otp = JsonSerializer.Deserialize<OtpBody>(otpBody, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var verifyResponse = await _client.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phoneNumber = phone, code = otp!.DevelopmentCode, deviceName = "integration-test" });
        var verifyBody = await verifyResponse.Content.ReadAsStringAsync();
        Assert.True(verifyResponse.IsSuccessStatusCode, verifyBody);
        return JsonSerializer.Deserialize<SessionBody>(verifyBody, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private async Task<HttpResponseMessage> UploadAsync(string accessToken, string path, string fileName, string contentType, byte[] bytes, string category)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        form.Add(new StringContent(category), "category");
        return await SendAsync(HttpMethod.Post, path, accessToken, form);
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

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    private sealed record OtpBody(string? DevelopmentCode);

    private sealed record SessionBody(string AccessToken, string RefreshToken);

    private sealed record SpecializationBody(Guid Id, string Code);
}
