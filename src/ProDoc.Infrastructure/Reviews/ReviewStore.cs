using Microsoft.EntityFrameworkCore;
using ProDoc.Application.Reviews;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Providers;
using ProDoc.Domain.Queue;
using ProDoc.Domain.Reviews;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.Reviews;

public sealed class ReviewStore(ProDocDbContext db) : IReviewStore
{
    public Task<Appointment?> FindAppointmentAsync(Guid appointmentId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.Appointments.AsQueryable() : db.Appointments.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == appointmentId, cancellationToken);
    }

    public async Task<IReadOnlyList<Appointment>> ListCompletedForPatientAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Appointments.AsNoTracking()
            .Where(item => item.AccountUserId == userId && item.Status == AppointmentStatus.Completed && !item.IsWalkIn)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

    public Task<Doctor?> FindDoctorByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Doctors.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);

    public Task<Clinic?> FindClinicOwnedByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Clinics.AsNoTracking().FirstOrDefaultAsync(item => item.OwnerUserId == userId, cancellationToken);

    public async Task<string?> FindDoctorNameAsync(Guid doctorId, CancellationToken cancellationToken)
    {
        var doctor = await db.Doctors.AsNoTracking().SingleOrDefaultAsync(item => item.Id == doctorId, cancellationToken);
        if (doctor is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(doctor.DisplayName)
            ? $"{doctor.FirstName} {doctor.LastName}".Trim()
            : doctor.DisplayName;
    }

    public async Task<string?> FindClinicNameAsync(Guid clinicId, CancellationToken cancellationToken)
    {
        var clinic = await db.Clinics.AsNoTracking().SingleOrDefaultAsync(item => item.Id == clinicId, cancellationToken);
        return clinic?.Name;
    }

    public Task<PatientReview?> FindReviewAsync(Guid reviewId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.PatientReviews.AsQueryable() : db.PatientReviews.AsNoTracking();
        return query
            .Include(item => item.Responses)
            .Include(item => item.Reports)
            .Include(item => item.ModerationEvents)
            .SingleOrDefaultAsync(item => item.Id == reviewId, cancellationToken);
    }

    public Task<PatientReview?> FindReviewByAppointmentTargetAsync(Guid appointmentId, ReviewTargetKind targetKind, CancellationToken cancellationToken) =>
        db.PatientReviews.AsNoTracking()
            .SingleOrDefaultAsync(item => item.AppointmentId == appointmentId && item.TargetKind == targetKind, cancellationToken);

    public async Task<IReadOnlyList<PatientReview>> ListPublishedForDoctorAsync(Guid doctorId, int skip, int take, CancellationToken cancellationToken) =>
        await db.PatientReviews.AsNoTracking()
            .Include(item => item.Responses)
            .Where(item => item.DoctorId == doctorId && item.TargetKind == ReviewTargetKind.Doctor && item.Status == ReviewStatus.Published)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PatientReview>> ListPublishedForClinicAsync(Guid clinicId, int skip, int take, CancellationToken cancellationToken) =>
        await db.PatientReviews.AsNoTracking()
            .Include(item => item.Responses)
            .Where(item => item.ClinicId == clinicId && item.TargetKind == ReviewTargetKind.Clinic && item.Status == ReviewStatus.Published)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PatientReview>> ListPublishedRatingsForDoctorAsync(Guid doctorId, CancellationToken cancellationToken) =>
        await db.PatientReviews.AsNoTracking()
            .Where(item => item.DoctorId == doctorId && item.TargetKind == ReviewTargetKind.Doctor && item.Status == ReviewStatus.Published)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PatientReview>> ListPublishedRatingsForClinicAsync(Guid clinicId, CancellationToken cancellationToken) =>
        await db.PatientReviews.AsNoTracking()
            .Where(item => item.ClinicId == clinicId && item.TargetKind == ReviewTargetKind.Clinic && item.Status == ReviewStatus.Published)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PatientReview>> ListForModerationAsync(string? status, int take, CancellationToken cancellationToken)
    {
        var query = db.PatientReviews.AsNoTracking()
            .Include(item => item.Reports)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ReviewStatus>(status, true, out var parsed))
        {
            query = query.Where(item => item.Status == parsed);
        }
        else
        {
            query = query.Where(item => item.Reports.Any(report => report.ResolvedAtUtc == null) || item.Status != ReviewStatus.Published);
        }

        return await query.OrderByDescending(item => item.UpdatedAtUtc).Take(take).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PatientReview>> ListProviderReviewsAsync(Guid? doctorId, Guid? clinicId, int take, CancellationToken cancellationToken)
    {
        if (doctorId is null && clinicId is null)
        {
            return [];
        }

        return await db.PatientReviews.AsNoTracking()
            .Include(item => item.Responses)
            .Where(item =>
                (doctorId != null && item.DoctorId == doctorId && item.TargetKind == ReviewTargetKind.Doctor)
                || (clinicId != null && item.ClinicId == clinicId && item.TargetKind == ReviewTargetKind.Clinic))
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public void AddReview(PatientReview review) => db.PatientReviews.Add(review);

    public void AddResponse(ReviewResponse response) => db.ReviewResponses.Add(response);

    public void AddReport(ReviewReport report) => db.ReviewReports.Add(report);

    public void AddModerationEvent(ReviewModerationEvent entry) => db.ReviewModerationEvents.Add(entry);

    public void AddFeedback(FeedbackSubmission feedback) => db.FeedbackSubmissions.Add(feedback);

    public async Task<IReadOnlyList<FeedbackSubmission>> ListFeedbackForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.FeedbackSubmissions.AsNoTracking()
            .Where(item => item.AuthorUserId == userId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FeedbackSubmission>> ListFeedbackAdminAsync(string? category, int take, CancellationToken cancellationToken)
    {
        var query = db.FeedbackSubmissions.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<FeedbackCategory>(category, true, out var parsed))
        {
            query = query.Where(item => item.Category == parsed);
        }

        return await query
            .OrderByDescending(item => item.Rating == null)
            .ThenBy(item => item.Rating)
            .ThenByDescending(item => item.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasRecentNotificationAsync(Guid userId, string kind, Guid? subjectId, DateTimeOffset since, CancellationToken cancellationToken) =>
        db.UserNotifications.AnyAsync(
            item => item.UserId == userId && item.Kind == kind && item.SubjectId == subjectId && item.CreatedAtUtc >= since,
            cancellationToken);

    public Task NotifyAsync(UserNotification notification, CancellationToken cancellationToken)
    {
        db.UserNotifications.Add(notification);
        return Task.CompletedTask;
    }

    public async Task<string?> FindSettingAsync(string key, CancellationToken cancellationToken) =>
        await db.PlatformSettings.AsNoTracking().Where(item => item.Key == key).Select(item => item.Value).SingleOrDefaultAsync(cancellationToken);

    public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(audit);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
