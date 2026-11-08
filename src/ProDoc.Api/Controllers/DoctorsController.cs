using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Api.Contracts;
using ProDoc.Application.Providers;
using ProDoc.Application.Queue;
using ProDoc.Domain.Common;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Providers;

namespace ProDoc.Api.Controllers;

[ApiController]
[Route("api/v1/doctors")]
public sealed class DoctorsController(
    DoctorOnboardingService doctors,
    DoctorPortalService portal,
    ProviderDirectoryService directory,
    ProviderDocumentService documents)
    : ControllerBase
{
    [HasPermission(PermissionCodes.ProviderOnboard)]
    [HttpGet("me")]
    public async Task<ActionResult<DoctorProfile>> Mine(CancellationToken cancellationToken)
    {
        var profile = await doctors.FindMineAsync(User.GetUserId(), cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [Authorize]
    [HttpGet("me/dashboard")]
    public Task<DoctorDashboard> Dashboard([FromQuery] Guid? clinicId, CancellationToken cancellationToken) =>
        portal.GetDashboardAsync(User.GetUserId(), clinicId, cancellationToken);

    [Authorize]
    [HttpGet("me/clinics")]
    public Task<IReadOnlyList<DoctorClinicOption>> Clinics(CancellationToken cancellationToken) =>
        portal.ListClinicsAsync(User.GetUserId(), cancellationToken);

    [Authorize]
    [HttpGet("me/earnings")]
    public Task<DoctorEarningsDashboard> Earnings([FromQuery] Guid? clinicId, CancellationToken cancellationToken) =>
        portal.GetEarningsAsync(User.GetUserId(), clinicId, cancellationToken);

    [Authorize]
    [HttpGet("me/patients")]
    public Task<IReadOnlyList<DoctorPatientRow>> Patients([FromQuery] Guid? clinicId, CancellationToken cancellationToken) =>
        portal.ListPatientsAsync(User.GetUserId(), clinicId, cancellationToken);

    [Authorize]
    [HttpGet("me/prescriptions")]
    public Task<IReadOnlyList<DoctorPrescriptionRow>> Prescriptions([FromQuery] Guid? clinicId, CancellationToken cancellationToken) =>
        portal.ListPrescriptionsAsync(User.GetUserId(), clinicId, cancellationToken);

    [Authorize]
    [HttpGet("me/notifications")]
    public Task<IReadOnlyList<NotificationDetails>> Notifications(CancellationToken cancellationToken) =>
        portal.ListNotificationsAsync(User.GetUserId(), cancellationToken);

    [HasPermission(PermissionCodes.ProviderOnboard)]
    [HttpPut("me/basics")]
    public async Task<ActionResult<DoctorProfile>> Basics(DoctorBasicsRequest request, CancellationToken cancellationToken)
    {
        var existing = await doctors.FindMineAsync(User.GetUserId(), cancellationToken);
        var allowCreate = existing is null && CanCreateDoctorProfile();
        if (existing is null && !allowCreate)
        {
            return Forbid();
        }

        return Ok(await doctors.SaveBasicsAsync(
            User.GetUserId(),
            request.FirstName,
            request.LastName,
            request.DateOfBirth,
            request.Gender,
            request.Email,
            request.PreferredLanguage,
            cancellationToken,
            allowCreate));
    }

    [HasPermission(PermissionCodes.ProviderOnboard)]
    [HttpPut("me/professional")]
    public async Task<ActionResult<DoctorProfile>> Professional(DoctorProfessionalRequest request, CancellationToken cancellationToken) =>
        Ok(await doctors.SaveProfessionalAsync(
            User.GetUserId(),
            request.DisplayName,
            request.Biography,
            request.YearsOfExperience,
            request.Languages,
            request.Expertise,
            cancellationToken));

    [HasPermission(PermissionCodes.ProviderOnboard)]
    [HttpPost("me/qualifications")]
    public async Task<ActionResult<DoctorProfile>> AddQualification(QualificationRequest request, CancellationToken cancellationToken) =>
        Ok(await doctors.AddQualificationAsync(User.GetUserId(), request.Name, request.Institution, request.GraduationYear, cancellationToken));

    [HasPermission(PermissionCodes.ProviderOnboard)]
    [HttpDelete("me/qualifications/{qualificationId:guid}")]
    public async Task<ActionResult<DoctorProfile>> RemoveQualification(Guid qualificationId, CancellationToken cancellationToken) =>
        Ok(await doctors.RemoveQualificationAsync(User.GetUserId(), qualificationId, cancellationToken));

    [HasPermission(PermissionCodes.ProviderOnboard)]
    [HttpPut("me/specializations")]
    public async Task<ActionResult<DoctorProfile>> Specializations(SpecializationsRequest request, CancellationToken cancellationToken) =>
        Ok(await doctors.SetSpecializationsAsync(
            User.GetUserId(),
            request.SpecializationIds,
            request.PrimarySpecializationId,
            cancellationToken));

    [HasPermission(PermissionCodes.ProviderOnboard)]
    [HttpPut("me/registration")]
    public async Task<ActionResult<DoctorProfile>> Registration(RegistrationRequest request, CancellationToken cancellationToken) =>
        Ok(await doctors.SaveRegistrationAsync(
            User.GetUserId(),
            request.Number,
            request.Council,
            request.State,
            request.RegistrationType,
            request.RegisteredOn,
            request.ExpiresOn,
            cancellationToken));

    [HasPermission(PermissionCodes.ProviderOnboard)]
    [HttpPost("me/documents")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<DoctorProfile>> Upload(IFormFile? file, [FromForm] string? category, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            throw DomainException.Validation("file_empty", "Choose a file to upload.");
        }

        var parsed = ParseEnum<DocumentCategory>(category, "document_invalid", "Choose a document category.");
        await using var stream = file.OpenReadStream();
        var stored = await documents.StoreAsync(
            new IncomingUpload(file.FileName, file.ContentType, stream),
            imagesOnly: parsed == DocumentCategory.ProfilePhoto,
            cancellationToken);
        return Ok(await doctors.AttachDocumentAsync(User.GetUserId(), parsed, stored, cancellationToken));
    }

    [HasPermission(PermissionCodes.ProviderOnboard)]
    [HttpPut("me/availability")]
    public async Task<ActionResult<DoctorProfile>> Availability(AvailabilityRequest request, CancellationToken cancellationToken) =>
        Ok(await doctors.SetAcceptingPatientsAsync(User.GetUserId(), request.IsAcceptingPatients, cancellationToken));

    [HasPermission(PermissionCodes.ProviderOnboard)]
    [HttpPost("me/affiliations")]
    public async Task<ActionResult<DoctorProfile>> Affiliate(AffiliationRequest request, CancellationToken cancellationToken) =>
        Ok(await doctors.AffiliateAsync(
            User.GetUserId(),
            request.ClinicId,
            request.BranchId,
            ParseEnum<AssociationKind>(request.Kind, "affiliation_invalid", "Choose how you practice at this clinic."),
            cancellationToken));

    [HasPermission(PermissionCodes.ProviderOnboard)]
    [HttpDelete("me/affiliations/{clinicId:guid}")]
    public async Task<ActionResult<DoctorProfile>> RemoveAffiliation(Guid clinicId, CancellationToken cancellationToken) =>
        Ok(await doctors.RemoveAffiliationAsync(User.GetUserId(), clinicId, cancellationToken));

    [HasPermission(PermissionCodes.ProviderOnboard)]
    [HttpPost("me/submit")]
    public async Task<ActionResult<DoctorProfile>> Submit(CancellationToken cancellationToken) =>
        Ok(await doctors.SubmitAsync(User.GetUserId(), Metadata(), cancellationToken));

    [HasPermission(PermissionCodes.DoctorsSearch)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DoctorCard>>> Search(
        [FromQuery] string? query,
        [FromQuery] Guid? specializationId,
        CancellationToken cancellationToken) =>
        Ok(await directory.SearchDoctorsAsync(query, specializationId, cancellationToken));

    [HasPermission(PermissionCodes.DoctorsSearch)]
    [HttpGet("{doctorId:guid}")]
    public async Task<ActionResult<PublicDoctor>> Get(Guid doctorId, CancellationToken cancellationToken) =>
        Ok(await directory.GetDoctorAsync(doctorId, cancellationToken));

    private RequestMetadata Metadata() => new(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext));

    /// <summary>
    /// New doctor profiles may be created by Super Admin (<see cref="PermissionCodes.DoctorCreate"/>)
    /// or by users who already hold the Doctor role (invited doctors completing onboarding).
    /// Patients and other roles cannot mint a doctor profile.
    /// </summary>
    private bool CanCreateDoctorProfile()
    {
        if (User.Claims.Any(claim => claim.Type == "perm" && claim.Value == PermissionCodes.DoctorCreate))
        {
            return true;
        }

        return User.Claims.Any(claim =>
            (claim.Type is "role" or System.Security.Claims.ClaimTypes.Role)
            && string.Equals(claim.Value, RoleNames.Doctor, StringComparison.Ordinal));
    }

    private static TEnum ParseEnum<TEnum>(string? value, string code, string title)
        where TEnum : struct, Enum
    {
        if (!Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
        {
            throw DomainException.Validation(code, title);
        }

        return parsed;
    }
}
