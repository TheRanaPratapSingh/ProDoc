using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Application.Scheduling;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Route("api/v1/search")]
public sealed class SearchController(DiscoveryService discovery) : ControllerBase
{
    [HasPermission(PermissionCodes.DoctorsSearch)]
    [HttpGet("doctors")]
    public Task<ActionResult<IReadOnlyList<DiscoveryPlace>>> Doctors(
        [FromQuery] string? query,
        [FromQuery] Guid? specializationId,
        [FromQuery] Guid? clinicId,
        [FromQuery] string? city,
        [FromQuery] string? locality,
        [FromQuery] double? latitude,
        [FromQuery] double? longitude,
        [FromQuery] double? radiusKm,
        [FromQuery] decimal? minFee,
        [FromQuery] decimal? maxFee,
        [FromQuery] int? minExperience,
        [FromQuery] string? gender,
        [FromQuery] string? language,
        [FromQuery] string? qualification,
        [FromQuery] string? postalCode,
        [FromQuery] string? availability,
        [FromQuery] string? sort,
        [FromQuery] int? page,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        SearchDoctors(
            query,
            specializationId,
            clinicId,
            city,
            locality,
            latitude,
            longitude,
            radiusKm,
            minFee,
            maxFee,
            minExperience,
            gender,
            language,
            qualification,
            postalCode,
            availability,
            sort,
            null,
            page,
            pageSize,
            cancellationToken);

    private async Task<ActionResult<IReadOnlyList<DiscoveryPlace>>> SearchDoctors(
        string? query,
        Guid? specializationId,
        Guid? clinicId,
        string? city,
        string? locality,
        double? latitude,
        double? longitude,
        double? radiusKm,
        decimal? minFee,
        decimal? maxFee,
        int? minExperience,
        string? gender,
        string? language,
        string? qualification,
        string? postalCode,
        string? availability,
        string? sort,
        string? clinicType,
        int? page,
        int pageSize,
        CancellationToken cancellationToken) =>
        Ok(await discovery.SearchAsync(
            new DiscoveryQuery(query, specializationId, clinicId, city, latitude, longitude, radiusKm, minFee, maxFee, minExperience, gender, language, qualification, postalCode, locality, availability, sort, clinicType, page ?? 1, pageSize),
            cancellationToken));

    [HasPermission(PermissionCodes.DoctorsSearch)]
    [HttpGet("doctors/{doctorId:guid}")]
    public async Task<ActionResult<DiscoveredDoctor>> Doctor(Guid doctorId, CancellationToken cancellationToken) =>
        Ok(await discovery.GetDoctorAsync(doctorId, cancellationToken));

    [HasPermission(PermissionCodes.DoctorsSearch)]
    [HttpGet("clinics")]
    public async Task<ActionResult<IReadOnlyList<DiscoveredClinic>>> Clinics(
        [FromQuery] string? query,
        [FromQuery] Guid? specializationId,
        [FromQuery] string? city,
        [FromQuery] string? locality,
        [FromQuery] string? postalCode,
        [FromQuery] double? latitude,
        [FromQuery] double? longitude,
        [FromQuery] double? radiusKm,
        [FromQuery] string? clinicType,
        [FromQuery] string? availability,
        [FromQuery] int? page,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await discovery.SearchClinicsAsync(
            new DiscoveryQuery(query, specializationId, null, city, latitude, longitude, radiusKm, null, null, null, null, null, null, postalCode, locality, availability, null, clinicType, page ?? 1, pageSize),
            cancellationToken));

    [HasPermission(PermissionCodes.DoctorsSearch)]
    [HttpGet("clinics/{clinicId:guid}")]
    public async Task<ActionResult<DiscoveredClinic>> Clinic(Guid clinicId, CancellationToken cancellationToken) =>
        Ok(await discovery.GetClinicAsync(clinicId, cancellationToken));

    [HasPermission(PermissionCodes.DoctorsSearch)]
    [HttpGet("suggestions")]
    public async Task<ActionResult<IReadOnlyList<SearchSuggestion>>> Suggestions([FromQuery] string? query, CancellationToken cancellationToken) =>
        Ok(await discovery.SuggestAsync(query, cancellationToken));
}
