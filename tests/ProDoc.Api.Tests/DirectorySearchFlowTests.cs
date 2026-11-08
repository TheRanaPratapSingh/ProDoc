using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProDoc.Api.Tests;

[Collection("api")]
public sealed class DirectorySearchFlowTests(ProDocApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Directory_search_matches_partial_doctor_and_clinic_names_case_insensitively()
    {
        var doctor = await SignInAsync("9000000031");
        var visitor = await SignInAsync("9000000032");
        var admin = await SignInAsync("9000000001", purpose: "admin");
        var specializations = await SendAsync(HttpMethod.Get, "/api/v1/specializations", doctor.AccessToken);
        var general = (await specializations.Content.ReadFromJsonAsync<SpecializationBody[]>())!.Single(item => item.Code == "general-medicine");

        await ProviderSeed.EnsureDoctorRoleAsync(_client, doctor.AccessToken, admin.AccessToken);
        doctor = await SignInAsync("9000000031");
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/basics", doctor.AccessToken, JsonContent.Create(new
        {
            firstName = "Akash",
            lastName = "Verma",
            dateOfBirth = "1988-04-12",
            gender = "male",
            email = "akash.search@example.com",
            preferredLanguage = "en"
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/professional", doctor.AccessToken, JsonContent.Create(new
        {
            displayName = "Akash",
            biography = "General physician",
            yearsOfExperience = 8,
            languages = new[] { "en", "hi" },
            expertise = new[] { "Fever" }
        }));
        await SendAsync(HttpMethod.Post, "/api/v1/doctors/me/qualifications", doctor.AccessToken, JsonContent.Create(new
        {
            name = "MBBS",
            institution = "AIIMS",
            graduationYear = 2014
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/specializations", doctor.AccessToken, JsonContent.Create(new
        {
            specializationIds = new[] { general.Id },
            primarySpecializationId = general.Id
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/registration", doctor.AccessToken, JsonContent.Create(new
        {
            number = "MCI-AKASH-SEARCH",
            council = "National Medical Commission",
            state = "Uttarakhand",
            registrationType = "permanent",
            registeredOn = "2015-01-01",
            expiresOn = "2032-01-01"
        }));
        await UploadAsync(doctor.AccessToken, "/api/v1/doctors/me/documents", "photo.png", "image/png", Png, "ProfilePhoto");
        await UploadAsync(doctor.AccessToken, "/api/v1/doctors/me/documents", "registration.pdf", "application/pdf", "%PDF-1.4\n"u8.ToArray(), "RegistrationCertificate");
        var submitted = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/doctors/me/submit", doctor.AccessToken));
        var doctorId = submitted.GetProperty("id").GetGuid();
        var doctorApproved = await SendAsync(HttpMethod.Post, $"/api/v1/admin/verifications/doctors/{doctorId}", admin.AccessToken, JsonContent.Create(new { decision = "Approve" }));
        Assert.Equal(HttpStatusCode.OK, doctorApproved.StatusCode);

        var ownerUserId = await ProviderSeed.GetUserIdAsync(_client, doctor.AccessToken);
        var clinicId = await ProviderSeed.CreateClinicOwnedByAsync(_client, admin.AccessToken, ownerUserId);
        await SendAsync(HttpMethod.Put, $"/api/v1/clinics/{clinicId}", doctor.AccessToken, JsonContent.Create(new
        {
            name = "Pratap clinic",
            type = "Clinic",
            phoneNumber = "9876503311",
            registrationNumber = "CLN-PRATAP",
            establishmentYear = 2019
        }));
        var branched = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/clinics/{clinicId}/branches", doctor.AccessToken, JsonContent.Create(new
        {
            name = "Pratap Main",
            addressLine = "12 Market Road",
            city = "Jaspur",
            state = "Uttarakhand",
            postalCode = "244712",
            isPrimary = true
        })));
        var branchId = branched.GetProperty("branches").EnumerateArray().Single().GetProperty("id").GetGuid();
        await UploadAsync(doctor.AccessToken, $"/api/v1/clinics/{clinicId}/documents", "clinic.pdf", "application/pdf", "%PDF-1.4\n"u8.ToArray(), "ClinicRegistration");
        await SendAsync(HttpMethod.Post, $"/api/v1/clinics/{clinicId}/submit", doctor.AccessToken);
        var clinicApproved = await SendAsync(HttpMethod.Post, $"/api/v1/admin/verifications/clinics/{clinicId}", admin.AccessToken, JsonContent.Create(new { decision = "Approve" }));
        Assert.Equal(HttpStatusCode.OK, clinicApproved.StatusCode);
        await SendAsync(HttpMethod.Post, "/api/v1/doctors/me/affiliations", doctor.AccessToken, JsonContent.Create(new
        {
            clinicId,
            branchId,
            kind = "Independent"
        }));

        Assert.Empty(await SearchDoctorsAsync(visitor.AccessToken, "Aka"));

        var created = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/schedules", doctor.AccessToken, JsonContent.Create(new
        {
            clinicId,
            branchId,
            consultationFee = 400,
            slotDurationMinutes = 15
        })));
        var scheduleId = created.GetProperty("id").GetGuid();
        var hours = await SendAsync(HttpMethod.Put, $"/api/v1/schedules/{scheduleId}/hours", doctor.AccessToken, JsonContent.Create(new
        {
            periods = Enum.GetNames<DayOfWeek>().Select(day => new { day, startsAt = "09:00", endsAt = "12:00" })
        }));
        Assert.Equal(HttpStatusCode.OK, hours.StatusCode);
        var published = await SendAsync(HttpMethod.Post, $"/api/v1/schedules/{scheduleId}/publication", doctor.AccessToken, JsonContent.Create(new { isPublished = true }));
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);

        foreach (var query in new[] { "A", "AK", "Aka", "akash", "  Aka  " })
        {
            var cards = await SearchDoctorsAsync(visitor.AccessToken, query);
            var match = Assert.Single(cards);
            Assert.Equal(doctorId, match.GetProperty("id").GetGuid());
            Assert.Equal("Akash", match.GetProperty("displayName").GetString());
            Assert.Equal("MBBS", match.GetProperty("qualification").GetString());
            Assert.True(match.GetProperty("isVerified").GetBoolean());
        }

        foreach (var query in new[] { "P", "Pra", "Pratap", "pratap main", "jaspur" })
        {
            var cards = await SearchClinicsAsync(visitor.AccessToken, query);
            var match = Assert.Single(cards, item => item.GetProperty("id").GetGuid() == clinicId);
            Assert.Equal("Pratap clinic", match.GetProperty("name").GetString());
            Assert.Equal("Pratap Main", match.GetProperty("branchName").GetString());
            Assert.Contains("Akash", match.GetProperty("associatedDoctors").EnumerateArray().Select(item => item.GetString()));
        }

        Assert.Empty(await SearchDoctorsAsync(visitor.AccessToken, "Zzz"));
        Assert.Empty(await SearchClinicsAsync(visitor.AccessToken, "Zzz"));
    }

    private async Task<JsonElement[]> SearchDoctorsAsync(string accessToken, string query)
    {
        var response = await SendAsync(HttpMethod.Get, $"/api/v1/doctors?query={Uri.EscapeDataString(query)}", accessToken);
        return (await response.Content.ReadFromJsonAsync<JsonElement[]>())!;
    }

    private async Task<JsonElement[]> SearchClinicsAsync(string accessToken, string query)
    {
        var response = await SendAsync(HttpMethod.Get, $"/api/v1/clinics?query={Uri.EscapeDataString(query)}", accessToken);
        return (await response.Content.ReadFromJsonAsync<JsonElement[]>())!;
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

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    private sealed record OtpBody(string? DevelopmentCode);

    private sealed record SessionBody(string AccessToken, string RefreshToken);

    private sealed record SpecializationBody(Guid Id, string Code);
}
