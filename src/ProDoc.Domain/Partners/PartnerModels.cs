using System.Security.Cryptography;
using System.Text;
using ProDoc.Domain.Common;

namespace ProDoc.Domain.Partners;

public enum PartnerStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Suspended = 4
}

public enum PartnerAppEnvironment
{
    Sandbox = 1,
    Production = 2
}

public enum PartnerWebhookDeliveryStatus
{
    Pending = 1,
    Delivered = 2,
    Failed = 3,
    DeadLetter = 4
}

public static class PartnerScopes
{
    public const string DiscoveryRead = "discovery.read";
    public const string AvailabilityRead = "availability.read";
    public const string AppointmentsRead = "appointments.read";
    public const string AppointmentsWrite = "appointments.write";
    public const string PrescriptionsRead = "prescriptions.read";
    public const string LabsRead = "labs.read";
    public const string OrganizationRead = "organization.read";
    public const string WebhooksManage = "webhooks.manage";

    public static IReadOnlyList<string> Catalog { get; } =
    [
        DiscoveryRead,
        AvailabilityRead,
        AppointmentsRead,
        AppointmentsWrite,
        PrescriptionsRead,
        LabsRead,
        OrganizationRead,
        WebhooksManage
    ];

    public static IReadOnlyList<string> Normalize(IEnumerable<string> scopes)
    {
        var set = scopes
            .Select(item => item.Trim().ToLowerInvariant())
            .Where(item => Catalog.Contains(item, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item)
            .ToArray();
        if (set.Length == 0)
        {
            throw DomainException.Validation("partner_scopes_required", "Select at least one approved API scope.");
        }

        return set;
    }
}

public sealed class PartnerOrganization
{
    private PartnerOrganization()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string ContactEmail { get; private set; } = string.Empty;

    public string? Website { get; private set; }

    public PartnerStatus Status { get; private set; }

    public Guid OwnerUserId { get; private set; }

    public string? ReviewNote { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static PartnerOrganization Register(
        string name,
        string contactEmail,
        string? website,
        Guid ownerUserId,
        DateTimeOffset now)
    {
        var text = name.Trim();
        if (text.Length is < 2 or > 160)
        {
            throw DomainException.Validation("partner_name_invalid", "Partner name must be 2–160 characters.");
        }

        var email = contactEmail.Trim();
        if (email.Length is < 5 or > 160 || !email.Contains('@'))
        {
            throw DomainException.Validation("partner_email_invalid", "Enter a valid contact email.");
        }

        return new PartnerOrganization
        {
            Id = Guid.NewGuid(),
            Name = text,
            ContactEmail = email,
            Website = string.IsNullOrWhiteSpace(website) ? null : website.Trim(),
            Status = PartnerStatus.Pending,
            OwnerUserId = ownerUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Approve(string? note, DateTimeOffset now)
    {
        Status = PartnerStatus.Approved;
        ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        UpdatedAtUtc = now;
    }

    public void Reject(string note, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            throw DomainException.Validation("partner_reject_reason_required", "Provide a rejection reason.");
        }

        Status = PartnerStatus.Rejected;
        ReviewNote = note.Trim();
        UpdatedAtUtc = now;
    }

    public void Suspend(string? note, DateTimeOffset now)
    {
        Status = PartnerStatus.Suspended;
        ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        UpdatedAtUtc = now;
    }

    public void Reactivate(DateTimeOffset now)
    {
        Status = PartnerStatus.Approved;
        UpdatedAtUtc = now;
    }
}

public sealed class PartnerApp
{
    private PartnerApp()
    {
    }

    public Guid Id { get; private set; }

    public Guid PartnerOrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string ClientId { get; private set; } = string.Empty;

    public string ScopesCsv { get; private set; } = string.Empty;

    public PartnerAppEnvironment Environment { get; private set; }

    public bool IsActive { get; private set; }

    public int RateLimitPerMinute { get; private set; }

    public int DailyQuota { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public IReadOnlyList<string> Scopes =>
        ScopesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static PartnerApp Create(
        Guid partnerOrganizationId,
        string name,
        IEnumerable<string> scopes,
        PartnerAppEnvironment environment,
        int rateLimitPerMinute,
        int dailyQuota,
        DateTimeOffset now)
    {
        var text = name.Trim();
        if (text.Length is < 2 or > 120)
        {
            throw DomainException.Validation("partner_app_name_invalid", "Application name must be 2–120 characters.");
        }

        return new PartnerApp
        {
            Id = Guid.NewGuid(),
            PartnerOrganizationId = partnerOrganizationId,
            Name = text,
            ClientId = $"pd_{environment.ToString().ToLowerInvariant()}_{Guid.NewGuid():N}"[..40],
            ScopesCsv = string.Join(',', PartnerScopes.Normalize(scopes)),
            Environment = environment,
            IsActive = true,
            RateLimitPerMinute = Math.Clamp(rateLimitPerMinute, 10, 6000),
            DailyQuota = Math.Clamp(dailyQuota, 100, 1_000_000),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void UpdateScopes(IEnumerable<string> scopes, int rateLimitPerMinute, int dailyQuota, DateTimeOffset now)
    {
        ScopesCsv = string.Join(',', PartnerScopes.Normalize(scopes));
        RateLimitPerMinute = Math.Clamp(rateLimitPerMinute, 10, 6000);
        DailyQuota = Math.Clamp(dailyQuota, 100, 1_000_000);
        UpdatedAtUtc = now;
    }

    public void SetActive(bool active, DateTimeOffset now)
    {
        IsActive = active;
        UpdatedAtUtc = now;
    }

    public bool HasScope(string scope) =>
        Scopes.Contains(scope, StringComparer.OrdinalIgnoreCase);
}

public sealed class PartnerCredential
{
    private PartnerCredential()
    {
    }

    public Guid Id { get; private set; }

    public Guid PartnerAppId { get; private set; }

    public string SecretHash { get; private set; } = string.Empty;

    public string SecretPrefix { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public DateTimeOffset? LastUsedAtUtc { get; private set; }

    public static (PartnerCredential Credential, string PlainSecret) Issue(Guid partnerAppId, DateTimeOffset now)
    {
        var plain = $"psk_{Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()}";
        var credential = new PartnerCredential
        {
            Id = Guid.NewGuid(),
            PartnerAppId = partnerAppId,
            SecretHash = Hash(plain),
            SecretPrefix = plain[..12],
            CreatedAtUtc = now
        };
        return (credential, plain);
    }

    public bool Matches(string plainSecret) =>
        RevokedAtUtc is null && FixedTimeEquals(SecretHash, Hash(plainSecret));

    public void Revoke(DateTimeOffset now) => RevokedAtUtc = now;

    public void MarkUsed(DateTimeOffset now) => LastUsedAtUtc = now;

    public static string Hash(string plain)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plain));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var a = Encoding.UTF8.GetBytes(left);
        var b = Encoding.UTF8.GetBytes(right);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}

public sealed class PartnerWebhookEndpoint
{
    private PartnerWebhookEndpoint()
    {
    }

    public Guid Id { get; private set; }

    public Guid PartnerAppId { get; private set; }

    public string TargetUrl { get; private set; } = string.Empty;

    public string SigningSecret { get; private set; } = string.Empty;

    public string SecretPrefix { get; private set; } = string.Empty;

    public string EventTypesCsv { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public IReadOnlyList<string> EventTypes =>
        EventTypesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static (PartnerWebhookEndpoint Endpoint, string PlainSecret) Create(
        Guid partnerAppId,
        string targetUrl,
        IEnumerable<string> eventTypes,
        DateTimeOffset now)
    {
        ValidateUrl(targetUrl);
        var types = NormalizeEvents(eventTypes);
        var plain = $"whsec_{Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant()}";
        var endpoint = new PartnerWebhookEndpoint
        {
            Id = Guid.NewGuid(),
            PartnerAppId = partnerAppId,
            TargetUrl = targetUrl.Trim(),
            SigningSecret = plain,
            SecretPrefix = plain[..10],
            EventTypesCsv = string.Join(',', types),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        return (endpoint, plain);
    }

    public string RotateSecret(DateTimeOffset now)
    {
        var plain = $"whsec_{Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant()}";
        SigningSecret = plain;
        SecretPrefix = plain[..10];
        UpdatedAtUtc = now;
        return plain;
    }

    public void SetActive(bool active, DateTimeOffset now)
    {
        IsActive = active;
        UpdatedAtUtc = now;
    }

    public static string Sign(string plainSecret, long unixSeconds, string body)
    {
        var payload = $"{unixSeconds}.{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(plainSecret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    public static bool Verify(string plainSecret, long unixSeconds, string body, string signature, DateTimeOffset now, TimeSpan skew)
    {
        var age = now.ToUnixTimeSeconds() - unixSeconds;
        if (age > skew.TotalSeconds || age < -skew.TotalSeconds)
        {
            return false;
        }

        var expected = Sign(plainSecret, unixSeconds, body);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant()));
    }

    private static void ValidateUrl(string targetUrl)
    {
        if (!Uri.TryCreate(targetUrl.Trim(), UriKind.Absolute, out var uri))
        {
            throw DomainException.Validation("webhook_url_invalid", "Webhook URL must be https (http allowed for localhost only).");
        }

        var ok = uri.Scheme == "https" || (uri.Scheme == "http" && uri.IsLoopback);
        if (!ok)
        {
            throw DomainException.Validation("webhook_url_invalid", "Webhook URL must be https (http allowed for localhost only).");
        }
    }

    private static IReadOnlyList<string> NormalizeEvents(IEnumerable<string> eventTypes)
    {
        var allowed = PartnerWebhookEvents.Catalog;
        var set = eventTypes
            .Select(item => item.Trim().ToLowerInvariant())
            .Where(item => allowed.Contains(item, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (set.Length == 0)
        {
            throw DomainException.Validation("webhook_events_required", "Select at least one approved webhook event.");
        }

        return set;
    }
}

public static class PartnerWebhookEvents
{
    public const string AppointmentCreated = "appointment.created";
    public const string AppointmentCancelled = "appointment.cancelled";
    public const string AppointmentStatusChanged = "appointment.status_changed";
    public const string PaymentStatusChanged = "payment.status_changed";
    public const string PrescriptionFinalized = "prescription.finalized";
    public const string ClinicStatusChanged = "clinic.status_changed";
    public const string DoctorStatusChanged = "doctor.status_changed";

    public static IReadOnlyList<string> Catalog { get; } =
    [
        AppointmentCreated,
        AppointmentCancelled,
        AppointmentStatusChanged,
        PaymentStatusChanged,
        PrescriptionFinalized,
        ClinicStatusChanged,
        DoctorStatusChanged
    ];
}

public sealed class PartnerWebhookDelivery
{
    private PartnerWebhookDelivery()
    {
    }

    public Guid Id { get; private set; }

    public Guid PartnerWebhookEndpointId { get; private set; }

    public Guid PartnerAppId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string IdempotencyKey { get; private set; } = string.Empty;

    public string PayloadJson { get; private set; } = "{}";

    public PartnerWebhookDeliveryStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset AvailableAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? DeliveredAtUtc { get; private set; }

    public int? LastStatusCode { get; private set; }

    public string? LastError { get; private set; }

    public static PartnerWebhookDelivery Create(
        Guid endpointId,
        Guid appId,
        string eventType,
        string idempotencyKey,
        string payloadJson,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            PartnerWebhookEndpointId = endpointId,
            PartnerAppId = appId,
            EventType = eventType,
            IdempotencyKey = idempotencyKey,
            PayloadJson = payloadJson,
            Status = PartnerWebhookDeliveryStatus.Pending,
            AttemptCount = 0,
            AvailableAtUtc = now,
            CreatedAtUtc = now
        };

    public void MarkDelivered(int statusCode, DateTimeOffset now)
    {
        Status = PartnerWebhookDeliveryStatus.Delivered;
        LastStatusCode = statusCode;
        DeliveredAtUtc = now;
        AttemptCount += 1;
        LastError = null;
    }

    public void MarkRetry(string error, int? statusCode, DateTimeOffset nextAttempt, DateTimeOffset now)
    {
        Status = PartnerWebhookDeliveryStatus.Pending;
        AttemptCount += 1;
        LastError = Trim(error, 1000);
        LastStatusCode = statusCode;
        AvailableAtUtc = nextAttempt;
        _ = now;
    }

    public void MarkDeadLetter(string error, DateTimeOffset now)
    {
        Status = PartnerWebhookDeliveryStatus.DeadLetter;
        AttemptCount += 1;
        LastError = Trim(error, 1000);
        DeliveredAtUtc = now;
    }

    private static string Trim(string value, int max)
    {
        var text = value.Trim();
        return text.Length <= max ? text : text[..max];
    }
}

public sealed class PartnerApiUsageDaily
{
    private PartnerApiUsageDaily()
    {
    }

    public Guid Id { get; private set; }

    public Guid PartnerAppId { get; private set; }

    public DateOnly Day { get; private set; }

    public int RequestCount { get; private set; }

    public int ErrorCount { get; private set; }

    public static PartnerApiUsageDaily Create(Guid partnerAppId, DateOnly day) =>
        new()
        {
            Id = Guid.NewGuid(),
            PartnerAppId = partnerAppId,
            Day = day,
            RequestCount = 0,
            ErrorCount = 0
        };

    public void Increment(bool isError)
    {
        RequestCount += 1;
        if (isError)
        {
            ErrorCount += 1;
        }
    }
}

public enum PartnerBookingStatus
{
    Held = 1,
    Confirmed = 2,
    Cancelled = 3,
    Completed = 4
}

/// <summary>
/// Partner-originated appointment requests. Sandbox rows never mutate production appointments,
/// payments, or medical records. Production rows remain partner-scoped until clinic staff links care.
/// </summary>
public sealed class PartnerBooking
{
    private PartnerBooking()
    {
    }

    public Guid Id { get; private set; }

    public Guid PartnerAppId { get; private set; }

    public bool IsSandbox { get; private set; }

    public Guid DoctorId { get; private set; }

    public Guid? ClinicId { get; private set; }

    public Guid? BranchId { get; private set; }

    public Guid? ScheduleId { get; private set; }

    public DateTimeOffset StartsAtUtc { get; private set; }

    public DateTimeOffset EndsAtUtc { get; private set; }

    public string ExternalPatientRef { get; private set; } = string.Empty;

    public string PatientDisplayName { get; private set; } = string.Empty;

    public string? Reason { get; private set; }

    public PartnerBookingStatus Status { get; private set; }

    public string? IdempotencyKey { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static PartnerBooking Create(
        Guid partnerAppId,
        bool isSandbox,
        Guid doctorId,
        Guid? clinicId,
        Guid? branchId,
        Guid? scheduleId,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        string externalPatientRef,
        string patientDisplayName,
        string? reason,
        string? idempotencyKey,
        DateTimeOffset now)
    {
        var patientRef = externalPatientRef.Trim();
        if (patientRef.Length is < 2 or > 80)
        {
            throw DomainException.Validation("partner_patient_ref_invalid", "External patient reference must be 2–80 characters.");
        }

        var name = patientDisplayName.Trim();
        if (name.Length is < 2 or > 120)
        {
            throw DomainException.Validation("partner_patient_name_invalid", "Patient display name must be 2–120 characters.");
        }

        if (endsAtUtc <= startsAtUtc)
        {
            throw DomainException.Validation("partner_booking_window_invalid", "Appointment end must be after start.");
        }

        return new PartnerBooking
        {
            Id = Guid.NewGuid(),
            PartnerAppId = partnerAppId,
            IsSandbox = isSandbox,
            DoctorId = doctorId,
            ClinicId = clinicId,
            BranchId = branchId,
            ScheduleId = scheduleId,
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = endsAtUtc,
            ExternalPatientRef = patientRef,
            PatientDisplayName = name,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            Status = PartnerBookingStatus.Confirmed,
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Cancel(DateTimeOffset now)
    {
        if (Status == PartnerBookingStatus.Cancelled)
        {
            return;
        }

        if (Status == PartnerBookingStatus.Completed)
        {
            throw DomainException.Conflict("partner_booking_completed", "A completed booking cannot be cancelled.");
        }

        Status = PartnerBookingStatus.Cancelled;
        UpdatedAtUtc = now;
    }

    public void SetStatus(PartnerBookingStatus status, DateTimeOffset now)
    {
        Status = status;
        UpdatedAtUtc = now;
    }
}
