using ProDoc.Domain.Enterprise;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Providers;
using ProDoc.Domain.Queue;

namespace ProDoc.Application.Enterprise;

public interface IEnterpriseStore
{
    Task<Organization?> FindOrganizationAsync(Guid organizationId, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<Organization>> ListOrganizationsForUserAsync(Guid userId, CancellationToken cancellationToken);

    void AddOrganization(Organization organization);

    Task<OrganizationMembership?> FindMembershipAsync(Guid organizationId, Guid userId, bool tracking, CancellationToken cancellationToken);

    Task<OrganizationMembership?> FindMembershipByIdAsync(Guid membershipId, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrganizationMembership>> ListMembershipsAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<int> CountActiveMembershipsAsync(Guid organizationId, CancellationToken cancellationToken);

    void AddMembership(OrganizationMembership membership);

    Task<Clinic?> FindClinicAsync(Guid clinicId, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<Clinic>> ListClinicsForOrganizationAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Clinic>> ListOwnedClinicsAsync(Guid ownerUserId, CancellationToken cancellationToken);

    Task<OrganizationSetting?> FindSettingAsync(Guid organizationId, string key, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrganizationSetting>> ListSettingsAsync(Guid organizationId, CancellationToken cancellationToken);

    void AddSetting(OrganizationSetting setting);

    Task<BranchSettingOverride?> FindBranchOverrideAsync(Guid branchId, string key, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<BranchSettingOverride>> ListBranchOverridesAsync(Guid organizationId, Guid? branchId, CancellationToken cancellationToken);

    void AddBranchOverride(BranchSettingOverride setting);

    Task<ConsultationResource?> FindResourceAsync(Guid resourceId, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConsultationResource>> ListResourcesAsync(Guid organizationId, Guid? branchId, CancellationToken cancellationToken);

    void AddResource(ConsultationResource resource);

    Task<bool> HasResourceConflictAsync(Guid resourceId, DateTimeOffset startsAtUtc, DateTimeOffset endsAtUtc, Guid? excludeAllocationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ResourceAllocation>> ListAllocationsAsync(Guid organizationId, Guid? resourceId, CancellationToken cancellationToken);

    void AddAllocation(ResourceAllocation allocation);

    Task<OrganizationSubscription?> FindSubscriptionAsync(Guid organizationId, bool tracking, CancellationToken cancellationToken);

    void AddSubscription(OrganizationSubscription subscription);

    Task<Guid?> FindUserIdByPhoneAsync(string phoneE164, CancellationToken cancellationToken);

    Task<string?> FindUserPhoneAsync(Guid userId, CancellationToken cancellationToken);

    Task GrantOrganizationAdminRoleAsync(Guid userId, CancellationToken cancellationToken);

    Task<OrganizationAnalytics> BuildAnalyticsAsync(
        Guid organizationId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        Guid? clinicId,
        Guid? branchId,
        Guid? doctorId,
        CancellationToken cancellationToken);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
