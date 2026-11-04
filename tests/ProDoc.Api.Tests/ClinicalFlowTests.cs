using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ProDoc.Api.Tests;

[Collection("api")]
public sealed class ClinicalFlowTests(ProDocApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Prescription_lifecycle_enforces_authz_immutability_and_pdf()
    {
        var doctor = await SignInAsync("9000000051");
        var patient = await SignInAsync("9000000052");
        var stranger = await SignInAsync("9000000053");
        var admin = await SignInAsync("9000000001");
        await SendAsync(HttpMethod.Put, "/api/v1/account/profile", patient.AccessToken, JsonContent.Create(new { fullName = "Kavya Iyer", preferredLanguage = "en" }));
        var specializations = await SendAsync(HttpMethod.Get, "/api/v1/specializations", doctor.AccessToken);
        var cardiology = (await specializations.Content.ReadFromJsonAsync<SpecializationBody[]>())!.Single(item => item.Code == "cardiology");
        var (scheduleId, clinicId, branchId, _) = await PublishAsync(doctor, admin, cardiology.Id);
        doctor = await SignInAsync("9000000051");
        await ReadAsync(await SendAsync(HttpMethod.Put, $"/api/v1/clinics/{clinicId}/branches/{branchId}/queue-settings", doctor.AccessToken, JsonContent.Create(new
        {
            tokenPrefix = "C",
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
        var confirmed = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/appointments/{held.GetProperty("id").GetGuid()}/confirm", patient.AccessToken, JsonContent.Create(new { reason = "Cough" })));
        var appointmentId = confirmed.GetProperty("id").GetGuid();
        var qr = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/appointments/{appointmentId}/check-in/qr-token", patient.AccessToken));
        await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/check-ins/qr", doctor.AccessToken, JsonContent.Create(new { token = qr.GetProperty("token").GetString() })));

        var workspace = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/v1/appointments/{appointmentId}/workspace", doctor.AccessToken));
        Assert.Equal("Kavya Iyer", workspace.GetProperty("patientName").GetString());
        var consultation = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/appointments/{appointmentId}/consultations", doctor.AccessToken));
        var consultationId = consultation.GetProperty("id").GetGuid();
        await ReadAsync(await SendAsync(HttpMethod.Put, $"/api/v1/consultations/{consultationId}", doctor.AccessToken, JsonContent.Create(new
        {
            symptoms = "Dry cough",
            clinicalObservations = "Clear chest",
            doctorNotes = "Supportive care",
            advice = "Hydration",
            diagnoses = new[] { new { description = "Viral URI", code = (string?)null, isPrimary = true } },
            complete = true
        })));

        var draft = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/consultations/{consultationId}/prescriptions", doctor.AccessToken));
        var prescriptionId = draft.GetProperty("id").GetGuid();
        await ReadAsync(await SendAsync(HttpMethod.Put, $"/api/v1/prescriptions/{prescriptionId}", doctor.AccessToken, JsonContent.Create(new
        {
            diagnosis = "Viral URI",
            symptoms = "Dry cough",
            advice = "Hydration",
            medicines = new[]
            {
                new
                {
                    medicineName = "Paracetamol",
                    dosage = "500 mg",
                    frequency = "TDS",
                    duration = "3 days",
                    route = "Oral",
                    instructions = "After food"
                }
            }
        })));
        var finalized = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/prescriptions/{prescriptionId}/finalize", doctor.AccessToken));
        Assert.Equal("Finalized", finalized.GetProperty("status").GetString());
        Assert.True(finalized.GetProperty("current").GetProperty("hasPdf").GetBoolean());

        var edit = await SendAsync(HttpMethod.Put, $"/api/v1/prescriptions/{prescriptionId}", doctor.AccessToken, JsonContent.Create(new
        {
            diagnosis = "Changed",
            medicines = new[]
            {
                new { medicineName = "Ibuprofen", dosage = "400 mg", frequency = "BD", duration = "2 days", route = "Oral" }
            }
        }));
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);

        var strangerGet = await SendAsync(HttpMethod.Get, $"/api/v1/prescriptions/{prescriptionId}", stranger.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, strangerGet.StatusCode);
        var adminGet = await SendAsync(HttpMethod.Get, $"/api/v1/prescriptions/{prescriptionId}", admin.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, adminGet.StatusCode);

        var patientRx = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/v1/prescriptions/{prescriptionId}", patient.AccessToken));
        Assert.Equal(prescriptionId, patientRx.GetProperty("id").GetGuid());
        var pdf = await SendAsync(HttpMethod.Get, $"/api/v1/prescriptions/{prescriptionId}/pdf", patient.AccessToken);
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        var pdfBytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.True(pdfBytes.Length > 100);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdfBytes, 0, 4));

        await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/prescriptions/{prescriptionId}/amend", doctor.AccessToken, JsonContent.Create(new { reason = "Dose adjustment" })));
        await ReadAsync(await SendAsync(HttpMethod.Put, $"/api/v1/prescriptions/{prescriptionId}", doctor.AccessToken, JsonContent.Create(new
        {
            diagnosis = "Viral URI",
            medicines = new[]
            {
                new { medicineName = "Paracetamol", dosage = "650 mg", frequency = "TDS", duration = "3 days", route = "Oral" }
            }
        })));
        var amended = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/prescriptions/{prescriptionId}/finalize", doctor.AccessToken));
        Assert.Equal("Amended", amended.GetProperty("status").GetString());
        Assert.Equal(2, amended.GetProperty("currentVersionNumber").GetInt32());

        var records = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/patients/me/medical-records", patient.AccessToken));
        Assert.True(records.GetArrayLength() >= 1);
        var recordId = records[0].GetProperty("id").GetGuid();
        var detail = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/v1/medical-records/{recordId}", patient.AccessToken));
        Assert.True(detail.GetProperty("prescriptions").GetArrayLength() >= 1);

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent("%PDF-1.4\nreport"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "lab.pdf");
        form.Add(new StringContent("Lab report"), "title");
        var uploaded = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/medical-records/{recordId}/documents", patient.AccessToken, form));
        var documentId = uploaded.GetProperty("id").GetGuid();
        var downloaded = await SendAsync(HttpMethod.Get, $"/api/v1/medical-documents/{documentId}/content", patient.AccessToken);
        Assert.Equal(HttpStatusCode.OK, downloaded.StatusCode);
        var strangerDoc = await SendAsync(HttpMethod.Get, $"/api/v1/medical-documents/{documentId}/content", stranger.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, strangerDoc.StatusCode);
    }

    private async Task<(Guid ScheduleId, Guid ClinicId, Guid BranchId, Guid DoctorId)> PublishAsync(SessionBody doctor, SessionBody admin, Guid specializationId)
    {
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/basics", doctor.AccessToken, JsonContent.Create(new
        {
            firstName = "Neel",
            lastName = "Rao",
            dateOfBirth = "1982-03-03",
            gender = "male",
            email = "neel@example.com",
            preferredLanguage = "en"
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/professional", doctor.AccessToken, JsonContent.Create(new
        {
            displayName = "Dr. Neel Rao",
            biography = "General practice",
            yearsOfExperience = 14,
            languages = new[] { "en", "hi" },
            expertise = new[] { "Fever care" }
        }));
        await SendAsync(HttpMethod.Post, "/api/v1/doctors/me/qualifications", doctor.AccessToken, JsonContent.Create(new
        {
            name = "MBBS",
            institution = "CMC",
            graduationYear = 2008
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/specializations", doctor.AccessToken, JsonContent.Create(new
        {
            specializationIds = new[] { specializationId },
            primarySpecializationId = specializationId
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/registration", doctor.AccessToken, JsonContent.Create(new
        {
            number = "MCI-NEEL",
            council = "National Medical Commission",
            state = "Karnataka",
            registrationType = "permanent",
            registeredOn = "2010-01-01",
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
            name = "Rao Clinic",
            type = "Clinic",
            phoneNumber = "9876505511",
            registrationNumber = "CLN-NEEL",
            establishmentYear = 2012
        }));
        var branched = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/clinics/{clinicId}/branches", doctor.AccessToken, JsonContent.Create(new
        {
            name = "Indiranagar",
            addressLine = "12 Main Road",
            city = "Bengaluru",
            state = "Karnataka",
            postalCode = "560038",
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
