using Microsoft.Extensions.Logging;
using ProDoc.Application.Abstractions;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Providers;

namespace ProDoc.Application.Providers;

public sealed class VerificationReviewService(IProviderStore store, IClock clock, ILogger<VerificationReviewService> logger)
{
    public Task<IReadOnlyList<ReviewQueueItem>> ListAsync(
        string? kind,
        VerificationStatus? status,
        CancellationToken cancellationToken) =>
        store.ListReviewsAsync(kind, status, cancellationToken);

    public async Task<DoctorProfile> ReviewDoctorAsync(
        Guid reviewerUserId,
        Guid doctorId,
        ReviewDecision decision,
        string? reason,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var doctor = await store.FindDoctorAsync(doctorId, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("doctor_not_found", "That doctor application was not found.");
        var now = clock.UtcNow;
        doctor.RefreshExpiry(DateOnly.FromDateTime(now.UtcDateTime), now);
        doctor.Review(decision, reason, now);
        if (decision == ReviewDecision.Approve)
        {
            await store.GrantRoleAsync(doctor.UserId, RoleNames.Doctor, cancellationToken);
        }

        await store.AddAuditAsync(
            AuditLog.Record(reviewerUserId, AuditActions.DoctorReviewed, now, metadata.IpAddress, metadata.UserAgent),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Doctor application {Reference} reviewed with {Decision}", doctor.Reference, decision);
        var specializations = await store.ListSpecializationsAsync(cancellationToken);
        var affiliations = await store.ListAffiliationDetailsAsync(doctor.Id, false, DateOnly.FromDateTime(now.UtcDateTime), cancellationToken);
        var phone = await store.FindUserPhoneAsync(doctor.UserId, cancellationToken);
        return ProviderMapper.ToDoctor(doctor, phone, specializations.ToDictionary(item => item.Id), affiliations, DateOnly.FromDateTime(now.UtcDateTime));
    }

    public async Task<ClinicProfile> ReviewClinicAsync(
        Guid reviewerUserId,
        Guid clinicId,
        ReviewDecision decision,
        string? reason,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var clinic = await store.FindClinicAsync(clinicId, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("clinic_not_found", "That clinic application was not found.");
        var now = clock.UtcNow;
        clinic.RefreshExpiry(DateOnly.FromDateTime(now.UtcDateTime), now);
        clinic.Review(decision, reason, now);
        if (decision == ReviewDecision.Approve)
        {
            await store.GrantRoleAsync(clinic.OwnerUserId, RoleNames.ClinicAdmin, cancellationToken);
        }

        await store.AddAuditAsync(
            AuditLog.Record(reviewerUserId, AuditActions.ClinicReviewed, now, metadata.IpAddress, metadata.UserAgent),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Clinic application {Reference} reviewed with {Decision}", clinic.Reference, decision);
        return ProviderMapper.ToClinic(clinic, DateOnly.FromDateTime(now.UtcDateTime));
    }
}
