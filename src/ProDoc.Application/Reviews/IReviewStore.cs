using ProDoc.Domain.Appointments;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Providers;
using ProDoc.Domain.Queue;
using ProDoc.Domain.Reviews;

namespace ProDoc.Application.Reviews;

public interface IReviewStore
{
    Task<Appointment?> FindAppointmentAsync(Guid appointmentId, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<Appointment>> ListCompletedForPatientAsync(Guid userId, CancellationToken cancellationToken);

    Task<Doctor?> FindDoctorByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<Clinic?> FindClinicOwnedByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<string?> FindDoctorNameAsync(Guid doctorId, CancellationToken cancellationToken);

    Task<string?> FindClinicNameAsync(Guid clinicId, CancellationToken cancellationToken);

    Task<PatientReview?> FindReviewAsync(Guid reviewId, bool tracking, CancellationToken cancellationToken);

    Task<PatientReview?> FindReviewByAppointmentTargetAsync(Guid appointmentId, ReviewTargetKind targetKind, CancellationToken cancellationToken);

    Task<IReadOnlyList<PatientReview>> ListPublishedForDoctorAsync(Guid doctorId, int skip, int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<PatientReview>> ListPublishedForClinicAsync(Guid clinicId, int skip, int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<PatientReview>> ListPublishedRatingsForDoctorAsync(Guid doctorId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PatientReview>> ListPublishedRatingsForClinicAsync(Guid clinicId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PatientReview>> ListForModerationAsync(string? status, int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<PatientReview>> ListProviderReviewsAsync(Guid? doctorId, Guid? clinicId, int take, CancellationToken cancellationToken);

    void AddReview(PatientReview review);

    void AddResponse(ReviewResponse response);

    void AddReport(ReviewReport report);

    void AddModerationEvent(ReviewModerationEvent entry);

    void AddFeedback(FeedbackSubmission feedback);

    Task<IReadOnlyList<FeedbackSubmission>> ListFeedbackForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<FeedbackSubmission>> ListFeedbackAdminAsync(string? category, int take, CancellationToken cancellationToken);

    Task<bool> HasRecentNotificationAsync(Guid userId, string kind, Guid? subjectId, DateTimeOffset since, CancellationToken cancellationToken);

    Task NotifyAsync(UserNotification notification, CancellationToken cancellationToken);

    Task<string?> FindSettingAsync(string key, CancellationToken cancellationToken);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
