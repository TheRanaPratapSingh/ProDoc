using ProDoc.Application.Enterprise;
using ProDoc.Application.Providers;
using ProDoc.Domain.Common;

namespace ProDoc.Application.Queue;

public sealed class ClinicAccessService(IQueueStore queue, IProviderStore providers, EnterpriseService enterprise) : IClinicAccess
{
    public async Task EnsureClinicOwnerAsync(Guid userId, Guid clinicId, CancellationToken cancellationToken)
    {
        if (await IsClinicOwnerAsync(userId, clinicId, cancellationToken)
            || await enterprise.UserHasOrgClinicAccessAsync(userId, clinicId, cancellationToken))
        {
            return;
        }

        throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
    }

    public async Task EnsureQueueAccessAsync(Guid userId, Guid clinicId, Guid branchId, CancellationToken cancellationToken)
    {
        if (await IsClinicOwnerAsync(userId, clinicId, cancellationToken)
            || await enterprise.UserHasOrgClinicAccessAsync(userId, clinicId, cancellationToken)
            || await HasQueueStaffAccessAsync(userId, clinicId, branchId, cancellationToken))
        {
            return;
        }

        throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
    }

    public async Task EnsureDoctorQueueAsync(Guid userId, Guid doctorId, Guid clinicId, Guid branchId, CancellationToken cancellationToken)
    {
        var doctor = await providers.FindDoctorAsync(doctorId, tracking: false, cancellationToken);
        if (doctor?.UserId == userId)
        {
            var affiliation = await providers.FindAffiliationAsync(doctor.Id, clinicId, cancellationToken);
            if (affiliation is null)
            {
                throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
            }

            if (affiliation.BranchId is Guid affiliatedBranch && affiliatedBranch != branchId)
            {
                throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
            }

            return;
        }

        await EnsureDoctorCoverageAsync(userId, clinicId, branchId, doctorId, cancellationToken);
    }

    public async Task EnsureDoctorCoverageAsync(
        Guid userId,
        Guid clinicId,
        Guid branchId,
        Guid doctorId,
        CancellationToken cancellationToken)
    {
        if (await IsClinicOwnerAsync(userId, clinicId, cancellationToken)
            || await enterprise.UserHasOrgClinicAccessAsync(userId, clinicId, cancellationToken))
        {
            return;
        }

        await EnsureQueueAccessAsync(userId, clinicId, branchId, cancellationToken);
        var assigned = await ListAssignedDoctorIdsAsync(userId, clinicId, cancellationToken);
        if (assigned.Count > 0 && !assigned.Contains(doctorId))
        {
            throw DomainException.NotFound("doctor_not_found", "That doctor was not found.");
        }
    }

    public async Task EnsureClinicStaffAccessAsync(Guid userId, Guid clinicId, CancellationToken cancellationToken)
    {
        if (await IsClinicOwnerAsync(userId, clinicId, cancellationToken)
            || await enterprise.UserHasOrgClinicAccessAsync(userId, clinicId, cancellationToken)
            || await HasClinicStaffAccessAsync(userId, clinicId, cancellationToken))
        {
            return;
        }

        throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
    }

    public async Task<bool> HasClinicStaffAccessAsync(Guid userId, Guid clinicId, CancellationToken cancellationToken)
    {
        var staff = await queue.FindStaffAsync(clinicId, userId, cancellationToken);
        return staff is { IsActive: true };
    }

    public Task<IReadOnlyList<Guid>> ListAssignedDoctorIdsAsync(Guid userId, Guid clinicId, CancellationToken cancellationToken) =>
        queue.ListReceptionistAssignedDoctorIdsAsync(userId, clinicId, cancellationToken);

    public async Task<bool> IsClinicOwnerAsync(Guid userId, Guid clinicId, CancellationToken cancellationToken)
    {
        var clinic = await providers.FindClinicAsync(clinicId, tracking: false, cancellationToken);
        return clinic?.OwnerUserId == userId;
    }

    public async Task<bool> HasQueueStaffAccessAsync(Guid userId, Guid clinicId, Guid branchId, CancellationToken cancellationToken)
    {
        var staff = await queue.FindStaffAsync(clinicId, userId, cancellationToken);
        return staff is { IsActive: true } && staff.CoversBranch(branchId);
    }
}
