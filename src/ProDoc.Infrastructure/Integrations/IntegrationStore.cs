using Microsoft.EntityFrameworkCore;
using ProDoc.Application.Integrations;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Clinical;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Integrations;
using ProDoc.Domain.Providers;
using ProDoc.Domain.Queue;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.Integrations;

public sealed class IntegrationStore(ProDocDbContext db) : IIntegrationStore
{
    public async Task<IReadOnlyList<IntegrationProvider>> ListProvidersAsync(CancellationToken cancellationToken) =>
        await db.IntegrationProviders.AsNoTracking().OrderBy(item => item.DisplayName).ToListAsync(cancellationToken);

    public Task<IntegrationProvider?> FindProviderByCodeAsync(string code, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.IntegrationProviders.AsQueryable() : db.IntegrationProviders.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Code == code, cancellationToken);
    }

    public Task<IntegrationProvider?> FindProviderAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.IntegrationProviders.AsQueryable() : db.IntegrationProviders.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public void AddProvider(IntegrationProvider provider) => db.IntegrationProviders.Add(provider);

    public Task<IntegrationConsent?> FindConsentAsync(Guid userId, IntegrationProviderKind kind, string purpose, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.IntegrationConsents.AsQueryable() : db.IntegrationConsents.AsNoTracking();
        return query.SingleOrDefaultAsync(
            item => item.UserId == userId && item.ProviderKind == kind && item.Purpose == purpose,
            cancellationToken);
    }

    public async Task<IReadOnlyList<IntegrationConsent>> ListConsentsAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.IntegrationConsents.AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

    public void AddConsent(IntegrationConsent consent) => db.IntegrationConsents.Add(consent);

    public Task<IntegrationDeliveryLog?> FindDeliveryByIdempotencyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        db.IntegrationDeliveryLogs.AsNoTracking().SingleOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<IReadOnlyList<IntegrationDeliveryLog>> ListRecentDeliveriesAsync(Guid? userReferenceHint, int take, CancellationToken cancellationToken) =>
        await db.IntegrationDeliveryLogs.AsNoTracking()
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

    public void AddDelivery(IntegrationDeliveryLog log) => db.IntegrationDeliveryLogs.Add(log);

    public Task<IntegrationWebhookEvent?> FindWebhookAsync(string providerCode, string eventId, CancellationToken cancellationToken) =>
        db.IntegrationWebhookEvents.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ProviderCode == providerCode && item.EventId == eventId, cancellationToken);

    public void AddWebhook(IntegrationWebhookEvent webhook) => db.IntegrationWebhookEvents.Add(webhook);

    public Task<LabOrder?> FindLabOrderByExternalAsync(string providerCode, string externalOrderId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.LabOrders.AsQueryable() : db.LabOrders.AsNoTracking();
        return query.SingleOrDefaultAsync(
            item => item.ProviderCode == providerCode && item.ExternalOrderId == externalOrderId,
            cancellationToken);
    }

    public Task<LabOrder?> FindLabOrderAsync(Guid orderId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.LabOrders.AsQueryable() : db.LabOrders.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == orderId, cancellationToken);
    }

    public async Task<IReadOnlyList<LabOrder>> ListLabOrdersAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.LabOrders.AsNoTracking()
            .Where(item => item.PatientUserId == userId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public void AddLabOrder(LabOrder order) => db.LabOrders.Add(order);

    public async Task<IReadOnlyList<LabReport>> ListLabReportsAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.LabReports.AsNoTracking()
            .Where(item => item.PatientUserId == userId)
            .OrderByDescending(item => item.ReportedAtUtc)
            .ToListAsync(cancellationToken);

    public Task<LabReport?> FindLabReportAsync(Guid reportId, CancellationToken cancellationToken) =>
        db.LabReports.AsNoTracking().SingleOrDefaultAsync(item => item.Id == reportId, cancellationToken);

    public Task<bool> LabReportExistsForOrderAsync(Guid labOrderId, CancellationToken cancellationToken) =>
        db.LabReports.AsNoTracking().AnyAsync(item => item.LabOrderId == labOrderId, cancellationToken);

    public void AddLabReport(LabReport report) => db.LabReports.Add(report);

    public Task<PharmacyShare?> FindPharmacyShareByExternalAsync(string providerCode, string externalShareId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.PharmacyShares.AsQueryable() : db.PharmacyShares.AsNoTracking();
        return query.SingleOrDefaultAsync(
            item => item.ProviderCode == providerCode && item.ExternalShareId == externalShareId,
            cancellationToken);
    }

    public Task<PharmacyShare?> FindPharmacyShareAsync(Guid shareId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.PharmacyShares.AsQueryable() : db.PharmacyShares.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == shareId, cancellationToken);
    }

    public async Task<IReadOnlyList<PharmacyShare>> ListPharmacySharesAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.PharmacyShares.AsNoTracking()
            .Where(item => item.PatientUserId == userId)
            .OrderByDescending(item => item.SharedAtUtc)
            .ToListAsync(cancellationToken);

    public void AddPharmacyShare(PharmacyShare share) => db.PharmacyShares.Add(share);

    public Task<AbhaLink?> FindAbhaLinkAsync(Guid userId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.AbhaLinks.AsQueryable() : db.AbhaLinks.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
    }

    public void AddAbhaLink(AbhaLink link) => db.AbhaLinks.Add(link);

    public async Task<(string? FullName, string? Phone)> FindUserProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var row = await db.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new
            {
                user.PhoneNumber,
                FullName = db.Patients.Where(patient => patient.UserId == userId).Select(patient => patient.FullName).FirstOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken);
        return (row?.FullName, row?.PhoneNumber);
    }

    public Task<PatientMedicalRecord?> FindPrimaryMedicalRecordAsync(Guid userId, CancellationToken cancellationToken) =>
        db.PatientMedicalRecords.AsNoTracking()
            .Include(item => item.Conditions)
            .Include(item => item.Medications)
            .SingleOrDefaultAsync(item => item.AccountUserId == userId && item.FamilyMemberId == null, cancellationToken);

    public async Task<IReadOnlyList<Appointment>> ListRecentAppointmentsAsync(Guid userId, int take, CancellationToken cancellationToken) =>
        await db.Appointments.AsNoTracking()
            .Where(item => item.AccountUserId == userId)
            .OrderByDescending(item => item.StartsAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<Appointment?> FindAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken) =>
        db.Appointments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == appointmentId, cancellationToken);

    public Task<Prescription?> FindPrescriptionAsync(Guid prescriptionId, CancellationToken cancellationToken) =>
        db.Prescriptions.AsNoTracking()
            .Include(item => item.Versions)
            .ThenInclude(version => version.Items)
            .SingleOrDefaultAsync(item => item.Id == prescriptionId, cancellationToken);

    public Task<Doctor?> FindDoctorAsync(Guid doctorId, CancellationToken cancellationToken) =>
        db.Doctors.AsNoTracking().SingleOrDefaultAsync(item => item.Id == doctorId, cancellationToken);

    public Task<Clinic?> FindClinicAsync(Guid clinicId, CancellationToken cancellationToken) =>
        db.Clinics.AsNoTracking().SingleOrDefaultAsync(item => item.Id == clinicId, cancellationToken);

    public Task NotifyAsync(UserNotification notification, CancellationToken cancellationToken)
    {
        db.UserNotifications.Add(notification);
        return Task.CompletedTask;
    }

    public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(audit);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
