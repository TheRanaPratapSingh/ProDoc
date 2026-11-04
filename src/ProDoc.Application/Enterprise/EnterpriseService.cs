using ProDoc.Application.Abstractions;
using ProDoc.Application.Providers;
using ProDoc.Application.SaaS;
using ProDoc.Domain.Common;
using ProDoc.Domain.Enterprise;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using PhoneNumber = ProDoc.Domain.Identity.PhoneNumber;

namespace ProDoc.Application.Enterprise;

public sealed class EnterpriseService(IEnterpriseStore store, ITenantEntitlementGuard entitlements, ISaaSService saas, IClock clock)
{
    public async Task<IReadOnlyList<OrganizationDetails>> ListMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var orgs = await store.ListOrganizationsForUserAsync(userId, cancellationToken);
        var result = new List<OrganizationDetails>();
        foreach (var org in orgs)
        {
            result.Add(await MapOrganizationAsync(org, cancellationToken));
        }

        return result;
    }

    public async Task<OrganizationDetails> CreateAsync(
        Guid userId,
        string name,
        string kind,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<OrganizationKind>(kind, true, out var parsed))
        {
            throw DomainException.Validation("organization_kind_invalid", "Choose HealthcareGroup, Hospital, or ClinicChain.");
        }

        var org = Organization.Create(name, parsed, userId, clock.UtcNow);
        store.AddOrganization(org);
        store.AddMembership(OrganizationMembership.Assign(org.Id, userId, OrganizationMemberRole.OrganizationOwner, null, null, clock.UtcNow));
        store.AddSubscription(OrganizationSubscription.Create(org.Id, "STARTER", 10, null, clock.UtcNow, trialDays: 14));
        await store.GrantOrganizationAdminRoleAsync(userId, cancellationToken);
        await store.AddAuditAsync(Audit(userId, AuditActions.OrganizationCreated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        await saas.EnsureTenantBootstrapAsync(org.Id, org.Name, cancellationToken);
        return await MapOrganizationAsync(org, cancellationToken);
    }

    public async Task<OrganizationDetails> UpdateAsync(
        Guid userId,
        Guid organizationId,
        string name,
        bool? isActive,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(userId, organizationId, cancellationToken);
        var org = await store.FindOrganizationAsync(organizationId, true, cancellationToken)
            ?? throw DomainException.NotFound("organization_not_found", "That organization was not found.");
        org.Rename(name, clock.UtcNow);
        if (isActive is bool active)
        {
            org.SetActive(active, clock.UtcNow);
        }

        await store.AddAuditAsync(Audit(userId, AuditActions.OrganizationUpdated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return await MapOrganizationAsync(org, cancellationToken);
    }

    public async Task<IReadOnlyList<OrganizationClinicDetails>> ListClinicsAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        await EnsureOrgAccessAsync(userId, organizationId, cancellationToken);
        var clinics = await store.ListClinicsForOrganizationAsync(organizationId, cancellationToken);
        return clinics.Select(clinic => new OrganizationClinicDetails(
            clinic.Id,
            clinic.Name,
            clinic.Status.ToString(),
            clinic.OwnerUserId,
            clinic.Branches.Count(branch => branch.IsActive))).ToArray();
    }

    public async Task<OrganizationClinicDetails> LinkClinicAsync(
        Guid userId,
        Guid organizationId,
        Guid clinicId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(userId, organizationId, cancellationToken);
        await entitlements.EnsureCanAddClinicAsync(organizationId, cancellationToken);
        var clinic = await store.FindClinicAsync(clinicId, true, cancellationToken)
            ?? throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
        if (clinic.OwnerUserId != userId)
        {
            var membership = await store.FindMembershipAsync(organizationId, userId, false, cancellationToken);
            if (membership is null || !OrganizationRoles.CanManageOrganization(membership.Role) || !membership.IsActive)
            {
                throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
            }

            // Non-owners may only link clinics already owned by an org member who is the clinic owner — require ownership.
            throw DomainException.Validation("clinic_link_owner_required", "Only the clinic owner can attach it to an organization.");
        }

        clinic.AttachToOrganization(organizationId, clock.UtcNow);
        await store.AddAuditAsync(Audit(userId, AuditActions.OrganizationClinicLinked, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return new OrganizationClinicDetails(clinic.Id, clinic.Name, clinic.Status.ToString(), clinic.OwnerUserId, clinic.Branches.Count(branch => branch.IsActive));
    }

    public async Task<IReadOnlyList<OrganizationMembershipDetails>> ListMembersAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        await EnsureCanManageStaffAsync(userId, organizationId, cancellationToken);
        var rows = await store.ListMembershipsAsync(organizationId, cancellationToken);
        var result = new List<OrganizationMembershipDetails>();
        foreach (var row in rows)
        {
            result.Add(new OrganizationMembershipDetails(
                row.Id,
                row.OrganizationId,
                row.UserId,
                await store.FindUserPhoneAsync(row.UserId, cancellationToken),
                row.Role.ToString(),
                row.ClinicId,
                row.BranchId,
                row.IsActive,
                row.UpdatedAtUtc));
        }

        return result;
    }

    public async Task<OrganizationMembershipDetails> InviteMemberAsync(
        Guid userId,
        Guid organizationId,
        string phoneNumber,
        string role,
        Guid? clinicId,
        Guid? branchId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await EnsureCanManageStaffAsync(userId, organizationId, cancellationToken);
        await entitlements.EnsureCanAddStaffAsync(organizationId, cancellationToken);
        if (!Enum.TryParse<OrganizationMemberRole>(role, true, out var parsed))
        {
            throw DomainException.Validation("org_role_invalid", "Choose a valid organization role.");
        }

        if (parsed == OrganizationMemberRole.OrganizationOwner)
        {
            throw DomainException.Validation("org_role_invalid", "Organization owner cannot be assigned via invite.");
        }

        await EnsureClinicInOrganizationAsync(organizationId, clinicId, branchId, cancellationToken);
        var phone = PhoneNumber.ParseIndianMobile(phoneNumber);
        var targetUserId = await store.FindUserIdByPhoneAsync(phone.E164, cancellationToken)
            ?? throw DomainException.NotFound("account_not_found", "Ask that person to sign in once before inviting them.");

        var subscription = await store.FindSubscriptionAsync(organizationId, false, cancellationToken);
        if (subscription is not null)
        {
            var seats = await store.CountActiveMembershipsAsync(organizationId, cancellationToken);
            if (seats >= subscription.SeatLimit)
            {
                throw DomainException.Conflict("org_seat_limit", "This organization has reached its subscription seat limit.");
            }
        }

        var existing = await store.FindMembershipAsync(organizationId, targetUserId, true, cancellationToken);
        OrganizationMembership membership;
        if (existing is null)
        {
            membership = OrganizationMembership.Assign(organizationId, targetUserId, parsed, clinicId, branchId, clock.UtcNow);
            store.AddMembership(membership);
        }
        else
        {
            existing.UpdateRole(parsed, clinicId, branchId, clock.UtcNow);
            membership = existing;
        }

        if (OrganizationRoles.CanManageStaff(parsed) || OrganizationRoles.CanViewAnalytics(parsed) || OrganizationRoles.CanManageBilling(parsed))
        {
            await store.GrantOrganizationAdminRoleAsync(targetUserId, cancellationToken);
        }

        await store.AddAuditAsync(Audit(userId, AuditActions.OrganizationMemberAssigned, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return new OrganizationMembershipDetails(
            membership.Id,
            membership.OrganizationId,
            membership.UserId,
            phone.E164,
            membership.Role.ToString(),
            membership.ClinicId,
            membership.BranchId,
            membership.IsActive,
            membership.UpdatedAtUtc);
    }

    public async Task<OrganizationMembershipDetails> TransferMemberAsync(
        Guid userId,
        Guid organizationId,
        Guid membershipId,
        Guid clinicId,
        Guid? branchId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await EnsureCanManageStaffAsync(userId, organizationId, cancellationToken);
        await EnsureClinicInOrganizationAsync(organizationId, clinicId, branchId, cancellationToken);
        var membership = await store.FindMembershipByIdAsync(membershipId, true, cancellationToken)
            ?? throw DomainException.NotFound("org_member_not_found", "That membership was not found.");
        if (membership.OrganizationId != organizationId)
        {
            throw DomainException.NotFound("org_member_not_found", "That membership was not found.");
        }

        membership.TransferBranch(clinicId, branchId, clock.UtcNow);
        await store.AddAuditAsync(Audit(userId, AuditActions.OrganizationMemberUpdated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return new OrganizationMembershipDetails(
            membership.Id,
            membership.OrganizationId,
            membership.UserId,
            await store.FindUserPhoneAsync(membership.UserId, cancellationToken),
            membership.Role.ToString(),
            membership.ClinicId,
            membership.BranchId,
            membership.IsActive,
            membership.UpdatedAtUtc);
    }

    public async Task SuspendMemberAsync(
        Guid userId,
        Guid organizationId,
        Guid membershipId,
        bool activate,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await EnsureCanManageStaffAsync(userId, organizationId, cancellationToken);
        var membership = await store.FindMembershipByIdAsync(membershipId, true, cancellationToken)
            ?? throw DomainException.NotFound("org_member_not_found", "That membership was not found.");
        if (membership.OrganizationId != organizationId)
        {
            throw DomainException.NotFound("org_member_not_found", "That membership was not found.");
        }

        if (membership.Role == OrganizationMemberRole.OrganizationOwner && !activate)
        {
            throw DomainException.Validation("org_owner_suspend_forbidden", "The organization owner cannot be suspended.");
        }

        if (activate)
        {
            membership.Activate(clock.UtcNow);
        }
        else
        {
            membership.Suspend(clock.UtcNow);
        }

        await store.AddAuditAsync(Audit(userId, AuditActions.OrganizationMemberUpdated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrganizationSettingDetails>> ListEffectiveSettingsAsync(
        Guid userId,
        Guid organizationId,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        await EnsureOrgAccessAsync(userId, organizationId, cancellationToken);
        var orgSettings = await store.ListSettingsAsync(organizationId, cancellationToken);
        var overrides = branchId is Guid bid
            ? await store.ListBranchOverridesAsync(organizationId, bid, cancellationToken)
            : [];
        var map = orgSettings.ToDictionary(item => item.Key, item => new OrganizationSettingDetails(item.Key, item.Value, "Organization", null), StringComparer.OrdinalIgnoreCase);
        foreach (var item in overrides)
        {
            map[item.Key] = new OrganizationSettingDetails(item.Key, item.Value, "Branch", item.BranchId);
        }

        return map.Values.OrderBy(item => item.Key).ToArray();
    }

    public async Task<OrganizationSettingDetails> UpsertOrganizationSettingAsync(
        Guid userId,
        Guid organizationId,
        string key,
        string value,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(userId, organizationId, cancellationToken);
        var existing = await store.FindSettingAsync(organizationId, key.Trim(), true, cancellationToken);
        if (existing is null)
        {
            store.AddSetting(OrganizationSetting.Upsert(organizationId, key, value, clock.UtcNow));
        }
        else
        {
            existing.SetValue(value, clock.UtcNow);
        }

        await store.AddAuditAsync(Audit(userId, AuditActions.OrganizationSettingChanged, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return new OrganizationSettingDetails(key.Trim(), value.Trim(), "Organization", null);
    }

    public async Task<OrganizationSettingDetails> UpsertBranchOverrideAsync(
        Guid userId,
        Guid organizationId,
        Guid branchId,
        string key,
        string value,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var membership = await EnsureCanManageAsync(userId, organizationId, cancellationToken);
        if (!OrganizationRoles.CanManageBranch(membership.Role, membership.BranchId, branchId))
        {
            throw DomainException.NotFound("branch_not_found", "That branch was not found.");
        }

        await EnsureClinicInOrganizationAsync(organizationId, null, branchId, cancellationToken);
        var existing = await store.FindBranchOverrideAsync(branchId, key.Trim(), true, cancellationToken);
        if (existing is null)
        {
            store.AddBranchOverride(BranchSettingOverride.Upsert(organizationId, branchId, key, value, clock.UtcNow));
        }
        else
        {
            existing.SetValue(value, clock.UtcNow);
        }

        await store.AddAuditAsync(Audit(userId, AuditActions.OrganizationSettingChanged, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return new OrganizationSettingDetails(key.Trim(), value.Trim(), "Branch", branchId);
    }

    public async Task<ConsultationResourceDetails> CreateResourceAsync(
        Guid userId,
        Guid organizationId,
        Guid clinicId,
        Guid branchId,
        string name,
        string kind,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(userId, organizationId, cancellationToken);
        await EnsureClinicInOrganizationAsync(organizationId, clinicId, branchId, cancellationToken);
        if (!Enum.TryParse<ResourceKind>(kind, true, out var parsed))
        {
            throw DomainException.Validation("resource_kind_invalid", "Choose ConsultationRoom or Equipment.");
        }

        var resource = ConsultationResource.Create(organizationId, clinicId, branchId, name, parsed, clock.UtcNow);
        store.AddResource(resource);
        await store.AddAuditAsync(Audit(userId, AuditActions.ResourceCreated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return new ConsultationResourceDetails(resource.Id, resource.ClinicId, resource.BranchId, resource.Name, resource.Kind.ToString(), resource.IsActive);
    }

    public Task<IReadOnlyList<ConsultationResourceDetails>> ListResourcesAsync(Guid userId, Guid organizationId, Guid? branchId, CancellationToken cancellationToken) =>
        ListResourcesInternalAsync(userId, organizationId, branchId, cancellationToken);

    public async Task<ResourceAllocationDetails> AllocateResourceAsync(
        Guid userId,
        Guid organizationId,
        Guid resourceId,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        Guid? doctorId,
        Guid? scheduleId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(userId, organizationId, cancellationToken);
        var resource = await store.FindResourceAsync(resourceId, false, cancellationToken)
            ?? throw DomainException.NotFound("resource_not_found", "That resource was not found.");
        if (resource.OrganizationId != organizationId || !resource.IsActive)
        {
            throw DomainException.NotFound("resource_not_found", "That resource was not found.");
        }

        if (await store.HasResourceConflictAsync(resourceId, startsAtUtc, endsAtUtc, null, cancellationToken))
        {
            throw DomainException.Conflict("resource_conflict", "That resource is already allocated for the selected time.");
        }

        var allocation = ResourceAllocation.Create(resourceId, organizationId, startsAtUtc, endsAtUtc, clock.UtcNow, doctorId, scheduleId);
        store.AddAllocation(allocation);
        await store.AddAuditAsync(Audit(userId, AuditActions.ResourceAllocated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return new ResourceAllocationDetails(allocation.Id, allocation.ResourceId, allocation.DoctorId, allocation.ScheduleId, allocation.StartsAtUtc, allocation.EndsAtUtc);
    }

    public async Task<OrganizationSubscriptionDetails> GetSubscriptionAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        await EnsureBillingAccessAsync(userId, organizationId, cancellationToken);
        var subscription = await store.FindSubscriptionAsync(organizationId, false, cancellationToken)
            ?? throw DomainException.NotFound("subscription_not_found", "No subscription configuration exists for this organization.");
        return MapSubscription(subscription);
    }

    public async Task<OrganizationSubscriptionDetails> UpdateSubscriptionAsync(
        Guid userId,
        Guid organizationId,
        string planCode,
        string status,
        int seatLimit,
        string? billingEmail,
        DateTimeOffset? periodEndUtc,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await EnsureBillingAccessAsync(userId, organizationId, cancellationToken);
        if (!Enum.TryParse<OrganizationSubscriptionStatus>(status, true, out var parsed))
        {
            throw DomainException.Validation("subscription_status_invalid", "Choose a valid subscription status.");
        }

        var subscription = await store.FindSubscriptionAsync(organizationId, true, cancellationToken);
        if (subscription is null)
        {
            subscription = OrganizationSubscription.Create(organizationId, planCode, seatLimit, billingEmail, clock.UtcNow);
            store.AddSubscription(subscription);
        }

        subscription.Update(planCode, parsed, seatLimit, billingEmail, periodEndUtc, clock.UtcNow);
        await store.AddAuditAsync(Audit(userId, AuditActions.OrganizationSubscriptionUpdated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapSubscription(subscription);
    }

    public async Task<OrganizationAnalytics> GetAnalyticsAsync(
        Guid userId,
        Guid organizationId,
        DateOnly from,
        DateOnly to,
        Guid? clinicId,
        Guid? branchId,
        Guid? doctorId,
        CancellationToken cancellationToken)
    {
        await EnsureAnalyticsAccessAsync(userId, organizationId, cancellationToken);
        if (to < from)
        {
            throw DomainException.Validation("analytics_range_invalid", "Choose a valid date range.");
        }

        var fromUtc = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toUtc = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
        return await store.BuildAnalyticsAsync(organizationId, fromUtc, toUtc, clinicId, branchId, doctorId, cancellationToken);
    }

    public async Task<bool> UserHasOrgClinicAccessAsync(Guid userId, Guid clinicId, CancellationToken cancellationToken)
    {
        var clinic = await store.FindClinicAsync(clinicId, false, cancellationToken);
        if (clinic?.OrganizationId is not Guid orgId)
        {
            return false;
        }

        var membership = await store.FindMembershipAsync(orgId, userId, false, cancellationToken);
        return membership is { IsActive: true }
            && OrganizationRoles.CanManageOrganization(membership.Role)
            && membership.CoversClinic(clinicId);
    }

    private async Task<IReadOnlyList<ConsultationResourceDetails>> ListResourcesInternalAsync(
        Guid userId,
        Guid organizationId,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        await EnsureOrgAccessAsync(userId, organizationId, cancellationToken);
        var rows = await store.ListResourcesAsync(organizationId, branchId, cancellationToken);
        return rows.Select(item => new ConsultationResourceDetails(item.Id, item.ClinicId, item.BranchId, item.Name, item.Kind.ToString(), item.IsActive)).ToArray();
    }

    private async Task EnsureOrgAccessAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        var membership = await store.FindMembershipAsync(organizationId, userId, false, cancellationToken);
        if (membership is not { IsActive: true })
        {
            throw DomainException.NotFound("organization_not_found", "That organization was not found.");
        }
    }

    private async Task<OrganizationMembership> EnsureCanManageAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        var membership = await store.FindMembershipAsync(organizationId, userId, false, cancellationToken);
        if (membership is not { IsActive: true } || !OrganizationRoles.CanManageOrganization(membership.Role))
        {
            throw DomainException.NotFound("organization_not_found", "That organization was not found.");
        }

        return membership;
    }

    private async Task EnsureCanManageStaffAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        var membership = await store.FindMembershipAsync(organizationId, userId, false, cancellationToken);
        if (membership is not { IsActive: true } || !OrganizationRoles.CanManageStaff(membership.Role))
        {
            throw DomainException.NotFound("organization_not_found", "That organization was not found.");
        }
    }

    private async Task EnsureAnalyticsAccessAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        var membership = await store.FindMembershipAsync(organizationId, userId, false, cancellationToken);
        if (membership is not { IsActive: true } || !OrganizationRoles.CanViewAnalytics(membership.Role))
        {
            throw DomainException.NotFound("organization_not_found", "That organization was not found.");
        }
    }

    private async Task EnsureBillingAccessAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        var membership = await store.FindMembershipAsync(organizationId, userId, false, cancellationToken);
        if (membership is not { IsActive: true } || !OrganizationRoles.CanManageBilling(membership.Role))
        {
            throw DomainException.NotFound("organization_not_found", "That organization was not found.");
        }
    }

    private async Task EnsureClinicInOrganizationAsync(Guid organizationId, Guid? clinicId, Guid? branchId, CancellationToken cancellationToken)
    {
        if (clinicId is null && branchId is null)
        {
            return;
        }

        var clinics = await store.ListClinicsForOrganizationAsync(organizationId, cancellationToken);
        if (clinicId is Guid cid)
        {
            var clinic = clinics.SingleOrDefault(item => item.Id == cid)
                ?? throw DomainException.NotFound("clinic_not_found", "That clinic was not found in this organization.");
            if (branchId is Guid bid && clinic.Branches.All(item => item.Id != bid || !item.IsActive))
            {
                throw DomainException.NotFound("branch_not_found", "That branch was not found.");
            }

            return;
        }

        if (branchId is Guid onlyBranch)
        {
            if (clinics.SelectMany(item => item.Branches).All(item => item.Id != onlyBranch || !item.IsActive))
            {
                throw DomainException.NotFound("branch_not_found", "That branch was not found.");
            }
        }
    }

    private async Task<OrganizationDetails> MapOrganizationAsync(Organization org, CancellationToken cancellationToken)
    {
        var clinics = await store.ListClinicsForOrganizationAsync(org.Id, cancellationToken);
        var members = await store.CountActiveMembershipsAsync(org.Id, cancellationToken);
        return new OrganizationDetails(org.Id, org.Name, org.Kind.ToString(), org.IsActive, org.CreatedAtUtc, clinics.Count, members);
    }

    private static OrganizationSubscriptionDetails MapSubscription(OrganizationSubscription subscription) =>
        new(
            subscription.OrganizationId,
            subscription.PlanCode,
            subscription.Status.ToString(),
            subscription.SeatLimit,
            subscription.BillingEmail,
            subscription.CurrentPeriodEndUtc,
            "Organization subscription charges are separate from appointment commission settlements.");

    private AuditLog Audit(Guid userId, string action, RequestMetadata metadata) =>
        AuditLog.Record(userId, action, clock.UtcNow, metadata.IpAddress, metadata.UserAgent);
}
