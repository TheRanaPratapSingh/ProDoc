using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProDoc.Api.Tests;

[Collection("api")]
public sealed class QueueFlowTests(ProDocApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Check_in_issues_a_unique_token_and_blocks_replay()
    {
        var doctor = await SignInAsync("9000000041");
        var patient = await SignInAsync("9000000042");
        var admin = await SignInAsync("9000000001", purpose: "admin");
        await SendAsync(HttpMethod.Put, "/api/v1/account/profile", patient.AccessToken, JsonContent.Create(new { fullName = "Riya Nair", preferredLanguage = "en" }));
        var specializations = await SendAsync(HttpMethod.Get, "/api/v1/specializations", doctor.AccessToken);
        var cardiology = (await specializations.Content.ReadFromJsonAsync<SpecializationBody[]>())!.Single(item => item.Code == "cardiology");
        var (scheduleId, clinicId, branchId, doctorId) = await PublishAsync(doctor, admin, cardiology.Id);
        doctor = await SignInAsync("9000000041");
        await ReadAsync(await SendAsync(HttpMethod.Put, $"/api/v1/clinics/{clinicId}/branches/{branchId}/queue-settings", doctor.AccessToken, JsonContent.Create(new
        {
            tokenPrefix = "A",
            scheduledPatientPriority = true,
            earlyCheckInMinutes = 10080,
            lateCheckInMinutes = 10080,
            allowWalkIns = true,
            allowPriorityPatients = false
        })));
        var date = ClinicDate(2);
        var open = await SlotsAsync(scheduleId, date, patient.AccessToken);
        var start = open[0];
        var held = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/appointments/holds", patient.AccessToken, JsonContent.Create(new
        {
            scheduleId,
            date = date.ToString("yyyy-MM-dd"),
            start
        })));
        var confirmed = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/appointments/{held.GetProperty("id").GetGuid()}/confirm", patient.AccessToken, JsonContent.Create(new { reason = "Follow up" })));
        var appointmentId = confirmed.GetProperty("id").GetGuid();

        var qr = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/appointments/{appointmentId}/check-in/qr-token", patient.AccessToken));
        var token = qr.GetProperty("token").GetString()!;
        var checkedIn = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/check-ins/qr", doctor.AccessToken, JsonContent.Create(new { token })));
        Assert.Equal("Waiting", checkedIn.GetProperty("status").GetString());
        Assert.StartsWith("A", checkedIn.GetProperty("tokenLabel").GetString());

        var replay = await SendAsync(HttpMethod.Post, "/api/v1/check-ins/qr", doctor.AccessToken, JsonContent.Create(new { token }));
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        var duplicate = await SendAsync(HttpMethod.Post, $"/api/v1/appointments/{appointmentId}/check-in", doctor.AccessToken);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var walkIn = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/clinics/{clinicId}/branches/{branchId}/walk-ins", doctor.AccessToken, JsonContent.Create(new
        {
            doctorId,
            patientName = "Desk Guest",
            reason = "Fever"
        })));
        var walkInTwo = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/clinics/{clinicId}/branches/{branchId}/walk-ins", doctor.AccessToken, JsonContent.Create(new
        {
            doctorId,
            patientName = "Second Guest"
        })));
        Assert.NotEqual(walkIn.GetProperty("tokenLabel").GetString(), walkInTwo.GetProperty("tokenLabel").GetString());

        var entryId = checkedIn.GetProperty("id").GetGuid();
        await SendAsync(HttpMethod.Post, $"/api/v1/queue-entries/{entryId}/call", doctor.AccessToken);
        await SendAsync(HttpMethod.Post, $"/api/v1/queue-entries/{entryId}/start-consultation", doctor.AccessToken);
        var completed = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/queue-entries/{entryId}/complete-consultation", doctor.AccessToken));
        Assert.Equal("Completed", completed.GetProperty("status").GetString());
        var appointment = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/v1/appointments/{appointmentId}", patient.AccessToken));
        Assert.Equal("Completed", appointment.GetProperty("status").GetString());

        var walkEntryId = walkIn.GetProperty("queueEntryId").GetGuid();
        await SendAsync(HttpMethod.Post, $"/api/v1/queue-entries/{walkEntryId}/skip", doctor.AccessToken, JsonContent.Create(new { reason = "Left" }));
        var mine = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/appointments", patient.AccessToken));
        Assert.DoesNotContain("Desk Guest", mine.ToString());
    }

    private async Task<(Guid ScheduleId, Guid ClinicId, Guid BranchId, Guid DoctorId)> PublishAsync(SessionBody doctor, SessionBody admin, Guid specializationId)
    {
        await ProviderSeed.EnsureDoctorRoleAsync(_client, doctor.AccessToken, admin.AccessToken);
        doctor = await SignInAsync("9000000041");
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/basics", doctor.AccessToken, JsonContent.Create(new
        {
            firstName = "Anika",
            lastName = "Menon",
            dateOfBirth = "1985-05-05",
            gender = "female",
            email = "anika@example.com",
            preferredLanguage = "en"
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/professional", doctor.AccessToken, JsonContent.Create(new
        {
            displayName = "Dr. Anika Menon",
            biography = "Clinic visits",
            yearsOfExperience = 11,
            languages = new[] { "en", "hi" },
            expertise = new[] { "Clinic visits" }
        }));
        await SendAsync(HttpMethod.Post, "/api/v1/doctors/me/qualifications", doctor.AccessToken, JsonContent.Create(new
        {
            name = "MBBS",
            institution = "AIIMS",
            graduationYear = 2009
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/specializations", doctor.AccessToken, JsonContent.Create(new
        {
            specializationIds = new[] { specializationId },
            primarySpecializationId = specializationId
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/registration", doctor.AccessToken, JsonContent.Create(new
        {
            number = "MCI-ANIKA",
            council = "National Medical Commission",
            state = "Kerala",
            registrationType = "permanent",
            registeredOn = "2011-06-01",
            expiresOn = "2031-06-01"
        }));
        await UploadAsync(doctor.AccessToken, "/api/v1/doctors/me/documents", "photo.png", "image/png", Png, "ProfilePhoto");
        await UploadAsync(doctor.AccessToken, "/api/v1/doctors/me/documents", "registration.pdf", "application/pdf", "%PDF-1.4\n"u8.ToArray(), "RegistrationCertificate");
        var submitted = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/doctors/me/submit", doctor.AccessToken));
        var doctorId = submitted.GetProperty("id").GetGuid();
        await SendAsync(HttpMethod.Post, $"/api/v1/admin/verifications/doctors/{doctorId}", admin.AccessToken, JsonContent.Create(new { decision = "Approve" }));

        var ownerUserId = await ProviderSeed.GetUserIdAsync(_client, doctor.AccessToken);
        var clinicId = await ProviderSeed.CreateClinicOwnedByAsync(_client, admin.AccessToken, ownerUserId);
        await SendAsync(HttpMethod.Put, $"/api/v1/clinics/{clinicId}", doctor.AccessToken, JsonContent.Create(new
        {
            name = "Menon Clinic",
            type = "Clinic",
            phoneNumber = "9876504411",
            registrationNumber = "CLN-ANIKA",
            establishmentYear = 2015
        }));
        var branched = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/clinics/{clinicId}/branches", doctor.AccessToken, JsonContent.Create(new
        {
            name = "Kochi Main",
            addressLine = "1 MG Road",
            city = "Kochi",
            state = "Kerala",
            postalCode = "682001",
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
            consultationFee = 400,
            slotDurationMinutes = 20
        })));
        var scheduleId = created.GetProperty("id").GetGuid();
        var rowVersion = created.GetProperty("rowVersion").GetString();
        var days = Enum.GetNames<DayOfWeek>();
        var withHours = await ReadAsync(await SendAsync(HttpMethod.Put, $"/api/v1/schedules/{scheduleId}/hours", doctor.AccessToken, JsonContent.Create(new
        {
            expectedRowVersion = rowVersion,
            periods = days.Select(day => new { day, startsAt = "09:00", endsAt = "12:00" })
        })));
        await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/schedules/{scheduleId}/publication", doctor.AccessToken, JsonContent.Create(new
        {
            isPublished = true,
            expectedRowVersion = withHours.GetProperty("rowVersion").GetString()
        })));
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

    private async Task<SessionBody> SignInAsync(string phone, string? purpose = null)
    {
        var otpResponse = await _client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phoneNumber = phone, purpose });
        var otpBody = await otpResponse.Content.ReadAsStringAsync();
        Assert.True(otpResponse.IsSuccessStatusCode, otpBody);
        var otp = JsonSerializer.Deserialize<OtpBody>(otpBody, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var verifyResponse = await _client.PostAsJsonAsync("/api/v1/auth/otp/verify", new
        {
            phoneNumber = phone,
            code = otp!.DevelopmentCode,
            deviceName = "integration-test",
            purpose
        });
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
