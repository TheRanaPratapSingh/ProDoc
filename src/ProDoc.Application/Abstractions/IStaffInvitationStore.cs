using ProDoc.Application.Admin;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Providers;

namespace ProDoc.Application.Abstractions;

public interface IStaffInvitationStore
{
    Task<StaffInvitation?> FindPendingByPhoneAsync(string phoneE164, DateTimeOffset now, CancellationToken cancellationToken);

    Task<StaffInvitation?> FindByIdAsync(Guid invitationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StaffInvitationDetails>> ListAsync(string? status, int take, CancellationToken cancellationToken);

    Task AddAsync(StaffInvitation invitation, CancellationToken cancellationToken);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken);

    Task GrantRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken);

    Task EnsurePatientProfileAsync(Guid userId, string? fullName, DateTimeOffset now, CancellationToken cancellationToken);

    Task AssignClinicStaffAsync(Guid clinicId, Guid userId, Guid? branchId, string roleName, DateTimeOffset now, CancellationToken cancellationToken);

    Task EnsureDoctorDraftAsync(Guid userId, string? fullName, DateTimeOffset now, CancellationToken cancellationToken);

    Task ApplyDoctorInviteProfileAsync(
        Guid userId,
        string? fullName,
        Guid? specializationId,
        int? yearsOfExperience,
        string? qualifications,
        string? registrationNumber,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task AffiliateDoctorToClinicsAsync(
        Guid userId,
        IReadOnlyList<Guid> clinicIds,
        Guid? branchId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marks an invite-created doctor Approved and ensures a published clinic schedule so patients can discover them.
    /// Returns the doctor id when activation succeeds.
    /// </summary>
    Task<Guid> ActivateDoctorForPublicDiscoveryAsync(
        Guid userId,
        IReadOnlyList<Guid> clinicIds,
        Guid? branchId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<(Guid DoctorId, string Status, bool IsPubliclyDiscoverable)?> GetDoctorDiscoveryStateAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task AssignReceptionistDoctorsAsync(
        Guid clinicId,
        Guid receptionistUserId,
        IReadOnlyList<Guid> doctorIds,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task TransferClinicOwnershipAsync(Guid clinicId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<bool> ClinicExistsAsync(Guid clinicId, CancellationToken cancellationToken);

    Task<bool> DoctorExistsAsync(Guid doctorId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
