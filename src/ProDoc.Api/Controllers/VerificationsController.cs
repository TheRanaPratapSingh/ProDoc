using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Application.Providers;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class VerificationsController(
    ProviderDirectoryService directory,
    ProviderDocumentService documents,
    VerificationReviewService reviews) : ControllerBase
{
    [Authorize]
    [HttpGet("specializations")]
    public async Task<ActionResult<IReadOnlyList<SpecializationModel>>> Specializations(CancellationToken cancellationToken)
    {
        if (!Has(PermissionCodes.DoctorsSearch) && !Has(PermissionCodes.ProviderOnboard))
        {
            return Forbid();
        }

        return Ok(await directory.ListSpecializationsAsync(cancellationToken));
    }

    [Authorize]
    [HttpGet("provider-documents/{documentId:guid}")]
    public async Task<IActionResult> Download(Guid documentId, CancellationToken cancellationToken)
    {
        var download = await documents.OpenAsync(
            User.GetUserId(),
            Has(PermissionCodes.VerificationReview),
            Has(PermissionCodes.DoctorsSearch),
            documentId,
            cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(download.Content, download.ContentType, download.FileName);
    }

    [HasPermission(PermissionCodes.VerificationReview)]
    [HttpGet("admin/verifications")]
    public async Task<ActionResult<IReadOnlyList<ReviewQueueItem>>> Queue(
        [FromQuery] string? kind,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        Domain.Providers.VerificationStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<Domain.Providers.VerificationStatus>(status, ignoreCase: true, out var value) || !Enum.IsDefined(value))
            {
                return BadRequest();
            }

            parsed = value;
        }

        return Ok(await reviews.ListAsync(kind, parsed, cancellationToken));
    }

    [HasPermission(PermissionCodes.VerificationReview)]
    [HttpPost("admin/verifications/doctors/{doctorId:guid}")]
    public async Task<ActionResult<DoctorProfile>> ReviewDoctor(
        Guid doctorId,
        Contracts.ReviewRequest request,
        CancellationToken cancellationToken) =>
        Ok(await reviews.ReviewDoctorAsync(
            User.GetUserId(),
            doctorId,
            ParseDecision(request.Decision),
            request.Reason,
            Metadata(),
            cancellationToken));

    [HasPermission(PermissionCodes.VerificationReview)]
    [HttpPost("admin/verifications/clinics/{clinicId:guid}")]
    public async Task<ActionResult<ClinicProfile>> ReviewClinic(
        Guid clinicId,
        Contracts.ReviewRequest request,
        CancellationToken cancellationToken) =>
        Ok(await reviews.ReviewClinicAsync(
            User.GetUserId(),
            clinicId,
            ParseDecision(request.Decision),
            request.Reason,
            Metadata(),
            cancellationToken));

    private bool Has(string permission) => User.Claims.Any(claim => claim.Type == "perm" && claim.Value == permission);

    private RequestMetadata Metadata() => new(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext));

    private static Domain.Providers.ReviewDecision ParseDecision(string value)
    {
        if (!Enum.TryParse<Domain.Providers.ReviewDecision>(value, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
        {
            throw Domain.Common.DomainException.Validation("review_invalid", "Choose a review decision.");
        }

        return parsed;
    }
}
