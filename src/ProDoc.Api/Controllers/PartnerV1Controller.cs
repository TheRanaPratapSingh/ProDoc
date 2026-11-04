using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProDoc.Api.Auth;
using ProDoc.Application.Partners;
using ProDoc.Domain.Partners;
using ProDoc.Infrastructure.Partners;

namespace ProDoc.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = PartnerAccessTokenIssuer.AuthenticationScheme, Policy = "PartnerApi")]
[Route("api/partner/v1")]
[EnableRateLimiting("partner_api")]
public sealed class PartnerV1Controller(IPartnerService partners) : ControllerBase
{
    [HttpGet("docs")]
    [AllowAnonymous]
    public IActionResult Docs() =>
        Ok(new
        {
            title = "ProDoc Partner API v1",
            authentication = new
            {
                tokenUrl = "/oauth/partner/token",
                grantType = "client_credentials",
                tokenTtlSeconds = PartnerAccessTokenIssuer.AccessTokenMinutes * 60,
                header = "Authorization: Bearer {access_token}"
            },
            scopes = PartnerScopes.Catalog,
            webhookEvents = PartnerWebhookEvents.Catalog,
            guides = partners.DocsGuides(),
            errors = partners.Catalog().ErrorCodes,
            openApi = "/swagger/partner/swagger.json"
        });

    [HttpGet("docs/auth")]
    [AllowAnonymous]
    public IActionResult AuthDocs() =>
        Ok(new
        {
            summary = "Exchange client credentials for a short-lived JWT (token_use=partner).",
            example = new
            {
                grant_type = "client_credentials",
                client_id = "pd_sandbox_…",
                client_secret = "psk_…",
                scope = "discovery.read availability.read appointments.read appointments.write"
            },
            notes = new[]
            {
                "Never embed client_secret in mobile or browser apps.",
                "Rotate credentials from the developer portal; revoked secrets fail immediately.",
                "Access tokens expire in minutes; refresh by requesting a new token."
            }
        });

    [HttpGet("docs/webhooks")]
    [AllowAnonymous]
    public IActionResult WebhookDocs() =>
        Ok(new
        {
            signatureHeader = "X-ProDoc-Signature",
            timestampHeader = "X-ProDoc-Timestamp",
            algorithm = "HMAC-SHA256 over `{timestamp}.{rawBody}` hex digest",
            replayProtection = "Reject timestamps older/newer than 5 minutes",
            idempotencyHeader = "X-ProDoc-Idempotency-Key",
            retries = "Exponential backoff up to 8 attempts, then dead-letter"
        });

    [HttpGet("docs/sandbox")]
    [AllowAnonymous]
    public IActionResult SandboxDocs() =>
        Ok(new
        {
            summary = "Sandbox apps create PartnerBooking rows with isSandbox=true and never mutate production appointments, payments, or medical records.",
            tip = "Create a Sandbox application after partner registration, issue credentials once, and store the secret server-side."
        });

    [HttpGet("docs/errors")]
    [AllowAnonymous]
    public PartnerCatalog Errors() => partners.Catalog();

    [HttpGet("docs/scopes")]
    [AllowAnonymous]
    public IActionResult Scopes() => Ok(PartnerScopes.Catalog);

    [HttpGet("doctors")]
    [HasPartnerScope(PartnerScopes.DiscoveryRead)]
    public async Task<IReadOnlyList<PartnerDoctorCard>> Doctors(
        [FromQuery] string? query,
        [FromQuery] string? city,
        CancellationToken cancellationToken)
    {
        var principal = await PrincipalAsync(cancellationToken);
        return await partners.SearchDoctorsAsync(principal, query, city, cancellationToken);
    }

    [HttpGet("doctors/{doctorId:guid}")]
    [HasPartnerScope(PartnerScopes.DiscoveryRead)]
    public async Task<PartnerDoctorCard> Doctor(Guid doctorId, CancellationToken cancellationToken)
    {
        var principal = await PrincipalAsync(cancellationToken);
        return await partners.GetDoctorAsync(principal, doctorId, cancellationToken);
    }

    [HttpGet("clinics")]
    [HasPartnerScope(PartnerScopes.DiscoveryRead)]
    public async Task<IReadOnlyList<PartnerClinicCard>> Clinics(
        [FromQuery] string? query,
        [FromQuery] string? city,
        CancellationToken cancellationToken)
    {
        var principal = await PrincipalAsync(cancellationToken);
        return await partners.SearchClinicsAsync(principal, query, city, cancellationToken);
    }

    [HttpGet("clinics/{clinicId:guid}")]
    [HasPartnerScope(PartnerScopes.DiscoveryRead)]
    public async Task<PartnerClinicCard> Clinic(Guid clinicId, CancellationToken cancellationToken)
    {
        var principal = await PrincipalAsync(cancellationToken);
        return await partners.GetClinicAsync(principal, clinicId, cancellationToken);
    }

    [HttpGet("availability/doctors/{doctorId:guid}")]
    [HasPartnerScope(PartnerScopes.AvailabilityRead)]
    public async Task<object> Availability(
        Guid doctorId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken)
    {
        var principal = await PrincipalAsync(cancellationToken);
        return await partners.GetAvailabilityAsync(principal, doctorId, from, to, branchId, cancellationToken);
    }

    [HttpGet("organizations/{organizationId:guid}")]
    [HasPartnerScope(PartnerScopes.OrganizationRead)]
    public async Task<PartnerOrganizationPublic> Organization(Guid organizationId, CancellationToken cancellationToken)
    {
        var principal = await PrincipalAsync(cancellationToken);
        return await partners.GetOrganizationAsync(principal, organizationId, cancellationToken);
    }

    [HttpGet("appointments")]
    [HasPartnerScope(PartnerScopes.AppointmentsRead)]
    public async Task<IReadOnlyList<PartnerBookingDetails>> Appointments(CancellationToken cancellationToken)
    {
        var principal = await PrincipalAsync(cancellationToken);
        return await partners.ListBookingsAsync(principal, cancellationToken);
    }

    [HttpGet("appointments/{bookingId:guid}")]
    [HasPartnerScope(PartnerScopes.AppointmentsRead)]
    public async Task<PartnerBookingDetails> Appointment(Guid bookingId, CancellationToken cancellationToken)
    {
        var principal = await PrincipalAsync(cancellationToken);
        return await partners.GetBookingAsync(principal, bookingId, cancellationToken);
    }

    [HttpPost("appointments")]
    [HasPartnerScope(PartnerScopes.AppointmentsWrite)]
    public async Task<PartnerBookingDetails> CreateAppointment(PartnerCreateAppointmentRequest request, CancellationToken cancellationToken)
    {
        var principal = await PrincipalAsync(cancellationToken);
        return await partners.CreateBookingAsync(
            principal,
            request.DoctorId,
            request.ClinicId,
            request.BranchId,
            request.ScheduleId,
            request.StartsAtUtc,
            request.EndsAtUtc,
            request.ExternalPatientRef,
            request.PatientDisplayName,
            request.Reason,
            request.IdempotencyKey,
            cancellationToken);
    }

    [HttpPost("appointments/{bookingId:guid}/cancel")]
    [HasPartnerScope(PartnerScopes.AppointmentsWrite)]
    public async Task<PartnerBookingDetails> CancelAppointment(Guid bookingId, CancellationToken cancellationToken)
    {
        var principal = await PrincipalAsync(cancellationToken);
        return await partners.CancelBookingAsync(principal, bookingId, cancellationToken);
    }

    [HttpGet("prescriptions/{prescriptionId:guid}")]
    [HasPartnerScope(PartnerScopes.PrescriptionsRead)]
    public async Task<PartnerPrescriptionSummary> Prescription(
        Guid prescriptionId,
        [FromQuery] bool patientConsentAttested,
        CancellationToken cancellationToken)
    {
        var principal = await PrincipalAsync(cancellationToken);
        return await partners.GetPrescriptionAsync(principal, prescriptionId, patientConsentAttested, cancellationToken);
    }

    [HttpGet("labs/catalog")]
    [HasPartnerScope(PartnerScopes.LabsRead)]
    public async Task<IReadOnlyList<PartnerLabOrderSummary>> Labs(CancellationToken cancellationToken)
    {
        var principal = await PrincipalAsync(cancellationToken);
        return await partners.ListLabCatalogAsync(principal, cancellationToken);
    }

    private async Task<PartnerPrincipal> PrincipalAsync(CancellationToken cancellationToken)
    {
        var principal = await User.LoadPartnerPrincipalAsync(partners, cancellationToken);
        await partners.EnsureQuotaAsync(principal, cancellationToken);
        await partners.RecordUsageAsync(principal.PartnerAppId, isError: false, cancellationToken);
        return principal;
    }
}

public sealed record PartnerCreateAppointmentRequest(
    Guid DoctorId,
    Guid? ClinicId,
    Guid? BranchId,
    Guid? ScheduleId,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string ExternalPatientRef,
    string PatientDisplayName,
    string? Reason,
    string? IdempotencyKey);
