using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Application.Scheduling;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Route("api/v1/discovery")]
public sealed class DiscoveryController(DiscoveryService discovery) : ControllerBase
{
    [HasPermission(PermissionCodes.DoctorsSearch)]
    [HttpGet]
    public Task<ActionResult<IReadOnlyList<DiscoveryPlace>>> Search(
        [FromQuery] string? query,
        [FromQuery] Guid? specializationId,
        [FromQuery] Guid? clinicId,
        [FromQuery] string? city,
        [FromQuery] double? latitude,
        [FromQuery] double? longitude,
        [FromQuery] double? radiusKm,
        [FromQuery] decimal? maxFee,
        CancellationToken cancellationToken) =>
        SearchDoctors(query, specializationId, clinicId, city, null, latitude, longitude, radiusKm, null, maxFee, null, null, null, null, null, null, null, null, 1, 20, cancellationToken);

    [HasPermission(PermissionCodes.DoctorsSearch)]
    [HttpGet("doctors/{doctorId:guid}")]
    public async Task<ActionResult<DiscoveredDoctor>> Doctor(Guid doctorId, CancellationToken cancellationToken) =>
        Ok(await discovery.GetDoctorAsync(doctorId, cancellationToken));

    internal async Task<ActionResult<IReadOnlyList<DiscoveryPlace>>> SearchDoctors(
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
            new DiscoveryQuery(
                query,
                specializationId,
                clinicId,
                city,
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
                locality,
                availability,
                sort,
                clinicType,
                page ?? 1,
                pageSize),
            cancellationToken));
}
