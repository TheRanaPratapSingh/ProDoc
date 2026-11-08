using ProDoc.Application.Abstractions;
using ProDoc.Application.Providers;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Queue;
using ProDoc.Domain.Reviews;

namespace ProDoc.Application.Reviews;

public sealed class ReviewService(IReviewStore store, IClock clock)
{
    public async Task<IReadOnlyList<EligibleReviewTarget>> ListEligibleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var appointments = await store.ListCompletedForPatientAsync(userId, cancellationToken);
        var result = new List<EligibleReviewTarget>();
        foreach (var appointment in appointments.Where(item => !item.IsWalkIn).OrderByDescending(item => item.UpdatedAtUtc))
        {
            var doctorReview = await store.FindReviewByAppointmentTargetAsync(appointment.Id, ReviewTargetKind.Doctor, cancellationToken);
            var clinicReview = await store.FindReviewByAppointmentTargetAsync(appointment.Id, ReviewTargetKind.Clinic, cancellationToken);
            if (doctorReview is not null && clinicReview is not null)
            {
                continue;
            }

            result.Add(new EligibleReviewTarget(
                appointment.Id,
                appointment.Reference ?? appointment.Id.ToString("N")[..8],
                appointment.DoctorId,
                await store.FindDoctorNameAsync(appointment.DoctorId, cancellationToken) ?? "Doctor",
                appointment.ClinicId,
                await store.FindClinicNameAsync(appointment.ClinicId, cancellationToken) ?? "Clinic",
                appointment.UpdatedAtUtc,
                doctorReview is null,
                clinicReview is null));
        }

        return result;
    }

    public async Task<ReviewDetails> CreateAsync(
        Guid userId,
        Guid appointmentId,
        string targetKind,
        int? overallRating,
        int? communicationRating,
        int? cleanlinessRating,
        int? staffBehaviourRating,
        int? waitingTimeRating,
        int? consultationRating,
        string? body,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ReviewTargetKind>(targetKind, true, out var kind))
        {
            throw DomainException.Validation("review_target_invalid", "Choose doctor or clinic as the review target.");
        }

        var appointment = await RequireEligibleAppointmentAsync(userId, appointmentId, cancellationToken);
        var existing = await store.FindAnyReviewByAppointmentTargetAsync(appointmentId, kind, tracking: true, cancellationToken);
        if (existing is not null && existing.Status != ReviewStatus.Removed)
        {
            throw DomainException.Conflict("review_exists", "A verified review already exists for this appointment target.");
        }

        if (existing is not null && existing.AuthorUserId != userId)
        {
            throw DomainException.Conflict("review_exists", "A verified review already exists for this appointment target.");
        }

        PatientReview review;
        if (existing is not null)
        {
            // Soft-deleted row keeps the unique (appointment, target) key — republish instead of inserting.
            existing.Republish(
                overallRating,
                kind == ReviewTargetKind.Doctor ? communicationRating : null,
                kind == ReviewTargetKind.Clinic ? cleanlinessRating : null,
                staffBehaviourRating,
                waitingTimeRating,
                kind == ReviewTargetKind.Doctor ? consultationRating : null,
                body,
                clock.UtcNow);
            review = existing;
        }
        else
        {
            review = kind == ReviewTargetKind.Doctor
                ? PatientReview.Create(
                    appointment.Id,
                    userId,
                    kind,
                    appointment.DoctorId,
                    appointment.ClinicId,
                    overallRating,
                    communicationRating,
                    null,
                    staffBehaviourRating,
                    waitingTimeRating,
                    consultationRating,
                    body,
                    clock.UtcNow)
                : PatientReview.Create(
                    appointment.Id,
                    userId,
                    kind,
                    null,
                    appointment.ClinicId,
                    overallRating,
                    null,
                    cleanlinessRating,
                    staffBehaviourRating,
                    waitingTimeRating,
                    null,
                    body,
                    clock.UtcNow);
            store.AddReview(review);
        }

        await store.AddAuditAsync(Audit(userId, AuditActions.ReviewCreated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return await MapReviewAsync(review, userId, cancellationToken);
    }

    public async Task<ReviewDetails> UpdateAsync(
        Guid userId,
        Guid reviewId,
        int? overallRating,
        int? communicationRating,
        int? cleanlinessRating,
        int? staffBehaviourRating,
        int? waitingTimeRating,
        int? consultationRating,
        string? body,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var review = await store.FindReviewAsync(reviewId, true, cancellationToken)
            ?? throw DomainException.NotFound("review_not_found", "That review was not found.");
        if (review.AuthorUserId != userId)
        {
            throw DomainException.NotFound("review_not_found", "That review was not found.");
        }

        var hours = await EditWindowHoursAsync(cancellationToken);
        review.Edit(
            overallRating,
            communicationRating,
            cleanlinessRating,
            staffBehaviourRating,
            waitingTimeRating,
            consultationRating,
            body,
            clock.UtcNow,
            hours);
        await store.AddAuditAsync(Audit(userId, AuditActions.ReviewUpdated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return await MapReviewAsync(review, userId, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid userId,
        Guid reviewId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var review = await store.FindReviewAsync(reviewId, true, cancellationToken)
            ?? throw DomainException.NotFound("review_not_found", "That review was not found.");
        if (review.AuthorUserId != userId)
        {
            throw DomainException.NotFound("review_not_found", "That review was not found.");
        }

        var hours = await EditWindowHoursAsync(cancellationToken);
        review.Withdraw(clock.UtcNow, hours);
        await store.AddAuditAsync(Audit(userId, AuditActions.ReviewUpdated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task<ReviewDetails> GetAsync(Guid? userId, Guid reviewId, CancellationToken cancellationToken)
    {
        var review = await store.FindReviewAsync(reviewId, false, cancellationToken)
            ?? throw DomainException.NotFound("review_not_found", "That review was not found.");
        if (review.Status != ReviewStatus.Published && review.AuthorUserId != userId)
        {
            throw DomainException.NotFound("review_not_found", "That review was not found.");
        }

        return await MapReviewAsync(review, userId, cancellationToken);
    }

    public async Task<IReadOnlyList<ReviewDetails>> ListDoctorReviewsAsync(Guid doctorId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var take = NormalizePage(page, pageSize, out var skip);
        var rows = await store.ListPublishedForDoctorAsync(doctorId, skip, take, cancellationToken);
        var mapped = new List<ReviewDetails>();
        foreach (var row in rows)
        {
            mapped.Add(await MapReviewAsync(row, null, cancellationToken));
        }

        return mapped;
    }

    public async Task<IReadOnlyList<ReviewDetails>> ListClinicReviewsAsync(Guid clinicId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var take = NormalizePage(page, pageSize, out var skip);
        var rows = await store.ListPublishedForClinicAsync(clinicId, skip, take, cancellationToken);
        var mapped = new List<ReviewDetails>();
        foreach (var row in rows)
        {
            mapped.Add(await MapReviewAsync(row, null, cancellationToken));
        }

        return mapped;
    }

    public async Task<RatingSummary> DoctorRatingSummaryAsync(Guid doctorId, CancellationToken cancellationToken)
    {
        var rows = await store.ListPublishedRatingsForDoctorAsync(doctorId, cancellationToken);
        return BuildSummary(rows);
    }

    public async Task<RatingSummary> ClinicRatingSummaryAsync(Guid clinicId, CancellationToken cancellationToken)
    {
        var rows = await store.ListPublishedRatingsForClinicAsync(clinicId, cancellationToken);
        return BuildSummary(rows);
    }

    public async Task<ReviewDetails> RespondAsync(
        Guid userId,
        Guid reviewId,
        string body,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var review = await store.FindReviewAsync(reviewId, true, cancellationToken)
            ?? throw DomainException.NotFound("review_not_found", "That review was not found.");
        var (kind, _) = await EnsureCanRespondAsync(userId, review, cancellationToken);
        var response = review.AddResponse(userId, kind, body, clock.UtcNow);
        store.AddResponse(response);
        await NotifyAsync(review.AuthorUserId, "review_response", review.Id, "Provider responded", "A provider responded to your verified review.", cancellationToken);
        await store.AddAuditAsync(Audit(userId, AuditActions.ReviewResponseCreated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return await MapReviewAsync(review, userId, cancellationToken);
    }

    public async Task<ReviewDetails> EditResponseAsync(
        Guid userId,
        Guid reviewId,
        Guid responseId,
        string body,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var review = await store.FindReviewAsync(reviewId, true, cancellationToken)
            ?? throw DomainException.NotFound("review_not_found", "That review was not found.");
        await EnsureCanRespondAsync(userId, review, cancellationToken);
        review.EditResponse(responseId, userId, body, clock.UtcNow);
        await store.AddAuditAsync(Audit(userId, AuditActions.ReviewResponseUpdated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return await MapReviewAsync(review, userId, cancellationToken);
    }

    public async Task ReportAsync(Guid userId, Guid reviewId, string reason, RequestMetadata metadata, CancellationToken cancellationToken)
    {
        var review = await store.FindReviewAsync(reviewId, true, cancellationToken)
            ?? throw DomainException.NotFound("review_not_found", "That review was not found.");
        if (review.AuthorUserId == userId)
        {
            throw DomainException.Validation("cannot_report_own_review", "You cannot report your own review.");
        }

        var report = review.AddReport(userId, reason, clock.UtcNow);
        store.AddReport(report);
        await store.AddAuditAsync(Audit(userId, AuditActions.ReviewReported, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReviewModerationItem>> ListModerationAsync(string? status, CancellationToken cancellationToken)
    {
        var rows = await store.ListForModerationAsync(status, 200, cancellationToken);
        return rows.Select(item => new ReviewModerationItem(
            item.Id,
            item.AppointmentId,
            item.TargetKind.ToString(),
            item.DoctorId,
            item.ClinicId,
            item.OverallRating,
            item.Body,
            item.Status.ToString(),
            item.Reports.Count(report => report.ResolvedAtUtc is null),
            item.CreatedAtUtc)).ToArray();
    }

    public async Task<ReviewDetails> ModerateAsync(
        Guid adminUserId,
        Guid reviewId,
        string status,
        string reason,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ReviewStatus>(status, true, out var parsed))
        {
            throw DomainException.Validation("review_status_invalid", "Choose published, hidden, or removed.");
        }

        var review = await store.FindReviewAsync(reviewId, true, cancellationToken)
            ?? throw DomainException.NotFound("review_not_found", "That review was not found.");
        review.Moderate(parsed, adminUserId, reason, clock.UtcNow);
        var latest = review.ModerationEvents.OrderByDescending(item => item.CreatedAtUtc).First();
        store.AddModerationEvent(latest);
        foreach (var report in review.Reports.Where(item => item.ResolvedAtUtc is null))
        {
            report.Resolve(reason, clock.UtcNow);
        }

        await NotifyAsync(review.AuthorUserId, "review_moderation", review.Id, "Review moderation update", $"Your review was marked {parsed}.", cancellationToken);
        await store.AddAuditAsync(Audit(adminUserId, AuditActions.ReviewModerated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return await MapReviewAsync(review, adminUserId, cancellationToken);
    }

    public async Task<IReadOnlyList<ReviewDetails>> ListMineForProviderAsync(Guid userId, CancellationToken cancellationToken)
    {
        var doctor = await store.FindDoctorByUserAsync(userId, cancellationToken);
        var clinic = await store.FindClinicOwnedByUserAsync(userId, cancellationToken);
        var rows = await store.ListProviderReviewsAsync(doctor?.Id, clinic?.Id, 100, cancellationToken);
        var mapped = new List<ReviewDetails>();
        foreach (var row in rows.Where(item => item.Status == ReviewStatus.Published))
        {
            mapped.Add(await MapReviewAsync(row, userId, cancellationToken));
        }

        return mapped;
    }

    public async Task<IReadOnlyList<ReviewDetails>> ListMineForPatientAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await store.ListForAuthorAsync(userId, 50, cancellationToken);
        var mapped = new List<ReviewDetails>();
        foreach (var row in rows)
        {
            mapped.Add(await MapReviewAsync(row, userId, cancellationToken));
        }

        return mapped;
    }

    public async Task<FeedbackDetails> SubmitFeedbackAsync(
        Guid userId,
        string category,
        int? rating,
        string body,
        Guid? appointmentId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<FeedbackCategory>(category, true, out var parsed))
        {
            throw DomainException.Validation("feedback_category_invalid", "Choose a valid feedback category.");
        }

        if (appointmentId is Guid id)
        {
            var appointment = await store.FindAppointmentAsync(id, false, cancellationToken)
                ?? throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
            if (appointment.AccountUserId != userId)
            {
                throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
            }
        }

        var feedback = FeedbackSubmission.Create(userId, parsed, rating, body, appointmentId, clock.UtcNow);
        store.AddFeedback(feedback);
        await store.AddAuditAsync(Audit(userId, AuditActions.FeedbackSubmitted, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return new FeedbackDetails(feedback.Id, feedback.Category.ToString(), feedback.Rating, feedback.Body, feedback.AppointmentId, feedback.CreatedAtUtc);
    }

    public async Task<IReadOnlyList<FeedbackDetails>> ListMyFeedbackAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await store.ListFeedbackForUserAsync(userId, cancellationToken);
        return rows.Select(item => new FeedbackDetails(item.Id, item.Category.ToString(), item.Rating, item.Body, item.AppointmentId, item.CreatedAtUtc)).ToArray();
    }

    public async Task<IReadOnlyList<AdminFeedbackItem>> ListAdminFeedbackAsync(string? category, CancellationToken cancellationToken)
    {
        var rows = await store.ListFeedbackAdminAsync(category, 100, cancellationToken);
        return rows.Select(item => new AdminFeedbackItem(
            item.Id,
            item.AuthorUserId,
            item.Category.ToString(),
            item.Rating,
            item.Body,
            item.AppointmentId,
            item.CreatedAtUtc,
            PriorityFor(item))).ToArray();
    }

    private static string PriorityFor(FeedbackSubmission item)
    {
        if (item.Category is FeedbackCategory.PilotIssue && (item.Rating is null or <= 2))
        {
            return "Critical";
        }

        if (item.Rating is <= 2 || item.Category is FeedbackCategory.PilotIssue)
        {
            return "High";
        }

        if (item.Category is FeedbackCategory.PilotBilling or FeedbackCategory.PilotReception)
        {
            return "Medium";
        }

        return "Low";
    }

    public async Task NotifyReviewRequestAsync(Guid patientUserId, Guid appointmentId, CancellationToken cancellationToken)
    {
        await NotifyAsync(
            patientUserId,
            "review_request",
            appointmentId,
            "Share your visit feedback",
            "Your verified visit is complete. You can leave a rating for your doctor or clinic.",
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    private async Task<(ReviewResponderKind Kind, Guid TargetId)> EnsureCanRespondAsync(Guid userId, PatientReview review, CancellationToken cancellationToken)
    {
        if (review.TargetKind == ReviewTargetKind.Doctor && review.DoctorId is Guid doctorId)
        {
            var doctor = await store.FindDoctorByUserAsync(userId, cancellationToken);
            if (doctor?.Id == doctorId)
            {
                return (ReviewResponderKind.Doctor, doctorId);
            }
        }

        if (review.TargetKind == ReviewTargetKind.Clinic && review.ClinicId is Guid clinicId)
        {
            var clinic = await store.FindClinicOwnedByUserAsync(userId, cancellationToken);
            if (clinic?.Id == clinicId)
            {
                return (ReviewResponderKind.Clinic, clinicId);
            }
        }

        throw DomainException.Validation("review_respond_forbidden", "Only the reviewed doctor or clinic administrator can respond.");
    }

    private async Task<Appointment> RequireEligibleAppointmentAsync(Guid userId, Guid appointmentId, CancellationToken cancellationToken)
    {
        var appointment = await store.FindAppointmentAsync(appointmentId, false, cancellationToken)
            ?? throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        if (appointment.AccountUserId != userId || appointment.IsWalkIn || appointment.Status != AppointmentStatus.Completed)
        {
            throw DomainException.Validation("review_not_eligible", "Only completed, non-walk-in visits can receive verified reviews.");
        }

        return appointment;
    }

    private async Task NotifyAsync(Guid userId, string kind, Guid subjectId, string title, string body, CancellationToken cancellationToken)
    {
        var since = clock.UtcNow - QueueDefaults.NotificationCooldown;
        if (await store.HasRecentNotificationAsync(userId, kind, subjectId, since, cancellationToken))
        {
            return;
        }

        await store.NotifyAsync(UserNotification.Create(userId, kind, subjectId, title, body, clock.UtcNow), cancellationToken);
    }

    private async Task<int> EditWindowHoursAsync(CancellationToken cancellationToken)
    {
        var raw = await store.FindSettingAsync("reviews.edit_hours", cancellationToken);
        return int.TryParse(raw, out var hours) ? Math.Clamp(hours, 1, 720) : ReviewPolicy.DefaultEditWindowHours;
    }

    private static RatingSummary BuildSummary(IReadOnlyList<PatientReview> rows)
    {
        // Only overall ratings contribute to the public average — missing values are never treated as zero.
        var overallValues = rows.Where(item => item.OverallRating is not null).Select(item => item.OverallRating!.Value).ToArray();
        var overallCount = overallValues.Length;
        var averagePublic = overallCount >= ReviewPolicy.MinimumReviewsForPublicAverage;
        decimal? Avg(Func<PatientReview, int?> selector)
        {
            var values = rows.Select(selector).Where(item => item is not null).Select(item => item!.Value).ToArray();
            return values.Length == 0 ? null : Math.Round((decimal)values.Average(), 2);
        }

        var distribution = Enumerable.Range(1, 5)
            .Select(stars => new RatingBucket(stars, overallValues.Count(item => item == stars)))
            .Reverse()
            .ToArray();

        return new RatingSummary(
            overallCount,
            averagePublic ? Math.Round((decimal)overallValues.Average(), 2) : null,
            averagePublic,
            averagePublic
                ? "Average from verified completed-visit reviews."
                : "No ratings yet",
            distribution,
            averagePublic ? Avg(item => item.CommunicationRating) : null,
            averagePublic ? Avg(item => item.CleanlinessRating) : null,
            averagePublic ? Avg(item => item.StaffBehaviourRating) : null,
            averagePublic ? Avg(item => item.WaitingTimeRating) : null,
            averagePublic ? Avg(item => item.ConsultationRating) : null);
    }

    private async Task<ReviewDetails> MapReviewAsync(PatientReview review, Guid? viewerUserId, CancellationToken cancellationToken)
    {
        var hours = await EditWindowHoursAsync(cancellationToken);
        var canManage = viewerUserId == review.AuthorUserId
            && review.Status != ReviewStatus.Removed
            && clock.UtcNow <= review.CreatedAtUtc.AddHours(hours);
        var patientName = await store.FindAppointmentPatientNameAsync(review.AppointmentId, cancellationToken);
        var completedAt = await store.FindAppointmentCompletedAtAsync(review.AppointmentId, cancellationToken);
        return new ReviewDetails(
            review.Id,
            review.AppointmentId,
            review.TargetKind.ToString(),
            review.DoctorId,
            review.ClinicId,
            review.OverallRating,
            review.CommunicationRating,
            review.CleanlinessRating,
            review.StaffBehaviourRating,
            review.WaitingTimeRating,
            review.Body,
            review.IsVerifiedVisit,
            review.Status.ToString(),
            review.CreatedAtUtc,
            review.EditedAtUtc,
            canManage,
            review.Responses
                .OrderBy(item => item.CreatedAtUtc)
                .Select(item => new ReviewResponseDetails(item.Id, item.ResponderKind.ToString(), item.Body, item.EditCount, item.CreatedAtUtc, item.UpdatedAtUtc))
                .ToArray(),
            review.ConsultationRating,
            PrivacySafeAuthorName(patientName),
            completedAt,
            canManage);
    }

    private static string PrivacySafeAuthorName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return "Verified patient";
        }

        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            return parts[0];
        }

        var last = parts[^1];
        return $"{parts[0]} {char.ToUpperInvariant(last[0])}.";
    }

    private static int NormalizePage(int page, int pageSize, out int skip)
    {
        var size = pageSize is < 1 or > 50 ? 20 : pageSize;
        var current = page < 1 ? 1 : page;
        skip = (current - 1) * size;
        return size;
    }

    private AuditLog Audit(Guid userId, string action, RequestMetadata metadata) =>
        AuditLog.Record(userId, action, clock.UtcNow, metadata.IpAddress, metadata.UserAgent);
}
