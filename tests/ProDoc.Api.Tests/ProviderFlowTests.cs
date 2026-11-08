using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProDoc.Api.Tests;

[Collection("api")]
public sealed class ProviderFlowTests(ProDocApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Approved_doctors_and_clinics_are_public_and_credential_changes_remove_them()
    {
        var patient = await SignInAsync("9000000011");
        var other = await SignInAsync("9000000012");
        var admin = await SignInAsync("9000000001", purpose: "admin");
        var missing = await SendAsync(HttpMethod.Get, "/api/v1/doctors/me", patient.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var specializations = await SendAsync(HttpMethod.Get, "/api/v1/specializations", patient.AccessToken);
        var catalog = await specializations.Content.ReadFromJsonAsync<SpecializationBody[]>();
        var cardiology = catalog!.Single(item => item.Code == "cardiology");

        await ProviderSeed.EnsureDoctorRoleAsync(_client, patient.AccessToken, admin.AccessToken);
        patient = await SignInAsync("9000000011");
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/basics", patient.AccessToken, JsonContent.Create(new
        {
            firstName = "Asha",
            lastName = "Rao",
            dateOfBirth = "1990-01-15",
            gender = "female",
            email = "asha@example.com",
            preferredLanguage = "en"
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/professional", patient.AccessToken, JsonContent.Create(new
        {
            displayName = "Dr. Asha Rao",
            biography = "Cardiologist",
            yearsOfExperience = 11,
            languages = new[] { "en", "hi" },
            expertise = new[] { "Heart failure" }
        }));
        await SendAsync(HttpMethod.Post, "/api/v1/doctors/me/qualifications", patient.AccessToken, JsonContent.Create(new
        {
            name = "MBBS",
            institution = "Grant Medical College",
            graduationYear = 2012
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/specializations", patient.AccessToken, JsonContent.Create(new
        {
            specializationIds = new[] { cardiology.Id },
            primarySpecializationId = cardiology.Id
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/registration", patient.AccessToken, JsonContent.Create(new
        {
            number = "MCI-100",
            council = "National Medical Commission",
            state = "Maharashtra",
            registrationType = "permanent",
            registeredOn = "2014-05-01",
            expiresOn = "2030-05-01"
        }));

        var rejected = await UploadAsync(patient.AccessToken, "/api/v1/doctors/me/documents", "notes.txt", "text/plain", "hello"u8.ToArray(), "Other");
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        var withPhoto = await ReadAsync(await UploadAsync(
            patient.AccessToken,
            "/api/v1/doctors/me/documents",
            "photo.png",
            "image/png",
            Png,
            "ProfilePhoto"));
        var withCertificate = await ReadAsync(await UploadAsync(
            patient.AccessToken,
            "/api/v1/doctors/me/documents",
            "registration.pdf",
            "application/pdf",
            "%PDF-1.4\n"u8.ToArray(),
            "RegistrationCertificate"));
        Assert.Equal("signature-allow-list", withCertificate.GetProperty("documents").EnumerateArray().First().GetProperty("scanEngine").GetString());
        Assert.Contains("not configured", withCertificate.GetProperty("documents").EnumerateArray().First().GetProperty("scanDetail").GetString(), StringComparison.OrdinalIgnoreCase);

        var hidden = await SendAsync(HttpMethod.Get, "/api/v1/doctors?query=Asha", patient.AccessToken);
        Assert.Empty((await hidden.Content.ReadFromJsonAsync<JsonElement[]>())!);

        var submitted = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/doctors/me/submit", patient.AccessToken));
        var doctorId = submitted.GetProperty("id").GetGuid();
        Assert.Equal("Submitted", submitted.GetProperty("status").GetString());
        Assert.StartsWith("DOC-", submitted.GetProperty("reference").GetString());
        Assert.False(submitted.GetProperty("isPubliclyBookable").GetBoolean());

        var forbidden = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/admin/verifications/doctors/{doctorId}",
            patient.AccessToken,
            JsonContent.Create(new { decision = "Approve" }));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var queue = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/admin/verifications?kind=doctor", admin.AccessToken));
        Assert.Contains(queue.EnumerateArray(), item => item.GetProperty("id").GetGuid() == doctorId);

        var approved = await ReadAsync(await SendAsync(
            HttpMethod.Post,
            $"/api/v1/admin/verifications/doctors/{doctorId}",
            admin.AccessToken,
            JsonContent.Create(new { decision = "Approve" })));
        Assert.Equal("Approved", approved.GetProperty("status").GetString());
        Assert.True(approved.GetProperty("isPubliclyBookable").GetBoolean());

        var listedBeforePlace = await SendAsync(HttpMethod.Get, "/api/v1/doctors?query=Asha", other.AccessToken);
        Assert.Empty((await listedBeforePlace.Content.ReadFromJsonAsync<JsonElement[]>())!);

        var photoId = withPhoto.GetProperty("documents").EnumerateArray()
            .Single(item => item.GetProperty("category").GetString() == "ProfilePhoto")
            .GetProperty("id").GetGuid();
        var certificateId = withCertificate.GetProperty("documents").EnumerateArray()
            .Single(item => item.GetProperty("category").GetString() == "RegistrationCertificate")
            .GetProperty("id").GetGuid();
        var photo = await SendAsync(HttpMethod.Get, $"/api/v1/provider-documents/{photoId}", other.AccessToken);
        var certificate = await SendAsync(HttpMethod.Get, $"/api/v1/provider-documents/{certificateId}", other.AccessToken);
        var ownCertificate = await SendAsync(HttpMethod.Get, $"/api/v1/provider-documents/{certificateId}", patient.AccessToken);
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal("image/png", photo.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, certificate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownCertificate.StatusCode);
        Assert.DoesNotContain("provider-documents", photo.Content.Headers.ContentDisposition?.FileName ?? "photo.png");

        var ownerUserId = await ProviderSeed.GetUserIdAsync(_client, patient.AccessToken);
        var clinicId = await ProviderSeed.CreateClinicOwnedByAsync(_client, admin.AccessToken, ownerUserId);
        await SendAsync(HttpMethod.Put, $"/api/v1/clinics/{clinicId}", patient.AccessToken, JsonContent.Create(new
        {
            name = "Rao Clinic",
            type = "IndividualPractice",
            phoneNumber = "9876500099",
            establishmentYear = 2018
        }));
        await SendAsync(HttpMethod.Post, $"/api/v1/clinics/{clinicId}/branches", patient.AccessToken, JsonContent.Create(new
        {
            name = "Main",
            addressLine = "12 Lake Road",
            city = "Pune",
            state = "Maharashtra",
            postalCode = "411001",
            isPrimary = true
        }));
        var branched = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/clinics/{clinicId}/branches", patient.AccessToken, JsonContent.Create(new
        {
            name = "Camp",
            addressLine = "4 MG Road",
            city = "Pune",
            state = "Maharashtra",
            postalCode = "411001",
            latitude = 18.52,
            longitude = 73.85,
            isPrimary = false
        })));
        Assert.Equal(2, branched.GetProperty("branches").GetArrayLength());
        await SendAsync(HttpMethod.Post, $"/api/v1/clinics/{clinicId}/submit", patient.AccessToken);
        await SendAsync(
            HttpMethod.Post,
            $"/api/v1/admin/verifications/clinics/{clinicId}",
            admin.AccessToken,
            JsonContent.Create(new { decision = "Approve" }));

        var branchId = branched.GetProperty("branches").EnumerateArray().First().GetProperty("id").GetGuid();
        await SendAsync(HttpMethod.Post, "/api/v1/doctors/me/affiliations", patient.AccessToken, JsonContent.Create(new
        {
            clinicId,
            branchId,
            kind = "Independent"
        }));
        var schedule = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/schedules", patient.AccessToken, JsonContent.Create(new
        {
            clinicId,
            branchId,
            consultationFee = 500,
            slotDurationMinutes = 15
        })));
        var scheduleId = schedule.GetProperty("id").GetGuid();
        await SendAsync(HttpMethod.Put, $"/api/v1/schedules/{scheduleId}/hours", patient.AccessToken, JsonContent.Create(new
        {
            periods = Enum.GetNames<DayOfWeek>().Select(day => new { day, startsAt = "09:00", endsAt = "12:00" })
        }));
        await SendAsync(HttpMethod.Post, $"/api/v1/schedules/{scheduleId}/publication", patient.AccessToken, JsonContent.Create(new { isPublished = true }));

        var listed = await SendAsync(HttpMethod.Get, "/api/v1/doctors?query=Ash", other.AccessToken);
        var cards = (await listed.Content.ReadFromJsonAsync<JsonElement[]>())!;
        Assert.Equal(doctorId, cards.Single().GetProperty("id").GetGuid());
        Assert.Equal("MBBS", cards.Single().GetProperty("qualification").GetString());

        var secondClinicId = await ProviderSeed.CreateClinicOwnedByAsync(_client, admin.AccessToken, ownerUserId);
        await SendAsync(HttpMethod.Put, $"/api/v1/clinics/{secondClinicId}", patient.AccessToken, JsonContent.Create(new
        {
            name = "Camp Hospital",
            type = "Hospital",
            phoneNumber = "9876500098",
            registrationNumber = "HOS-22",
            establishmentYear = 2001
        }));
        await SendAsync(HttpMethod.Post, $"/api/v1/clinics/{secondClinicId}/branches", patient.AccessToken, JsonContent.Create(new
        {
            name = "Hospital",
            addressLine = "1 Station Road",
            city = "Pune",
            state = "Maharashtra",
            postalCode = "411001",
            isPrimary = true
        }));
        await UploadAsync(patient.AccessToken, $"/api/v1/clinics/{secondClinicId}/documents", "hospital.pdf", "application/pdf", "%PDF-1.4\n"u8.ToArray(), "ClinicRegistration");
        await SendAsync(HttpMethod.Post, $"/api/v1/clinics/{secondClinicId}/submit", patient.AccessToken);
        await SendAsync(
            HttpMethod.Post,
            $"/api/v1/admin/verifications/clinics/{secondClinicId}",
            admin.AccessToken,
            JsonContent.Create(new { decision = "Approve" }));
        var linked = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/doctors/me/affiliations", patient.AccessToken, JsonContent.Create(new
        {
            clinicId = secondClinicId,
            kind = "Hospital"
        })));
        Assert.Equal(2, linked.GetProperty("affiliations").GetArrayLength());

        var clinics = await SendAsync(HttpMethod.Get, "/api/v1/clinics?query=Pune", other.AccessToken);
        Assert.Equal(2, (await clinics.Content.ReadFromJsonAsync<JsonElement[]>())!.Length);
        var profile = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/v1/doctors/{doctorId}", other.AccessToken));
        Assert.Equal(2, profile.GetProperty("clinics").GetArrayLength());

        var refreshed = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = patient.RefreshToken });
        var session = await refreshed.Content.ReadFromJsonAsync<SessionBody>();
        Assert.Contains("Doctor", session!.Account.Roles);
        Assert.Contains("ClinicAdmin", session.Account.Roles);

        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/registration", session.AccessToken, JsonContent.Create(new
        {
            number = "MCI-200",
            council = "National Medical Commission",
            state = "Maharashtra",
            registrationType = "permanent",
            registeredOn = "2014-05-01",
            expiresOn = "2030-05-01"
        }));
        var removed = await SendAsync(HttpMethod.Get, "/api/v1/doctors?query=Asha", other.AccessToken);
        Assert.Empty((await removed.Content.ReadFromJsonAsync<JsonElement[]>())!);
        var unavailable = await SendAsync(HttpMethod.Get, $"/api/v1/doctors/{doctorId}", other.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, unavailable.StatusCode);
    }

    private async Task<SessionBody> SignInAsync(string phone, string? purpose = null)
    {
        var otpResponse = await _client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phoneNumber = phone, purpose });
        var otp = await otpResponse.Content.ReadFromJsonAsync<OtpBody>();
        var verifyResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/otp/verify",
            new { phoneNumber = phone, code = otp!.DevelopmentCode, deviceName = "integration-test", purpose });
        var session = await verifyResponse.Content.ReadFromJsonAsync<SessionBody>();
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        return session!;
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

    private sealed record SessionBody(string AccessToken, string RefreshToken, AccountBody Account);

    private sealed record AccountBody(IReadOnlyList<string> Roles);

    private sealed record SpecializationBody(Guid Id, string Code);
}
