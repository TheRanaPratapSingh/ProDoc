using ProDoc.Domain.Governance;
using ProDoc.Domain.Providers;

namespace ProDoc.Application.Providers;

public interface IProviderStore
{
    Task<IReadOnlyList<Specialization>> ListSpecializationsAsync(CancellationToken cancellationToken);

    Task<bool> SpecializationsExistAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<Doctor?> FindDoctorByUserIdAsync(Guid userId, bool tracking, CancellationToken cancellationToken);

    Task<Doctor?> FindDoctorAsync(Guid doctorId, bool tracking, CancellationToken cancellationToken);

    Task AddDoctorAsync(Doctor doctor, CancellationToken cancellationToken);

    Task<Clinic?> FindClinicAsync(Guid clinicId, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<Clinic>> ListClinicsByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken);

    Task AddClinicAsync(Clinic clinic, CancellationToken cancellationToken);

    Task<IReadOnlyList<DoctorClinicAffiliation>> ListAffiliationsAsync(Guid doctorId, CancellationToken cancellationToken);

    Task<DoctorClinicAffiliation?> FindAffiliationAsync(Guid doctorId, Guid clinicId, CancellationToken cancellationToken);

    Task AddAffiliationAsync(DoctorClinicAffiliation affiliation, CancellationToken cancellationToken);

    void RemoveAffiliation(DoctorClinicAffiliation affiliation);

    Task<IReadOnlyList<AffiliationModel>> ListAffiliationDetailsAsync(
        Guid doctorId,
        bool approvedClinicsOnly,
        DateOnly today,
        CancellationToken cancellationToken);

    Task<ProviderDocument?> FindDocumentAsync(Guid documentId, CancellationToken cancellationToken);

    Task<string?> FindUserPhoneAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DoctorCard>> SearchDoctorsAsync(
        string? query,
        Guid? specializationId,
        DateOnly today,
        CancellationToken cancellationToken);

    Task<PublicDoctor?> FindPublicDoctorAsync(Guid doctorId, DateOnly today, CancellationToken cancellationToken);

    Task<IReadOnlyList<ClinicCard>> SearchClinicsAsync(string? query, DateOnly today, CancellationToken cancellationToken);

    Task<PublicClinic?> FindPublicClinicAsync(Guid clinicId, DateOnly today, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReviewQueueItem>> ListReviewsAsync(
        string? kind,
        VerificationStatus? status,
        CancellationToken cancellationToken);

    Task GrantRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken);

    Task AddAuditAsync(AuditLog log, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
