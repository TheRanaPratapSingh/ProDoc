namespace ProDoc.Application.Reviews;

public sealed record RatingSummary(
    int PublishedReviewCount,
    decimal? AverageOverall,
    bool AverageIsPublic,
    string DisplayNote,
    IReadOnlyList<RatingBucket> Distribution,
    decimal? AverageCommunication,
    decimal? AverageCleanliness,
    decimal? AverageStaffBehaviour,
    decimal? AverageWaitingTime);

public sealed record RatingBucket(int Stars, int Count);

public sealed record ReviewResponseDetails(
    Guid Id,
    string ResponderKind,
    string Body,
    int EditCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record ReviewDetails(
    Guid Id,
    Guid AppointmentId,
    string TargetKind,
    Guid? DoctorId,
    Guid? ClinicId,
    int OverallRating,
    int? CommunicationRating,
    int? CleanlinessRating,
    int? StaffBehaviourRating,
    int? WaitingTimeRating,
    string? Body,
    bool IsVerifiedVisit,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? EditedAtUtc,
    bool CanEdit,
    IReadOnlyList<ReviewResponseDetails> Responses);

public sealed record EligibleReviewTarget(
    Guid AppointmentId,
    string AppointmentReference,
    Guid DoctorId,
    string DoctorName,
    Guid ClinicId,
    string ClinicName,
    DateTimeOffset CompletedAtUtc,
    bool CanReviewDoctor,
    bool CanReviewClinic);

public sealed record ReviewModerationItem(
    Guid Id,
    Guid AppointmentId,
    string TargetKind,
    Guid? DoctorId,
    Guid? ClinicId,
    int OverallRating,
    string? Body,
    string Status,
    int OpenReportCount,
    DateTimeOffset CreatedAtUtc);

public sealed record FeedbackDetails(
    Guid Id,
    string Category,
    int? Rating,
    string Body,
    Guid? AppointmentId,
    DateTimeOffset CreatedAtUtc);

public sealed record AdminFeedbackItem(
    Guid Id,
    Guid AuthorUserId,
    string Category,
    int? Rating,
    string Body,
    Guid? AppointmentId,
    DateTimeOffset CreatedAtUtc,
    string Priority);
