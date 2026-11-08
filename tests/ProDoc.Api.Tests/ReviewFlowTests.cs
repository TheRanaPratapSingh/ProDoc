using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProDoc.Api.Tests;

[Collection("api")]
public sealed class ReviewFlowTests(ProDocApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Verified_reviews_enforce_eligibility_duplicates_ratings_responses_and_moderation()
    {
        var doctor = await SignInAsync("9000000101");
        var patient = await SignInAsync("9000000102");
        var stranger = await SignInAsync("9000000103");
        var admin = await SignInAsync("9000000001", purpose: "admin");
        await SendAsync(HttpMethod.Put, "/api/v1/account/profile", patient.AccessToken, JsonContent.Create(new
        {
            fullName = "Riya Sen",
            preferredLanguage = "en"
        }));

        var specializations = await SendAsync(HttpMethod.Get, "/api/v1/specializations", doctor.AccessToken);
        var cardiology = (await specializations.Content.ReadFromJsonAsync<SpecializationBody[]>())!.Single(item => item.Code == "cardiology");
        var (scheduleId, clinicId, branchId, _) = await PublishAsync(doctor, admin, cardiology.Id);
        doctor = await SignInAsync("9000000101");
        await ReadAsync(await SendAsync(HttpMethod.Put, $"/api/v1/clinics/{clinicId}/branches/{branchId}/queue-settings", doctor.AccessToken, JsonContent.Create(new
        {
            tokenPrefix = "R",
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
        var confirmed = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/appointments/{held.GetProperty("id").GetGuid()}/confirm", patient.AccessToken, JsonContent.Create(new { reason = "Review visit" })));
        var appointmentId = confirmed.GetProperty("id").GetGuid();
        var qr = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/appointments/{appointmentId}/check-in/qr-token", patient.AccessToken));
        var checkedIn = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/check-ins/qr", doctor.AccessToken, JsonContent.Create(new { token = qr.GetProperty("token").GetString() })));
        await SendAsync(HttpMethod.Post, $"/api/v1/queue-entries/{checkedIn.GetProperty("id").GetGuid()}/call", doctor.AccessToken);
        await SendAsync(HttpMethod.Post, $"/api/v1/queue-entries/{checkedIn.GetProperty("id").GetGuid()}/start-consultation", doctor.AccessToken);
        await SendAsync(HttpMethod.Post, $"/api/v1/queue-entries/{checkedIn.GetProperty("id").GetGuid()}/complete-consultation", doctor.AccessToken);

        var eligible = await ReadArrayAsync(await SendAsync(HttpMethod.Get, "/api/v1/reviews/eligible", patient.AccessToken));
        Assert.Contains(eligible, item => item.GetProperty("appointmentId").GetGuid() == appointmentId);

        var beforeComplete = await SignInAsync("9000000104");
        var notEligible = await SendAsync(HttpMethod.Post, "/api/v1/reviews", beforeComplete.AccessToken, JsonContent.Create(new
        {
            appointmentId,
            targetKind = "Doctor",
            overallRating = 5
        }));
        Assert.Equal(HttpStatusCode.BadRequest, notEligible.StatusCode);

        var emptyReview = await SendAsync(HttpMethod.Post, "/api/v1/reviews", patient.AccessToken, JsonContent.Create(new
        {
            appointmentId,
            targetKind = "Doctor",
            body = "   "
        }));
        Assert.Equal(HttpStatusCode.BadRequest, emptyReview.StatusCode);

        var created = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/reviews", patient.AccessToken, JsonContent.Create(new
        {
            appointmentId,
            targetKind = "Doctor",
            overallRating = 5,
            communicationRating = 5,
            staffBehaviourRating = 5,
            consultationRating = 4,
            waitingTimeRating = 4,
            body = "Clear explanation and verified visit."
        })));
        Assert.True(created.GetProperty("isVerifiedVisit").GetBoolean());
        Assert.Equal("Published", created.GetProperty("status").GetString());
        Assert.Equal(4, created.GetProperty("consultationRating").GetInt32());
        var reviewId = created.GetProperty("id").GetGuid();

        var duplicate = await SendAsync(HttpMethod.Post, "/api/v1/reviews", patient.AccessToken, JsonContent.Create(new
        {
            appointmentId,
            targetKind = "Doctor",
            overallRating = 4
        }));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var clinicOnlyComment = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/reviews", patient.AccessToken, JsonContent.Create(new
        {
            appointmentId,
            targetKind = "Clinic",
            body = "Clean waiting area and helpful front desk."
        })));
        Assert.Equal(JsonValueKind.Null, clinicOnlyComment.GetProperty("overallRating").ValueKind);
        Assert.Equal("Clinic", clinicOnlyComment.GetProperty("targetKind").GetString());

        var summary = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/v1/doctors/{created.GetProperty("doctorId").GetGuid()}/rating-summary", patient.AccessToken));
        Assert.True(summary.GetProperty("averageIsPublic").GetBoolean());
        Assert.Equal(5m, summary.GetProperty("averageOverall").GetDecimal());
        Assert.Equal(1, summary.GetProperty("publishedReviewCount").GetInt32());

        var responded = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/responses", doctor.AccessToken, JsonContent.Create(new
        {
            body = "Thank you for visiting. Please follow the advice discussed."
        })));
        Assert.Single(responded.GetProperty("responses").EnumerateArray());

        var secondResponse = await SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/responses", doctor.AccessToken, JsonContent.Create(new
        {
            body = "Another response"
        }));
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);

        var report = await SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/reports", stranger.AccessToken, JsonContent.Create(new
        {
            reason = "Contains personal medical details"
        }));
        Assert.Equal(HttpStatusCode.NoContent, report.StatusCode);

        var moderated = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/admin/reviews/{reviewId}/moderate", admin.AccessToken, JsonContent.Create(new
        {
            status = "Hidden",
            reason = "Temporarily hidden pending review"
        })));
        Assert.Equal("Hidden", moderated.GetProperty("status").GetString());

        var publicList = await ReadArrayAsync(await SendAsync(HttpMethod.Get, $"/api/v1/doctors/{created.GetProperty("doctorId").GetGuid()}/reviews", stranger.AccessToken));
        Assert.DoesNotContain(publicList, item => item.GetProperty("id").GetGuid() == reviewId);

        var feedback = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/feedback", patient.AccessToken, JsonContent.Create(new
        {
            category = "BookingExperience",
            rating = 5,
            body = "Booking flow was clear and private feedback stays separate.",
            appointmentId
        })));
        Assert.Equal("BookingExperience", feedback.GetProperty("category").GetString());
    }

    private async Task<(Guid ScheduleId, Guid ClinicId, Guid BranchId, Guid DoctorId)> PublishAsync(SessionBody doctor, SessionBody admin, Guid specializationId)
    {
        await ProviderSeed.EnsureDoctorRoleAsync(_client, doctor.AccessToken, admin.AccessToken);
        doctor = await SignInAsync("9000000101");
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/basics", doctor.AccessToken, JsonContent.Create(new
        {
            firstName = "Ravi",
            lastName = "Mehta",
            dateOfBirth = "1982-01-01",
            gender = "male",
            email = "ravi@example.com",
            preferredLanguage = "en"
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/professional", doctor.AccessToken, JsonContent.Create(new
        {
            displayName = "Dr. Ravi Mehta",
            biography = "Review clinic",
            yearsOfExperience = 10,
            languages = new[] { "en" },
            expertise = new[] { "General" }
        }));
        await SendAsync(HttpMethod.Post, "/api/v1/doctors/me/qualifications", doctor.AccessToken, JsonContent.Create(new
        {
            name = "MBBS",
            institution = "Grant",
            graduationYear = 2008
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/specializations", doctor.AccessToken, JsonContent.Create(new
        {
            specializationIds = new[] { specializationId },
            primarySpecializationId = specializationId
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/registration", doctor.AccessToken, JsonContent.Create(new
        {
            number = "MCI-REV",
            council = "National Medical Commission",
            state = "Maharashtra",
            registrationType = "permanent",
            registeredOn = "2010-01-01",
            expiresOn = "2030-01-01"
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
            name = "Mehta Clinic",
            type = "Clinic",
            phoneNumber = "9876501011",
            registrationNumber = "CLN-REV",
            establishmentYear = 2015
        }));
        var branched = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/clinics/{clinicId}/branches", doctor.AccessToken, JsonContent.Create(new
        {
            name = "Bandra",
            addressLine = "11 Hill Road",
            city = "Mumbai",
            state = "Maharashtra",
            postalCode = "400050",
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
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new Xunit.Sdk.XunitException($"{(int)response.StatusCode} {response.ReasonPhrase}: {body}");
        }

        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static async Task<JsonElement[]> ReadArrayAsync(HttpResponseMessage response)
    {
        var root = await ReadAsync(response);
        return root.EnumerateArray().Select(item => item.Clone()).ToArray();
    }

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    private sealed record OtpBody(string? DevelopmentCode);

    private sealed record SessionBody(string AccessToken, string RefreshToken);

    private sealed record SpecializationBody(Guid Id, string Code);
}
