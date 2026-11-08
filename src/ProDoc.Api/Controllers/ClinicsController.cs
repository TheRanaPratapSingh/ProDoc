using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Api.Contracts;
using ProDoc.Application.Providers;
using ProDoc.Domain.Common;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Providers;

namespace ProDoc.Api.Controllers;

[ApiController]
[Route("api/v1/clinics")]
public sealed class ClinicsController(ClinicOnboardingService clinics, ProviderDirectoryService directory, ProviderDocumentService documents)
    : ControllerBase
{
    [HasPermission(PermissionCodes.ProviderOnboard)]
    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<ClinicProfile>>> Mine(CancellationToken cancellationToken) =>
        Ok(await clinics.ListMineAsync(User.GetUserId(), cancellationToken));

    /// <summary>Create a clinic. Super Admin only (<see cref="PermissionCodes.ClinicCreate"/>).</summary>
    [HasPermission(PermissionCodes.ClinicCreate)]
    [HttpPost]
    public async Task<ActionResult<ClinicProfile>> Create([FromBody] CreateClinicRequest? request, CancellationToken cancellationToken) =>
        Ok(await clinics.CreateAsync(User.GetUserId(), request?.OwnerUserId, cancellationToken));

    [HasPermission(PermissionCodes.ProviderOnboard)]
    [HttpPut("{clinicId:guid}")]
    public async Task<ActionResult<ClinicProfile>> Save(Guid clinicId, ClinicDetailsRequest request, CancellationToken cancellationToken) =>
        Ok(await clinics.SaveAsync(
            User.GetUserId(),
            clinicId,
            request.Name,
            ParseType(request.Type),
            request.LegalName,
            request.Description,
            request.PhoneNumber,
            request.Email,
            request.Website,
            request.EstablishmentYear,
            request.RegistrationNumber,
            request.RegistrationAuthority,
            request.RegistrationState,
            request.RegistrationExpiresOn,
            cancellationToken));

    [HasPermission(PermissionCodes.ProviderOnboard)]
    [HttpPost("{clinicId:guid}/branches")]
    public async Task<ActionResult<ClinicProfile>> AddBranch(Guid clinicId, BranchRequest request, CancellationToken cancellationToken) =>
        Ok(await clinics.AddBranchAsync(
            User.GetUserId(),
            clinicId,
            request.Name,
            request.AddressLine,
            request.AddressLine2,
            request.City,
            request.State,
            request.PostalCode,
            request.Latitude,
            request.Longitude,
            request.PhoneNumber,
            request.IsPrimary,
            cancellationToken,
            request.Landmark,
            request.Locality,
            request.District,
            request.Country,
            request.GooglePlaceId));

    [HasPermission(PermissionCodes.ProviderOnboard)]
    [HttpPut("{clinicId:guid}/branches/{branchId:guid}")]
    public async Task<ActionResult<ClinicProfile>> UpdateBranch(
        Guid clinicId,
        Guid branchId,
        BranchRequest request,
        CancellationToken cancellationToken) =>
        Ok(await clinics.UpdateBranchAsync(
            User.GetUserId(),
            clinicId,
            branchId,
            request.Name,
            request.AddressLine,
            request.AddressLine2,
            request.City,
            request.State,
            request.PostalCode,
            request.Latitude,
            request.Longitude,
            request.PhoneNumber,
            request.IsPrimary,
            cancellationToken,
            request.Landmark,
            request.Locality,
            request.District,
            request.Country,
            request.GooglePlaceId));

    [HasPermission(PermissionCodes.ProviderOnboard)]
    [HttpPost("{clinicId:guid}/documents")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ClinicProfile>> Upload(
        Guid clinicId,
        IFormFile? file,
        [FromForm] string? category,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            throw DomainException.Validation("file_empty", "Choose a file to upload.");
        }

        var parsed = ParseCategory(category);
        await using var stream = file.OpenReadStream();
        var stored = await documents.StoreAsync(
            new IncomingUpload(file.FileName, file.ContentType, stream),
            imagesOnly: parsed == DocumentCategory.ClinicLogo,
            cancellationToken);
        return Ok(await clinics.AttachDocumentAsync(User.GetUserId(), clinicId, parsed, stored, cancellationToken));
    }

    [HasPermission(PermissionCodes.ProviderOnboard)]
    [HttpPost("{clinicId:guid}/submit")]
    public async Task<ActionResult<ClinicProfile>> Submit(Guid clinicId, CancellationToken cancellationToken) =>
        Ok(await clinics.SubmitAsync(User.GetUserId(), clinicId, Metadata(), cancellationToken));

    [HasPermission(PermissionCodes.DoctorsSearch)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClinicCard>>> Search([FromQuery] string? query, CancellationToken cancellationToken) =>
        Ok(await directory.SearchClinicsAsync(query, cancellationToken));

    [HasPermission(PermissionCodes.DoctorsSearch)]
    [HttpGet("{clinicId:guid}")]
    public async Task<ActionResult<PublicClinic>> Get(Guid clinicId, CancellationToken cancellationToken) =>
        Ok(await directory.GetClinicAsync(clinicId, cancellationToken));

    private RequestMetadata Metadata() => new(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext));

    private static ClinicType? ParseType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Enum.TryParse<ClinicType>(value, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
        {
            throw DomainException.Validation("clinic_type_invalid", "Choose a clinic type.");
        }

        return parsed;
    }

    private static DocumentCategory ParseCategory(string? value)
    {
        if (!Enum.TryParse<DocumentCategory>(value, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
        {
            throw DomainException.Validation("document_invalid", "Choose a document category.");
        }

        return parsed;
    }
}

public sealed record CreateClinicRequest(Guid? OwnerUserId);
