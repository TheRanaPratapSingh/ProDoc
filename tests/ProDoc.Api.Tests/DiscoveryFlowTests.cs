using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProDoc.Api.Tests;

[Collection("api")]
public sealed class DiscoveryFlowTests(ProDocApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Published_schedules_are_discoverable_and_slots_skip_breaks_and_closures()
    {
        var doctor = await SignInAsync("9000000021");
        var visitor = await SignInAsync("9000000022");
        var admin = await SignInAsync("9000000001", purpose: "admin");
        var specializations = await SendAsync(HttpMethod.Get, "/api/v1/specializations", doctor.AccessToken);
        var cardiology = (await specializations.Content.ReadFromJsonAsync<SpecializationBody[]>())!.Single(item => item.Code == "cardiology");

        await ProviderSeed.EnsureDoctorRoleAsync(_client, doctor.AccessToken, admin.AccessToken);
        doctor = await SignInAsync("9000000021");
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/basics", doctor.AccessToken, JsonContent.Create(new
        {
            firstName = "Nila",
            lastName = "Desai",
            dateOfBirth = "1986-03-02",
            gender = "female",
            email = "nila@example.com",
            preferredLanguage = "en"
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/professional", doctor.AccessToken, JsonContent.Create(new
        {
            displayName = "Dr. Nila Desai",
            biography = "Heart clinic",
            yearsOfExperience = 14,
            languages = new[] { "en", "hi" },
            expertise = new[] { "Clinic visits" }
        }));
        await SendAsync(HttpMethod.Post, "/api/v1/doctors/me/qualifications", doctor.AccessToken, JsonContent.Create(new
        {
            name = "MBBS",
            institution = "BJ Medical College",
            graduationYear = 2010
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/specializations", doctor.AccessToken, JsonContent.Create(new
        {
            specializationIds = new[] { cardiology.Id },
            primarySpecializationId = cardiology.Id
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/registration", doctor.AccessToken, JsonContent.Create(new
        {
            number = "MCI-NILA",
            council = "National Medical Commission",
            state = "Maharashtra",
            registrationType = "permanent",
            registeredOn = "2012-06-01",
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
            name = "Desai Heart Clinic",
            type = "Clinic",
            phoneNumber = "9876502211",
            registrationNumber = "CLN-NILA",
            establishmentYear = 2016
        }));
        var branched = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/clinics/{clinicId}/branches", doctor.AccessToken, JsonContent.Create(new
        {
            name = "Nashik Road",
            addressLine = "8 College Road",
            city = "Nashik",
            state = "Maharashtra",
            postalCode = "422001",
            latitude = 19.9975,
            longitude = 73.7898,
            isPrimary = true
        })));
        var branchId = branched.GetProperty("branches").EnumerateArray().Single().GetProperty("id").GetGuid();
        await UploadAsync(doctor.AccessToken, $"/api/v1/clinics/{clinicId}/documents", "clinic.pdf", "application/pdf", "%PDF-1.4\n"u8.ToArray(), "ClinicRegistration");
        await SendAsync(HttpMethod.Post, $"/api/v1/clinics/{clinicId}/submit", doctor.AccessToken);
        await SendAsync(HttpMethod.Post, $"/api/v1/admin/verifications/clinics/{clinicId}", admin.AccessToken, JsonContent.Create(new { decision = "Approve" }));
        await SendAsync(HttpMethod.Post, "/api/v1/doctors/me/affiliations", doctor.AccessToken, JsonContent.Create(new
        {
            clinicId,
            branchId,
            kind = "Independent"
        }));

        var listedBeforeSchedule = await SendAsync(HttpMethod.Get, "/api/v1/doctors?query=Nila", visitor.AccessToken);
        Assert.Empty((await listedBeforeSchedule.Content.ReadFromJsonAsync<JsonElement[]>())!);
        var hidden = await SendAsync(HttpMethod.Get, "/api/v1/discovery?query=Nila", visitor.AccessToken);
        Assert.Empty((await hidden.Content.ReadFromJsonAsync<JsonElement[]>())!);

        var stranger = await SendAsync(HttpMethod.Post, "/api/v1/schedules", visitor.AccessToken, JsonContent.Create(new
        {
            doctorId,
            clinicId,
            branchId,
            consultationFee = 100,
            slotDurationMinutes = 15
        }));
        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);

        var created = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/schedules", doctor.AccessToken, JsonContent.Create(new
        {
            clinicId,
            branchId,
            consultationFee = 500,
            slotDurationMinutes = 30
        })));
        var scheduleId = created.GetProperty("id").GetGuid();
        var days = Enum.GetNames<DayOfWeek>();
        await SendAsync(HttpMethod.Put, $"/api/v1/schedules/{scheduleId}/hours", doctor.AccessToken, JsonContent.Create(new
        {
            periods = days.Select(day => new { day, startsAt = "09:00", endsAt = "11:00" })
        }));
        await SendAsync(HttpMethod.Put, $"/api/v1/schedules/{scheduleId}/breaks", doctor.AccessToken, JsonContent.Create(new
        {
            periods = days.Select(day => new { day, startsAt = "09:30", endsAt = "10:00" })
        }));
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var closed = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/schedules/{scheduleId}/closures", doctor.AccessToken, JsonContent.Create(new
        {
            date = tomorrow.ToString("yyyy-MM-dd"),
            reason = "Camp day"
        })));
        await SendAsync(HttpMethod.Post, $"/api/v1/schedules/{scheduleId}/publication", doctor.AccessToken, JsonContent.Create(new { isPublished = true }));

        var listedAfterPublish = await SendAsync(HttpMethod.Get, "/api/v1/doctors?query=Nil", visitor.AccessToken);
        Assert.Equal(doctorId, (await listedAfterPublish.Content.ReadFromJsonAsync<JsonElement[]>())!.Single().GetProperty("id").GetGuid());

        var unpublishedSlots = tomorrow;
        var closedSlots = await SendAsync(HttpMethod.Get, $"/api/v1/schedules/{scheduleId}/slots?date={unpublishedSlots:yyyy-MM-dd}", visitor.AccessToken);
        Assert.Empty(await SlotsAsync(closedSlots));

        var closureId = closed.GetProperty("closures").EnumerateArray().Single().GetProperty("id").GetGuid();
        await SendAsync(HttpMethod.Delete, $"/api/v1/schedules/{scheduleId}/closures/{closureId}", doctor.AccessToken);
        var open = await SlotsAsync(await SendAsync(HttpMethod.Get, $"/api/v1/schedules/{scheduleId}/slots?date={tomorrow:yyyy-MM-dd}", visitor.AccessToken));
        Assert.Equal(["09:00:00", "10:00:00", "10:30:00"], open);

        var places = await ReadAsync(await SendAsync(
            HttpMethod.Get,
            $"/api/v1/discovery?query=Desai&city=Nashik&specializationId={cardiology.Id}&maxFee=600&latitude=19.9975&longitude=73.7898&radiusKm=10",
            visitor.AccessToken));
        var place = places.EnumerateArray().Single();
        Assert.Equal(scheduleId, place.GetProperty("scheduleId").GetGuid());
        Assert.Equal(500, place.GetProperty("consultationFee").GetDecimal());
        Assert.Equal("Nashik", place.GetProperty("city").GetString());
        Assert.True(place.GetProperty("distanceKm").GetDouble() < 1);

        var tooFar = await SendAsync(HttpMethod.Get, "/api/v1/discovery?query=Nila&latitude=28.6&longitude=77.2&radiusKm=10", visitor.AccessToken);
        Assert.Empty((await tooFar.Content.ReadFromJsonAsync<JsonElement[]>())!);
        var tooDear = await SendAsync(HttpMethod.Get, "/api/v1/discovery?query=Nila&maxFee=100", visitor.AccessToken);
        Assert.Empty((await tooDear.Content.ReadFromJsonAsync<JsonElement[]>())!);

        var profile = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/v1/discovery/doctors/{doctorId}", visitor.AccessToken));
        Assert.Equal("Dr. Nila Desai", profile.GetProperty("displayName").GetString());
        Assert.Equal(scheduleId, profile.GetProperty("practices").EnumerateArray().Single().GetProperty("scheduleId").GetGuid());

        var blocked = await SendAsync(HttpMethod.Put, $"/api/v1/schedules/{scheduleId}", visitor.AccessToken, JsonContent.Create(new
        {
            consultationFee = 10,
            slotDurationMinutes = 15
        }));
        Assert.Equal(HttpStatusCode.NotFound, blocked.StatusCode);

        await SendAsync(HttpMethod.Post, $"/api/v1/schedules/{scheduleId}/publication", doctor.AccessToken, JsonContent.Create(new { isPublished = false }));
        var withdrawn = await SendAsync(HttpMethod.Get, "/api/v1/discovery?query=Nila", visitor.AccessToken);
        Assert.Empty((await withdrawn.Content.ReadFromJsonAsync<JsonElement[]>())!);
        var missingProfile = await SendAsync(HttpMethod.Get, $"/api/v1/discovery/doctors/{doctorId}", visitor.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, missingProfile.StatusCode);
    }

    private async Task<SessionBody> SignInAsync(string phone, string? purpose = null)
    {
        var otpResponse = await _client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phoneNumber = phone, purpose });
        var otpBody = await otpResponse.Content.ReadAsStringAsync();
        Assert.True(otpResponse.IsSuccessStatusCode, otpBody);
        var otp = JsonSerializer.Deserialize<OtpBody>(otpBody, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var verifyResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/otp/verify",
            new { phoneNumber = phone, code = otp!.DevelopmentCode, deviceName = "integration-test", purpose });
        var verifyBody = await verifyResponse.Content.ReadAsStringAsync();
        Assert.True(verifyResponse.IsSuccessStatusCode, verifyBody);
        return JsonSerializer.Deserialize<SessionBody>(verifyBody, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private async Task<HttpResponseMessage> UploadAsync(
        string accessToken,
        string path,
        string fileName,
        string contentType,
        byte[] bytes,
        string category)
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
        if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest)
        {
            return response;
        }

        var body = await response.Content.ReadAsStringAsync();
        Assert.Fail($"{method} {path} returned {(int)response.StatusCode}: {body}");
        return response;
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.Clone();
    }

    private static async Task<string[]> SlotsAsync(HttpResponseMessage response)
    {
        var body = await ReadAsync(response);
        return body.GetProperty("slots").EnumerateArray().Select(slot => slot.GetProperty("start").GetString()!).ToArray();
    }

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    private sealed record OtpBody(string? DevelopmentCode);

    private sealed record SessionBody(string AccessToken, string RefreshToken);

    private sealed record SpecializationBody(Guid Id, string Code);
}
