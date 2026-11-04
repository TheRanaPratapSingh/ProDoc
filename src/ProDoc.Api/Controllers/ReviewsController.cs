using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Application.Providers;
using ProDoc.Application.Reviews;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class ReviewsController(ReviewService reviews) : ControllerBase
{
    [HasPermission(PermissionCodes.ReviewCreate)]
    [HttpGet("reviews/eligible")]
    public Task<IReadOnlyList<EligibleReviewTarget>> Eligible(CancellationToken cancellationToken) =>
        reviews.ListEligibleAsync(User.GetUserId(), cancellationToken);

    [HasPermission(PermissionCodes.ReviewCreate)]
    [HttpPost("reviews")]
    public Task<ReviewDetails> Create([FromBody] CreateReviewRequest request, CancellationToken cancellationToken) =>
        reviews.CreateAsync(
            User.GetUserId(),
            request.AppointmentId,
            request.TargetKind,
            request.OverallRating,
            request.CommunicationRating,
            request.CleanlinessRating,
            request.StaffBehaviourRating,
            request.WaitingTimeRating,
            request.Body,
            Meta(),
            cancellationToken);

    [HasPermission(PermissionCodes.ReviewCreate)]
    [HttpPut("reviews/{reviewId:guid}")]
    public Task<ReviewDetails> Update(Guid reviewId, [FromBody] UpdateReviewRequest request, CancellationToken cancellationToken) =>
        reviews.UpdateAsync(
            User.GetUserId(),
            reviewId,
            request.OverallRating,
            request.CommunicationRating,
            request.CleanlinessRating,
            request.StaffBehaviourRating,
            request.WaitingTimeRating,
            request.Body,
            Meta(),
            cancellationToken);

    [HttpGet("reviews/{reviewId:guid}")]
    public Task<ReviewDetails> Get(Guid reviewId, CancellationToken cancellationToken) =>
        reviews.GetAsync(User.GetUserId(), reviewId, cancellationToken);

    [AllowAnonymous]
    [HttpGet("doctors/{doctorId:guid}/reviews")]
    public Task<IReadOnlyList<ReviewDetails>> DoctorReviews(Guid doctorId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        reviews.ListDoctorReviewsAsync(doctorId, page, pageSize, cancellationToken);

    [AllowAnonymous]
    [HttpGet("clinics/{clinicId:guid}/reviews")]
    public Task<IReadOnlyList<ReviewDetails>> ClinicReviews(Guid clinicId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        reviews.ListClinicReviewsAsync(clinicId, page, pageSize, cancellationToken);

    [AllowAnonymous]
    [HttpGet("doctors/{doctorId:guid}/rating-summary")]
    public Task<RatingSummary> DoctorSummary(Guid doctorId, CancellationToken cancellationToken) =>
        reviews.DoctorRatingSummaryAsync(doctorId, cancellationToken);

    [AllowAnonymous]
    [HttpGet("clinics/{clinicId:guid}/rating-summary")]
    public Task<RatingSummary> ClinicSummary(Guid clinicId, CancellationToken cancellationToken) =>
        reviews.ClinicRatingSummaryAsync(clinicId, cancellationToken);

    [HasPermission(PermissionCodes.ReviewRespond)]
    [HttpGet("reviews/inbox")]
    public Task<IReadOnlyList<ReviewDetails>> ProviderInbox(CancellationToken cancellationToken) =>
        reviews.ListMineForProviderAsync(User.GetUserId(), cancellationToken);

    [HasPermission(PermissionCodes.ReviewRespond)]
    [HttpPost("reviews/{reviewId:guid}/responses")]
    public Task<ReviewDetails> Respond(Guid reviewId, [FromBody] ReviewResponseRequest request, CancellationToken cancellationToken) =>
        reviews.RespondAsync(User.GetUserId(), reviewId, request.Body, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.ReviewRespond)]
    [HttpPut("reviews/{reviewId:guid}/responses/{responseId:guid}")]
    public Task<ReviewDetails> EditResponse(Guid reviewId, Guid responseId, [FromBody] ReviewResponseRequest request, CancellationToken cancellationToken) =>
        reviews.EditResponseAsync(User.GetUserId(), reviewId, responseId, request.Body, Meta(), cancellationToken);

    [Authorize]
    [HttpPost("reviews/{reviewId:guid}/reports")]
    public async Task<IActionResult> Report(Guid reviewId, [FromBody] ReportReviewRequest request, CancellationToken cancellationToken)
    {
        await reviews.ReportAsync(User.GetUserId(), reviewId, request.Reason, Meta(), cancellationToken);
        return NoContent();
    }

    [HasPermission(PermissionCodes.ReviewModerate)]
    [HttpGet("admin/reviews")]
    public Task<IReadOnlyList<ReviewModerationItem>> Moderation([FromQuery] string? status, CancellationToken cancellationToken) =>
        reviews.ListModerationAsync(status, cancellationToken);

    [HasPermission(PermissionCodes.ReviewModerate)]
    [HttpPost("admin/reviews/{reviewId:guid}/moderate")]
    public Task<ReviewDetails> Moderate(Guid reviewId, [FromBody] ModerateReviewRequest request, CancellationToken cancellationToken) =>
        reviews.ModerateAsync(User.GetUserId(), reviewId, request.Status, request.Reason, Meta(), cancellationToken);

    [Authorize]
    [HttpPost("feedback")]
    public Task<FeedbackDetails> Feedback([FromBody] FeedbackRequest request, CancellationToken cancellationToken) =>
        reviews.SubmitFeedbackAsync(User.GetUserId(), request.Category, request.Rating, request.Body, request.AppointmentId, Meta(), cancellationToken);

    [Authorize]
    [HttpGet("feedback/mine")]
    public Task<IReadOnlyList<FeedbackDetails>> MyFeedback(CancellationToken cancellationToken) =>
        reviews.ListMyFeedbackAsync(User.GetUserId(), cancellationToken);

    [HasPermission(PermissionCodes.AdminSupport)]
    [HttpGet("admin/feedback")]
    public Task<IReadOnlyList<AdminFeedbackItem>> AdminFeedback([FromQuery] string? category, CancellationToken cancellationToken) =>
        reviews.ListAdminFeedbackAsync(category, cancellationToken);

    private RequestMetadata Meta() => new(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext));
}

public sealed record CreateReviewRequest(
    Guid AppointmentId,
    string TargetKind,
    int OverallRating,
    int? CommunicationRating,
    int? CleanlinessRating,
    int? StaffBehaviourRating,
    int? WaitingTimeRating,
    string? Body);

public sealed record UpdateReviewRequest(
    int OverallRating,
    int? CommunicationRating,
    int? CleanlinessRating,
    int? StaffBehaviourRating,
    int? WaitingTimeRating,
    string? Body);

public sealed record ReviewResponseRequest(string Body);

public sealed record ReportReviewRequest(string Reason);

public sealed record ModerateReviewRequest(string Status, string Reason);

public sealed record FeedbackRequest(string Category, int? Rating, string Body, Guid? AppointmentId);
