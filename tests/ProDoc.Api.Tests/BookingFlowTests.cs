using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProDoc.Api.Tests;

[Collection("api")]
public sealed class BookingFlowTests(ProDocApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Booking_locks_a_slot_snapshots_the_fee_and_keeps_notes_off_the_admin_list()
    {
        var doctor = await SignInAsync("9000000031");
        var patient = await SignInAsync("9000000032");
        var other = await SignInAsync("9000000033");
        var admin = await SignInAsync("9000000001", purpose: "admin");
        await SendAsync(HttpMethod.Put, "/api/v1/account/profile", patient.AccessToken, JsonContent.Create(new { fullName = "Meera Iyer", preferredLanguage = "en" }));
        var specializations = await SendAsync(HttpMethod.Get, "/api/v1/specializations", doctor.AccessToken);
        var cardiology = (await specializations.Content.ReadFromJsonAsync<SpecializationBody[]>())!.Single(item => item.Code == "cardiology");
        var (scheduleId, scheduleRowVersion) = await PublishScheduleAsync(doctor, admin, cardiology.Id);
        doctor = await SignInAsync("9000000031");
        var date = ClinicDate(2);
        var open = await SlotsAsync(scheduleId, date, patient.AccessToken);
        Assert.Contains("09:00:00", open);

        var held = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/appointments/holds", patient.AccessToken, JsonContent.Create(new
        {
            scheduleId,
            date = date.ToString("yyyy-MM-dd"),
            start = "09:00:00"
        })));
        var appointmentId = held.GetProperty("id").GetGuid();
        Assert.Equal(500, held.GetProperty("consultationFee").GetDecimal());
        Assert.Equal("Held", held.GetProperty("status").GetString());
        var conflict = await SendAsync(HttpMethod.Post, "/api/v1/appointments/holds", other.AccessToken, JsonContent.Create(new
        {
            scheduleId,
            date = date.ToString("yyyy-MM-dd"),
            start = "09:00:00"
        }));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var updatedSchedule = await ReadAsync(await SendAsync(HttpMethod.Put, $"/api/v1/schedules/{scheduleId}", doctor.AccessToken, JsonContent.Create(new
        {
            consultationFee = 800,
            slotDurationMinutes = 30,
            bufferMinutes = 0,
            requiresApproval = false,
            expectedRowVersion = scheduleRowVersion
        })));
        scheduleRowVersion = updatedSchedule.GetProperty("rowVersion").GetString()!;
        var confirmed = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/appointments/{appointmentId}/confirm", patient.AccessToken, JsonContent.Create(new
        {
            reason = "Headache",
            notes = "Since morning"
        })));
        Assert.Equal("Confirmed", confirmed.GetProperty("status").GetString());
        Assert.Equal(500, confirmed.GetProperty("consultationFee").GetDecimal());
        Assert.Equal("PayAtClinic", confirmed.GetProperty("paymentMethod").GetString());
        Assert.StartsWith("APT-", confirmed.GetProperty("reference").GetString());
        Assert.DoesNotContain("09:00:00", await SlotsAsync(scheduleId, date, patient.AccessToken));

        var cancelled = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/appointments/{appointmentId}/cancellation", patient.AccessToken, JsonContent.Create(new { reason = "Changed plans" })));
        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());
        Assert.Contains("09:00:00", await SlotsAsync(scheduleId, date, patient.AccessToken));

        updatedSchedule = await ReadAsync(await SendAsync(HttpMethod.Put, $"/api/v1/schedules/{scheduleId}", doctor.AccessToken, JsonContent.Create(new
        {
            consultationFee = 800,
            slotDurationMinutes = 30,
            bufferMinutes = 0,
            requiresApproval = true,
            expectedRowVersion = scheduleRowVersion
        })));
        scheduleRowVersion = updatedSchedule.GetProperty("rowVersion").GetString()!;
        var requestedHold = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/appointments/holds", patient.AccessToken, JsonContent.Create(new
        {
            scheduleId,
            date = date.ToString("yyyy-MM-dd"),
            start = "09:30:00"
        })));
        var requested = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/appointments/{requestedHold.GetProperty("id").GetGuid()}/confirm", patient.AccessToken, JsonContent.Create(new { reason = "Headache" })));
        Assert.Equal("Requested", requested.GetProperty("status").GetString());
        var doctorAgain = await SignInAsync("9000000031");
        var approved = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/appointments/{requested.GetProperty("id").GetGuid()}/decision", doctorAgain.AccessToken, JsonContent.Create(new { decision = "Approve" })));
        Assert.Equal("Confirmed", approved.GetProperty("status").GetString());

        var child = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/family-members", patient.AccessToken, JsonContent.Create(new
        {
            fullName = "Aarav Iyer",
            relationship = "child"
        })));
        var familyDate = ClinicDate(3);
        var familyHold = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/appointments/holds", patient.AccessToken, JsonContent.Create(new
        {
            scheduleId,
            date = familyDate.ToString("yyyy-MM-dd"),
            start = "09:00:00"
        })));
        var familyVisit = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/appointments/{familyHold.GetProperty("id").GetGuid()}/confirm", patient.AccessToken, JsonContent.Create(new
        {
            familyMemberId = child.GetProperty("id").GetGuid(),
            reason = "Headache"
        })));
        Assert.Equal("Aarav Iyer", familyVisit.GetProperty("patientName").GetString());
        Assert.Equal("Requested", familyVisit.GetProperty("status").GetString());

        var sameDayDuplicate = await SendAsync(HttpMethod.Post, "/api/v1/appointments/holds", patient.AccessToken, JsonContent.Create(new
        {
            scheduleId,
            date = date.ToString("yyyy-MM-dd"),
            start = "10:00:00"
        }));
        Assert.Equal(HttpStatusCode.Conflict, sameDayDuplicate.StatusCode);
        var duplicateBody = await sameDayDuplicate.Content.ReadAsStringAsync();
        Assert.Contains("duplicate_appointment", duplicateBody, StringComparison.OrdinalIgnoreCase);

        var nextDay = ClinicDate(4);
        var nextDayHold = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/appointments/holds", patient.AccessToken, JsonContent.Create(new
        {
            scheduleId,
            date = nextDay.ToString("yyyy-MM-dd"),
            start = "09:00:00"
        })));
        Assert.Equal("Held", nextDayHold.GetProperty("status").GetString());
        await SendAsync(HttpMethod.Post, $"/api/v1/appointments/{nextDayHold.GetProperty("id").GetGuid()}/release", patient.AccessToken);

        var otherSameDay = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/appointments/holds", other.AccessToken, JsonContent.Create(new
        {
            scheduleId,
            date = date.ToString("yyyy-MM-dd"),
            start = "10:00:00"
        })));
        Assert.Equal("Held", otherSameDay.GetProperty("status").GetString());
        await SendAsync(HttpMethod.Post, $"/api/v1/appointments/{otherSameDay.GetProperty("id").GetGuid()}/release", other.AccessToken);

        var walkIn = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/appointments/walk-ins", doctorAgain.AccessToken, JsonContent.Create(new
        {
            scheduleId,
            date = date.ToString("yyyy-MM-dd"),
            start = "10:30:00",
            patientName = "Walk In Guest"
        })));
        Assert.Equal("Confirmed", walkIn.GetProperty("status").GetString());
        var mine = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/appointments", patient.AccessToken));
        Assert.DoesNotContain("Walk In Guest", mine.ToString());
        var adminList = await (await SendAsync(HttpMethod.Get, $"/api/v1/admin/appointments?from={date:yyyy-MM-dd}&to={date:yyyy-MM-dd}", admin.AccessToken)).Content.ReadAsStringAsync();
        Assert.Contains(approved.GetProperty("reference").GetString()!, adminList);
        Assert.DoesNotContain("Headache", adminList);
        Assert.DoesNotContain("Since morning", adminList);
    }

    private async Task<(Guid ScheduleId, string RowVersion)> PublishScheduleAsync(SessionBody doctor, SessionBody admin, Guid specializationId)
    {
        await ProviderSeed.EnsureDoctorRoleAsync(_client, doctor.AccessToken, admin.AccessToken);
        doctor = await SignInAsync("9000000031");
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/basics", doctor.AccessToken, JsonContent.Create(new
        {
            firstName = "Kabir",
            lastName = "Shah",
            dateOfBirth = "1984-04-04",
            gender = "male",
            email = "kabir@example.com",
            preferredLanguage = "en"
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/professional", doctor.AccessToken, JsonContent.Create(new
        {
            displayName = "Dr. Kabir Shah",
            biography = "Clinic visits",
            yearsOfExperience = 12,
            languages = new[] { "en", "hi" },
            expertise = new[] { "Clinic visits" }
        }));
        await SendAsync(HttpMethod.Post, "/api/v1/doctors/me/qualifications", doctor.AccessToken, JsonContent.Create(new
        {
            name = "MBBS",
            institution = "Grant Medical College",
            graduationYear = 2008
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/specializations", doctor.AccessToken, JsonContent.Create(new
        {
            specializationIds = new[] { specializationId },
            primarySpecializationId = specializationId
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/registration", doctor.AccessToken, JsonContent.Create(new
        {
            number = "MCI-KABIR",
            council = "National Medical Commission",
            state = "Maharashtra",
            registrationType = "permanent",
            registeredOn = "2010-06-01",
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
            name = "Shah Clinic",
            type = "Clinic",
            phoneNumber = "9876503311",
            registrationNumber = "CLN-KABIR",
            establishmentYear = 2014
        }));
        var branched = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/clinics/{clinicId}/branches", doctor.AccessToken, JsonContent.Create(new
        {
            name = "Pune Camp",
            addressLine = "12 MG Road",
            city = "Pune",
            state = "Maharashtra",
            postalCode = "411001",
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
            consultationFee = 500,
            slotDurationMinutes = 30
        })));
        var scheduleId = created.GetProperty("id").GetGuid();
        var rowVersion = created.GetProperty("rowVersion").GetString();
        var days = Enum.GetNames<DayOfWeek>();
        var withHours = await ReadAsync(await SendAsync(HttpMethod.Put, $"/api/v1/schedules/{scheduleId}/hours", doctor.AccessToken, JsonContent.Create(new
        {
            expectedRowVersion = rowVersion,
            periods = days.Select(day => new { day, startsAt = "09:00", endsAt = "11:00" })
        })));
        var published = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/schedules/{scheduleId}/publication", doctor.AccessToken, JsonContent.Create(new
        {
            isPublished = true,
            expectedRowVersion = withHours.GetProperty("rowVersion").GetString()
        })));
        return (scheduleId, published.GetProperty("rowVersion").GetString()!);
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
        return body.GetProperty("slots").EnumerateArray()
            .Where(slot => !slot.TryGetProperty("available", out var available) || available.GetBoolean())
            .Select(slot => slot.GetProperty("start").GetString()!)
            .ToArray();
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

        var body = await response.Content.ReadAsStringAsync();
        Assert.Fail($"{method} {path} returned {(int)response.StatusCode}: {body}");
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
