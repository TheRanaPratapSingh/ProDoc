using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProDoc.Api.Tests;

[Collection("api")]
public sealed class GrowthFlowTests(ProDocApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Referral_coupon_rewards_and_growth_dashboard_are_abuse_resistant()
    {
        var doctor = await SignInAsync("9000000111");
        var referrer = await SignInAsync("9000000112");
        var admin = await SignInAsync("9000000001");
        await SendAsync(HttpMethod.Put, "/api/v1/account/profile", referrer.AccessToken, JsonContent.Create(new
        {
            fullName = "Referrer Patel",
            preferredLanguage = "en"
        }));

        var referral = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/growth/referral", referrer.AccessToken));
        var code = referral.GetProperty("code").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(code));
        Assert.Contains(code, referral.GetProperty("shareUrl").GetString());

        var selfReferral = await SignInAsync("9000000112", code);
        var selfDash = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/growth/referral", selfReferral.AccessToken));
        Assert.Equal(0, selfDash.GetProperty("registeredCount").GetInt32());

        var referred = await SignInAsync("9000000113", code);
        await SendAsync(HttpMethod.Put, "/api/v1/account/profile", referred.AccessToken, JsonContent.Create(new
        {
            fullName = "Referred Shah",
            preferredLanguage = "en"
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/growth/marketing-consent", referred.AccessToken, JsonContent.Create(new { marketingOptIn = true }));

        var afterAttr = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/growth/referral", referrer.AccessToken));
        Assert.Equal(1, afterAttr.GetProperty("registeredCount").GetInt32());

        var specializations = await SendAsync(HttpMethod.Get, "/api/v1/specializations", doctor.AccessToken);
        var cardiology = (await specializations.Content.ReadFromJsonAsync<SpecializationBody[]>())!.Single(item => item.Code == "cardiology");
        var (scheduleId, clinicId, branchId, _) = await PublishAsync(doctor, admin, cardiology.Id);
        doctor = await SignInAsync("9000000111");
        await ReadAsync(await SendAsync(HttpMethod.Put, $"/api/v1/clinics/{clinicId}/payment-settings", doctor.AccessToken, JsonContent.Create(new
        {
            policy = "OnlineOptional",
            advancePercent = 0,
            chargeCommissionOnWalkIns = false
        })));
        await ReadAsync(await SendAsync(HttpMethod.Put, $"/api/v1/clinics/{clinicId}/branches/{branchId}/queue-settings", doctor.AccessToken, JsonContent.Create(new
        {
            tokenPrefix = "G",
            scheduledPatientPriority = true,
            earlyCheckInMinutes = 10080,
            lateCheckInMinutes = 10080,
            allowWalkIns = true,
            allowPriorityPatients = false
        })));

        var coupons = await ReadArrayAsync(await SendAsync(HttpMethod.Get, "/api/v1/growth/coupons", referred.AccessToken));
        Assert.Contains(coupons, item => item.GetProperty("code").GetString() == "WELCOME10");

        var date = ClinicDate(2);
        var open = await SlotsAsync(scheduleId, date, referred.AccessToken);
        var held = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/appointments/holds", referred.AccessToken, JsonContent.Create(new
        {
            scheduleId,
            date = date.ToString("yyyy-MM-dd"),
            start = open[0]
        })));
        var appointmentId = held.GetProperty("id").GetGuid();
        await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/appointments/{appointmentId}/confirm", referred.AccessToken, JsonContent.Create(new { reason = "Growth visit" })));

        var preview = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/growth/coupons/preview", referred.AccessToken, JsonContent.Create(new
        {
            appointmentId,
            couponCode = "WELCOME10"
        })));
        Assert.True(preview.GetProperty("discountAmount").GetDecimal() > 0);
        Assert.True(preview.GetProperty("payableAmount").GetDecimal() < preview.GetProperty("bookingAmount").GetDecimal());

        var payment = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/appointments/{appointmentId}/payments", referred.AccessToken, JsonContent.Create(new
        {
            paymentMethod = "PayOnline",
            idempotencyKey = $"growth-{appointmentId:N}",
            couponCode = "WELCOME10"
        })));
        Assert.True(payment.GetProperty("amount").GetDecimal() < 500m);
        var paymentId = payment.GetProperty("id").GetGuid();
        await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/payments/{paymentId}/verify", referred.AccessToken, JsonContent.Create(new { })));

        var duplicateCoupon = await SendAsync(HttpMethod.Post, $"/api/v1/appointments/{appointmentId}/payments", referred.AccessToken, JsonContent.Create(new
        {
            paymentMethod = "PayOnline",
            idempotencyKey = $"growth-dup-{appointmentId:N}",
            couponCode = "WELCOME10"
        }));
        Assert.Equal(HttpStatusCode.Conflict, duplicateCoupon.StatusCode);

        var qr = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/appointments/{appointmentId}/check-in/qr-token", referred.AccessToken));
        var checkedIn = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/check-ins/qr", doctor.AccessToken, JsonContent.Create(new { token = qr.GetProperty("token").GetString() })));
        await SendAsync(HttpMethod.Post, $"/api/v1/queue-entries/{checkedIn.GetProperty("id").GetGuid()}/call", doctor.AccessToken);
        await SendAsync(HttpMethod.Post, $"/api/v1/queue-entries/{checkedIn.GetProperty("id").GetGuid()}/start-consultation", doctor.AccessToken);
        await SendAsync(HttpMethod.Post, $"/api/v1/queue-entries/{checkedIn.GetProperty("id").GetGuid()}/complete-consultation", doctor.AccessToken);

        var referrerWallet = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/growth/wallet", referrer.AccessToken));
        Assert.True(referrerWallet.GetProperty("pointsBalance").GetInt32() >= 100);
        var converted = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/growth/referral", referrer.AccessToken));
        Assert.True(converted.GetProperty("rewardedCount").GetInt32() >= 1);

        var patientWallet = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/growth/wallet", referred.AccessToken));
        Assert.True(patientWallet.GetProperty("pointsBalance").GetInt32() >= 20);

        var redeemed = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/growth/wallet/redeem", referrer.AccessToken, JsonContent.Create(new { points = 10 })));
        Assert.True(redeemed.GetProperty("creditBalanceInr").GetDecimal() >= 1m);

        var dashboard = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/v1/admin/growth/dashboard", admin.AccessToken));
        Assert.True(dashboard.GetProperty("referralConversions").GetInt32() >= 1);
        Assert.True(dashboard.GetProperty("couponsRedeemed30d").GetInt32() >= 1);
        Assert.True(dashboard.GetProperty("outstandingRewardPoints").GetInt32() >= 0);

        var campaigns = await ReadArrayAsync(await SendAsync(HttpMethod.Get, "/api/v1/admin/growth/campaigns", admin.AccessToken));
        Assert.NotEmpty(campaigns);

        var createdCoupon = await ReadAsync(await SendAsync(HttpMethod.Post, "/api/v1/admin/growth/coupons", admin.AccessToken, JsonContent.Create(new
        {
            code = "FLAT50",
            name = "Flat fifty",
            discountType = "Fixed",
            discountValue = 50,
            startsAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            endsAtUtc = DateTimeOffset.UtcNow.AddDays(30),
            minBookingAmount = 100,
            maxDiscountAmount = (decimal?)null,
            firstBookingOnly = false,
            newUserOnly = false,
            clinicId = (Guid?)null,
            doctorId = (Guid?)null,
            maxRedemptions = 100,
            maxRedemptionsPerUser = 1
        })));
        Assert.Equal("FLAT50", createdCoupon.GetProperty("code").GetString());
    }

    private async Task<(Guid ScheduleId, Guid ClinicId, Guid BranchId, Guid DoctorId)> PublishAsync(SessionBody doctor, SessionBody admin, Guid specializationId)
    {
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/basics", doctor.AccessToken, JsonContent.Create(new
        {
            firstName = "Gita",
            lastName = "Nair",
            dateOfBirth = "1984-01-01",
            gender = "female",
            email = "gita@example.com",
            preferredLanguage = "en"
        }));
        await SendAsync(HttpMethod.Put, "/api/v1/doctors/me/professional", doctor.AccessToken, JsonContent.Create(new
        {
            displayName = "Dr. Gita Nair",
            biography = "Growth clinic",
            yearsOfExperience = 12,
            languages = new[] { "en" },
            expertise = new[] { "General" }
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
            number = "MCI-GROW",
            council = "National Medical Commission",
            state = "Maharashtra",
            registrationType = "permanent",
            registeredOn = "2011-01-01",
            expiresOn = "2031-01-01"
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
            name = "Growth Care",
            type = "Clinic",
            phoneNumber = "9876501111",
            registrationNumber = "CLN-GROW",
            establishmentYear = 2016
        }));
        var branched = await ReadAsync(await SendAsync(HttpMethod.Post, $"/api/v1/clinics/{clinicId}/branches", doctor.AccessToken, JsonContent.Create(new
        {
            name = "Andheri",
            addressLine = "22 Link Road",
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

    private async Task<SessionBody> SignInAsync(string phone, string? referralCode = null)
    {
        var otpResponse = await _client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phoneNumber = phone });
        var otpBody = await otpResponse.Content.ReadAsStringAsync();
        Assert.True(otpResponse.IsSuccessStatusCode, otpBody);
        var otp = JsonSerializer.Deserialize<OtpBody>(otpBody, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var verifyResponse = await _client.PostAsJsonAsync("/api/v1/auth/otp/verify", new
        {
            phoneNumber = phone,
            code = otp!.DevelopmentCode,
            deviceName = "integration-test",
            referralCode
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
