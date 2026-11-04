using ProDoc.Domain.Common;

namespace ProDoc.Domain.Reviews;

public enum ReviewTargetKind
{
    Doctor = 1,
    Clinic = 2
}

public enum ReviewStatus
{
    Published = 1,
    Hidden = 2,
    Removed = 3
}

public enum ReviewResponderKind
{
    Doctor = 1,
    Clinic = 2
}

public enum FeedbackCategory
{
    AppointmentExperience = 1,
    DoctorConsultation = 2,
    ClinicExperience = 3,
    BookingExperience = 4,
    PlatformUsability = 5,
    /// <summary>Pilot-only: product bug or broken workflow report.</summary>
    PilotIssue = 6,
    /// <summary>Pilot-only: willingness to pay / subscription pricing feedback.</summary>
    PilotBilling = 7,
    /// <summary>Pilot-only: receptionist or front-desk usability.</summary>
    PilotReception = 8
}

public static class ReviewPolicy
{
    public const int DefaultEditWindowHours = 48;

    public const int MinimumReviewsForPublicAverage = 3;

    public const int MaxBodyLength = 2000;

    public const int MaxResponseLength = 1000;

    public static int ClampRating(int rating, string code)
    {
        if (rating is < 1 or > 5)
        {
            throw DomainException.Validation(code, "Choose a rating from 1 to 5 stars.");
        }

        return rating;
    }
}

public sealed class PatientReview
{
    private readonly List<ReviewResponse> _responses = [];
    private readonly List<ReviewReport> _reports = [];
    private readonly List<ReviewModerationEvent> _moderationEvents = [];

    private PatientReview()
    {
    }

    public Guid Id { get; private set; }

    public Guid AppointmentId { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public ReviewTargetKind TargetKind { get; private set; }

    public Guid? DoctorId { get; private set; }

    public Guid? ClinicId { get; private set; }

    public int OverallRating { get; private set; }

    public int? CommunicationRating { get; private set; }

    public int? CleanlinessRating { get; private set; }

    public int? StaffBehaviourRating { get; private set; }

    public int? WaitingTimeRating { get; private set; }

    public string? Body { get; private set; }

    public bool IsVerifiedVisit { get; private set; } = true;

    public ReviewStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? EditedAtUtc { get; private set; }

    public IReadOnlyCollection<ReviewResponse> Responses => _responses;

    public IReadOnlyCollection<ReviewReport> Reports => _reports;

    public IReadOnlyCollection<ReviewModerationEvent> ModerationEvents => _moderationEvents;

    public static PatientReview Create(
        Guid appointmentId,
        Guid authorUserId,
        ReviewTargetKind targetKind,
        Guid? doctorId,
        Guid? clinicId,
        int overallRating,
        int? communicationRating,
        int? cleanlinessRating,
        int? staffBehaviourRating,
        int? waitingTimeRating,
        string? body,
        DateTimeOffset now)
    {
        if (targetKind == ReviewTargetKind.Doctor && doctorId is null)
        {
            throw DomainException.Validation("review_target_invalid", "Doctor reviews require a doctor.");
        }

        if (targetKind == ReviewTargetKind.Clinic && clinicId is null)
        {
            throw DomainException.Validation("review_target_invalid", "Clinic reviews require a clinic.");
        }

        var review = new PatientReview
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointmentId,
            AuthorUserId = authorUserId,
            TargetKind = targetKind,
            DoctorId = doctorId,
            ClinicId = clinicId,
            IsVerifiedVisit = true,
            Status = ReviewStatus.Published,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        review.ApplyRatings(overallRating, communicationRating, cleanlinessRating, staffBehaviourRating, waitingTimeRating, body, now, isEdit: false);
        return review;
    }

    public void Edit(
        int overallRating,
        int? communicationRating,
        int? cleanlinessRating,
        int? staffBehaviourRating,
        int? waitingTimeRating,
        string? body,
        DateTimeOffset now,
        int editWindowHours)
    {
        if (Status == ReviewStatus.Removed)
        {
            throw DomainException.Validation("review_removed", "That review can no longer be edited.");
        }

        if (now > CreatedAtUtc.AddHours(Math.Max(1, editWindowHours)))
        {
            throw DomainException.Validation("review_edit_closed", "The edit window for this review has closed.");
        }

        ApplyRatings(overallRating, communicationRating, cleanlinessRating, staffBehaviourRating, waitingTimeRating, body, now, isEdit: true);
    }

    public void Moderate(ReviewStatus status, Guid moderatorUserId, string reason, DateTimeOffset now)
    {
        if (status is not (ReviewStatus.Published or ReviewStatus.Hidden or ReviewStatus.Removed))
        {
            throw DomainException.Validation("review_status_invalid", "Choose a valid moderation status.");
        }

        var text = RequireReason(reason);
        var from = Status;
        Status = status;
        UpdatedAtUtc = now;
        var entry = ReviewModerationEvent.Record(Id, moderatorUserId, from, status, text, now);
        _moderationEvents.Add(entry);
    }

    public ReviewResponse AddResponse(Guid authorUserId, ReviewResponderKind kind, string body, DateTimeOffset now)
    {
        if (Status != ReviewStatus.Published)
        {
            throw DomainException.Validation("review_not_public", "Only published reviews can receive responses.");
        }

        if (_responses.Any(item => item.ResponderKind == kind))
        {
            throw DomainException.Conflict("review_response_exists", "A response already exists for this review target.");
        }

        var response = ReviewResponse.Create(Id, authorUserId, kind, body, now);
        _responses.Add(response);
        UpdatedAtUtc = now;
        return response;
    }

    public ReviewResponse EditResponse(Guid responseId, Guid authorUserId, string body, DateTimeOffset now)
    {
        var response = _responses.SingleOrDefault(item => item.Id == responseId)
            ?? throw DomainException.NotFound("review_response_not_found", "That review response was not found.");
        if (response.AuthorUserId != authorUserId)
        {
            throw DomainException.NotFound("review_response_not_found", "That review response was not found.");
        }

        response.Edit(body, now);
        UpdatedAtUtc = now;
        return response;
    }

    public ReviewReport AddReport(Guid reporterUserId, string reason, DateTimeOffset now)
    {
        if (_reports.Any(item => item.ReporterUserId == reporterUserId && item.ResolvedAtUtc is null))
        {
            throw DomainException.Conflict("review_report_exists", "You already reported this review.");
        }

        var report = ReviewReport.Create(Id, reporterUserId, reason, now);
        _reports.Add(report);
        UpdatedAtUtc = now;
        return report;
    }

    private void ApplyRatings(
        int overallRating,
        int? communicationRating,
        int? cleanlinessRating,
        int? staffBehaviourRating,
        int? waitingTimeRating,
        string? body,
        DateTimeOffset now,
        bool isEdit)
    {
        OverallRating = ReviewPolicy.ClampRating(overallRating, "rating_invalid");
        CommunicationRating = OptionalRating(communicationRating, "communication_rating_invalid");
        CleanlinessRating = OptionalRating(cleanlinessRating, "cleanliness_rating_invalid");
        StaffBehaviourRating = OptionalRating(staffBehaviourRating, "staff_rating_invalid");
        WaitingTimeRating = OptionalRating(waitingTimeRating, "waiting_rating_invalid");
        Body = NormalizeBody(body);
        UpdatedAtUtc = now;
        if (isEdit)
        {
            EditedAtUtc = now;
        }
    }

    private static int? OptionalRating(int? rating, string code) =>
        rating is null ? null : ReviewPolicy.ClampRating(rating.Value, code);

    private static string? NormalizeBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var text = body.Trim();
        if (text.Length > ReviewPolicy.MaxBodyLength)
        {
            throw DomainException.Validation("review_body_invalid", $"Keep written feedback to {ReviewPolicy.MaxBodyLength} characters.");
        }

        return text;
    }

    private static string RequireReason(string reason)
    {
        var text = reason.Trim();
        if (text.Length is < 4 or > 500)
        {
            throw DomainException.Validation("moderation_reason_invalid", "Enter a moderation reason from 4 to 500 characters.");
        }

        return text;
    }
}

public sealed class ReviewResponse
{
    private ReviewResponse()
    {
    }

    public Guid Id { get; private set; }

    public Guid ReviewId { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public ReviewResponderKind ResponderKind { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public int EditCount { get; private set; }

    public static ReviewResponse Create(Guid reviewId, Guid authorUserId, ReviewResponderKind kind, string body, DateTimeOffset now)
    {
        return new ReviewResponse
        {
            Id = Guid.NewGuid(),
            ReviewId = reviewId,
            AuthorUserId = authorUserId,
            ResponderKind = kind,
            Body = Normalize(body),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Edit(string body, DateTimeOffset now)
    {
        Body = Normalize(body);
        EditCount += 1;
        UpdatedAtUtc = now;
    }

    private static string Normalize(string body)
    {
        var text = body.Trim();
        if (text.Length is < 2 or > ReviewPolicy.MaxResponseLength)
        {
            throw DomainException.Validation("review_response_invalid", $"Enter a response from 2 to {ReviewPolicy.MaxResponseLength} characters.");
        }

        return text;
    }
}

public sealed class ReviewReport
{
    private ReviewReport()
    {
    }

    public Guid Id { get; private set; }

    public Guid ReviewId { get; private set; }

    public Guid ReporterUserId { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public string? ResolutionNote { get; private set; }

    public static ReviewReport Create(Guid reviewId, Guid reporterUserId, string reason, DateTimeOffset now)
    {
        var text = reason.Trim();
        if (text.Length is < 4 or > 500)
        {
            throw DomainException.Validation("report_reason_invalid", "Enter a report reason from 4 to 500 characters.");
        }

        return new ReviewReport
        {
            Id = Guid.NewGuid(),
            ReviewId = reviewId,
            ReporterUserId = reporterUserId,
            Reason = text,
            CreatedAtUtc = now
        };
    }

    public void Resolve(string? note, DateTimeOffset now)
    {
        ResolvedAtUtc = now;
        ResolutionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, 500)];
    }
}

public sealed class ReviewModerationEvent
{
    private ReviewModerationEvent()
    {
    }

    public Guid Id { get; private set; }

    public Guid ReviewId { get; private set; }

    public Guid ModeratorUserId { get; private set; }

    public ReviewStatus FromStatus { get; private set; }

    public ReviewStatus ToStatus { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static ReviewModerationEvent Record(
        Guid reviewId,
        Guid moderatorUserId,
        ReviewStatus from,
        ReviewStatus to,
        string reason,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            ReviewId = reviewId,
            ModeratorUserId = moderatorUserId,
            FromStatus = from,
            ToStatus = to,
            Reason = reason,
            CreatedAtUtc = now
        };
}

public sealed class FeedbackSubmission
{
    private FeedbackSubmission()
    {
    }

    public Guid Id { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public FeedbackCategory Category { get; private set; }

    public int? Rating { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public Guid? AppointmentId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static FeedbackSubmission Create(
        Guid authorUserId,
        FeedbackCategory category,
        int? rating,
        string body,
        Guid? appointmentId,
        DateTimeOffset now)
    {
        var text = body.Trim();
        if (text.Length is < 8 or > 2000)
        {
            throw DomainException.Validation("feedback_body_invalid", "Enter feedback from 8 to 2000 characters.");
        }

        return new FeedbackSubmission
        {
            Id = Guid.NewGuid(),
            AuthorUserId = authorUserId,
            Category = category,
            Rating = rating is null ? null : ReviewPolicy.ClampRating(rating.Value, "feedback_rating_invalid"),
            Body = text,
            AppointmentId = appointmentId,
            CreatedAtUtc = now
        };
    }
}
