using Microsoft.Extensions.Options;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Providers;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Providers;

namespace ProDoc.Application.Appointments;

public sealed class AppointmentShareService(
    IAppointmentStore appointments,
    IProviderStore providers,
    IPlaceLinkBuilder placeLinks,
    IOptions<AppointmentShareOptions> options,
    IClock clock)
{
    public async Task<AppointmentShareLinkDetails> CreateLinkAsync(
        Guid userId,
        Guid appointmentId,
        int? lifetimeHours,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var appointment = await OwnedAppointmentAsync(userId, appointmentId, cancellationToken);
        EnsureShareable(appointment);

        var now = clock.UtcNow;
        foreach (var existing in await appointments.ListShareLinksAsync(appointmentId, cancellationToken))
        {
            if (existing.IsActive(now))
            {
                existing.Revoke(now);
            }
        }

        var lifetime = lifetimeHours is int hours
            ? TimeSpan.FromHours(hours)
            : TimeSpan.FromHours(Math.Clamp(options.Value.DefaultLifetimeHours, 1, (int)AppointmentShareLink.MaxLifetime.TotalHours));
        var raw = AppointmentShareLink.CreateRawToken();
        var link = AppointmentShareLink.Create(
            appointment.Id,
            userId,
            AppointmentShareLink.HashToken(raw),
            now,
            lifetime);
        appointments.AddShareLink(link);
        await appointments.SaveChangesAsync(
            AuditLog.Record(userId, AuditActions.AppointmentShareCreated, now, metadata.IpAddress, metadata.UserAgent),
            cancellationToken);

        return new AppointmentShareLinkDetails(
            link.Id,
            appointment.Id,
            BuildShareUrl(raw),
            raw,
            link.CreatedAtUtc,
            link.ExpiresAtUtc,
            true);
    }

    public async Task RevokeLinkAsync(
        Guid userId,
        Guid appointmentId,
        Guid shareId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        _ = await OwnedAppointmentAsync(userId, appointmentId, cancellationToken);
        var links = await appointments.ListShareLinksAsync(appointmentId, cancellationToken);
        var link = links.FirstOrDefault(item => item.Id == shareId)
            ?? throw DomainException.NotFound("share_link_not_found", "That sharing link was not found.");
        if (link.CreatedByUserId != userId)
        {
            throw DomainException.NotFound("share_link_not_found", "That sharing link was not found.");
        }

        var now = clock.UtcNow;
        link.Revoke(now);
        await appointments.SaveChangesAsync(
            AuditLog.Record(userId, AuditActions.AppointmentShareRevoked, now, metadata.IpAddress, metadata.UserAgent),
            cancellationToken);
    }

    public async Task RevokeAllAsync(
        Guid userId,
        Guid appointmentId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        _ = await OwnedAppointmentAsync(userId, appointmentId, cancellationToken);
        var now = clock.UtcNow;
        var changed = false;
        foreach (var link in await appointments.ListShareLinksAsync(appointmentId, cancellationToken))
        {
            if (link.IsActive(now) && link.CreatedByUserId == userId)
            {
                link.Revoke(now);
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        await appointments.SaveChangesAsync(
            AuditLog.Record(userId, AuditActions.AppointmentShareRevoked, now, metadata.IpAddress, metadata.UserAgent),
            cancellationToken);
    }

    public async Task<IReadOnlyList<AppointmentShareLinkStatus>> ListLinksAsync(
        Guid userId,
        Guid appointmentId,
        CancellationToken cancellationToken)
    {
        _ = await OwnedAppointmentAsync(userId, appointmentId, cancellationToken);
        var now = clock.UtcNow;
        return (await appointments.ListShareLinksAsync(appointmentId, cancellationToken))
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => new AppointmentShareLinkStatus(
                item.Id,
                item.AppointmentId,
                item.CreatedAtUtc,
                item.ExpiresAtUtc,
                item.RevokedAtUtc,
                item.IsActive(now),
                item.AccessCount))
            .ToArray();
    }

    public async Task<SharedAppointmentSummary> ResolvePublicAsync(string rawToken, CancellationToken cancellationToken)
    {
        var hash = AppointmentShareLink.HashToken(rawToken);
        var link = await appointments.FindShareLinkByHashAsync(hash, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("share_link_unavailable", "That sharing link is no longer available.");
        var now = clock.UtcNow;
        link.RecordAccess(now);
        await appointments.SaveChangesAsync(
            AuditLog.Record(link.CreatedByUserId, AuditActions.AppointmentShareAccessed, now, null, null),
            cancellationToken);

        var appointment = await appointments.FindAsync(link.AppointmentId, tracking: false, cancellationToken)
            ?? throw DomainException.NotFound("share_link_unavailable", "That sharing link is no longer available.");
        return await MapSummaryAsync(appointment, link.ExpiresAtUtc, cancellationToken);
    }

    public async Task<SharedAppointmentSummary> PreviewAsync(
        Guid userId,
        Guid appointmentId,
        CancellationToken cancellationToken)
    {
        var appointment = await OwnedAppointmentAsync(userId, appointmentId, cancellationToken);
        return await MapSummaryAsync(appointment, linkExpiresAtUtc: null, cancellationToken);
    }

    private async Task<Appointment> OwnedAppointmentAsync(Guid userId, Guid appointmentId, CancellationToken cancellationToken)
    {
        var appointment = await appointments.FindAsync(appointmentId, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        if (appointment.AccountUserId != userId || appointment.IsWalkIn)
        {
            throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        }

        return appointment;
    }

    private static void EnsureShareable(Appointment appointment)
    {
        if (appointment.Status is AppointmentStatus.Held or AppointmentStatus.Expired)
        {
            throw DomainException.Conflict("appointment_not_shareable", "Only a booked appointment can be shared.");
        }
    }

    private async Task<SharedAppointmentSummary> MapSummaryAsync(
        Appointment appointment,
        DateTimeOffset? linkExpiresAtUtc,
        CancellationToken cancellationToken)
    {
        var doctor = await providers.FindDoctorAsync(appointment.DoctorId, tracking: false, cancellationToken);
        var clinic = await providers.FindClinicAsync(appointment.ClinicId, tracking: false, cancellationToken);
        var branch = clinic?.Branches.FirstOrDefault(item => item.Id == appointment.BranchId);

        var clinicName = !string.IsNullOrWhiteSpace(appointment.LocationClinicName)
            ? appointment.LocationClinicName.Trim()
            : clinic?.Name?.Trim() ?? "Clinic";
        var branchName = !string.IsNullOrWhiteSpace(appointment.LocationBranchName)
            ? appointment.LocationBranchName.Trim()
            : branch?.Name?.Trim();
        if (string.Equals(clinicName, branchName, StringComparison.OrdinalIgnoreCase))
        {
            branchName = null;
        }

        var address = !string.IsNullOrWhiteSpace(appointment.LocationAddress)
            ? appointment.LocationAddress.Trim()
            : branch?.FormattedAddress;
        var latitude = appointment.LocationLatitude ?? branch?.Latitude;
        var longitude = appointment.LocationLongitude ?? branch?.Longitude;
        var googlePlaceId = appointment.LocationGooglePlaceId ?? branch?.GooglePlaceId;
        var mapsUrl = placeLinks.MapsUrl(new PlaceLinkInput(clinicName, address, latitude, longitude, googlePlaceId));

        var primarySpecializationId = doctor?.Specializations.FirstOrDefault(item => item.IsPrimary)?.SpecializationId
            ?? doctor?.Specializations.FirstOrDefault()?.SpecializationId;
        var specialty = primarySpecializationId is Guid specializationId
            ? SpecializationCatalog.All.FirstOrDefault(item => item.Id == specializationId)?.Name
            : null;

        var doctorName = FormatDoctorName(doctor?.DisplayName);
        var patientName = string.IsNullOrWhiteSpace(appointment.PatientName) ? "Patient" : appointment.PatientName.Trim();

        return new SharedAppointmentSummary(
            appointment.Reference ?? appointment.Id.ToString("N")[..8].ToUpperInvariant(),
            appointment.Status.ToString(),
            patientName,
            doctorName,
            specialty,
            clinicName,
            branchName,
            string.IsNullOrWhiteSpace(address) ? null : address,
            mapsUrl,
            appointment.StartsAtUtc,
            appointment.EndsAtUtc,
            appointment.TimeZoneId,
            appointment.BookingMode == Domain.Scheduling.ScheduleBookingMode.HourlyQueue ? "HourlyQueue" : "TimedSlots",
            appointment.TokenLabel,
            appointment.TokenNumber,
            linkExpiresAtUtc);
    }

    private string BuildShareUrl(string rawToken)
    {
        var baseUrl = options.Value.PublicBaseUrl?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "https://prodoc.app/share/";
        }

        if (!baseUrl.EndsWith('/'))
        {
            baseUrl += "/";
        }

        return baseUrl + rawToken;
    }

    private static string FormatDoctorName(string? displayName)
    {
        var trimmed = displayName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "Doctor";
        }

        if (trimmed.StartsWith("Dr.", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Doctor ", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return "Dr. " + trimmed;
    }
}
