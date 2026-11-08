using ProDoc.Application.Admin;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Providers;

namespace ProDoc.Application.Abstractions;

public interface IAdminClinicStore
{
    Task<Clinic?> FindClinicAsync(Guid clinicId, bool tracking, CancellationToken cancellationToken);

    Task AddClinicAsync(Clinic clinic, CancellationToken cancellationToken);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminClinicSummary>> ListClinicsAsync(string? query, int take, CancellationToken cancellationToken);

    Task<AdminClinicDetails?> GetClinicDetailsAsync(Guid clinicId, CancellationToken cancellationToken);

    Task AffiliateDoctorAsync(Guid clinicId, Guid doctorId, Guid? branchId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<DoctorClinicAffiliation?> FindAffiliationAsync(Guid affiliationId, bool tracking, CancellationToken cancellationToken);

    /// <summary>Approve an admin-managed doctor and publish a default clinic schedule for patient discovery.</summary>
    Task ActivateDoctorDiscoveryAsync(Guid doctorId, DateTimeOffset now, CancellationToken cancellationToken);

    Task RemoveDoctorAffiliationAsync(Guid clinicId, Guid doctorId, CancellationToken cancellationToken);

    Task AssignStaffAsync(Guid clinicId, Guid userId, Guid? branchId, string roleName, DateTimeOffset now, CancellationToken cancellationToken);

    Task DeactivateStaffAsync(Guid clinicId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    Task AssignReceptionistDoctorsAsync(
        Guid clinicId,
        Guid receptionistUserId,
        IReadOnlyList<Guid> doctorIds,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<bool> DoctorExistsAsync(Guid doctorId, CancellationToken cancellationToken);

    Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
