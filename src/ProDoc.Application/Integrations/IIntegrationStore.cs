using ProDoc.Domain.Appointments;
using ProDoc.Domain.Clinical;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Integrations;
using ProDoc.Domain.Providers;
using ProDoc.Domain.Queue;

namespace ProDoc.Application.Integrations;

public interface IIntegrationStore
{
    Task<IReadOnlyList<IntegrationProvider>> ListProvidersAsync(CancellationToken cancellationToken);

    Task<IntegrationProvider?> FindProviderByCodeAsync(string code, bool tracking, CancellationToken cancellationToken);

    Task<IntegrationProvider?> FindProviderAsync(Guid id, bool tracking, CancellationToken cancellationToken);

    void AddProvider(IntegrationProvider provider);

    Task<IntegrationConsent?> FindConsentAsync(Guid userId, IntegrationProviderKind kind, string purpose, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<IntegrationConsent>> ListConsentsAsync(Guid userId, CancellationToken cancellationToken);

    void AddConsent(IntegrationConsent consent);

    Task<IntegrationDeliveryLog?> FindDeliveryByIdempotencyAsync(string idempotencyKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<IntegrationDeliveryLog>> ListRecentDeliveriesAsync(Guid? userReferenceHint, int take, CancellationToken cancellationToken);

    void AddDelivery(IntegrationDeliveryLog log);

    Task<IntegrationWebhookEvent?> FindWebhookAsync(string providerCode, string eventId, CancellationToken cancellationToken);

    void AddWebhook(IntegrationWebhookEvent webhook);

    Task<LabOrder?> FindLabOrderByExternalAsync(string providerCode, string externalOrderId, bool tracking, CancellationToken cancellationToken);

    Task<LabOrder?> FindLabOrderAsync(Guid orderId, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<LabOrder>> ListLabOrdersAsync(Guid userId, CancellationToken cancellationToken);

    void AddLabOrder(LabOrder order);

    Task<IReadOnlyList<LabReport>> ListLabReportsAsync(Guid userId, CancellationToken cancellationToken);

    Task<LabReport?> FindLabReportAsync(Guid reportId, CancellationToken cancellationToken);

    Task<bool> LabReportExistsForOrderAsync(Guid labOrderId, CancellationToken cancellationToken);

    void AddLabReport(LabReport report);

    Task<PharmacyShare?> FindPharmacyShareByExternalAsync(string providerCode, string externalShareId, bool tracking, CancellationToken cancellationToken);

    Task<PharmacyShare?> FindPharmacyShareAsync(Guid shareId, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<PharmacyShare>> ListPharmacySharesAsync(Guid userId, CancellationToken cancellationToken);

    void AddPharmacyShare(PharmacyShare share);

    Task<AbhaLink?> FindAbhaLinkAsync(Guid userId, bool tracking, CancellationToken cancellationToken);

    void AddAbhaLink(AbhaLink link);

    Task<(string? FullName, string? Phone)> FindUserProfileAsync(Guid userId, CancellationToken cancellationToken);

    Task<PatientMedicalRecord?> FindPrimaryMedicalRecordAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Appointment>> ListRecentAppointmentsAsync(Guid userId, int take, CancellationToken cancellationToken);

    Task<Appointment?> FindAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken);

    Task<Prescription?> FindPrescriptionAsync(Guid prescriptionId, CancellationToken cancellationToken);

    Task<Doctor?> FindDoctorAsync(Guid doctorId, CancellationToken cancellationToken);

    Task<Clinic?> FindClinicAsync(Guid clinicId, CancellationToken cancellationToken);

    Task NotifyAsync(UserNotification notification, CancellationToken cancellationToken);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
