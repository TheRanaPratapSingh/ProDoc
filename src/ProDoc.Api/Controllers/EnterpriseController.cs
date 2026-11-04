using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Api.Contracts;
using ProDoc.Application.Enterprise;
using ProDoc.Application.Providers;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class EnterpriseController(EnterpriseService enterprise) : ControllerBase
{
    [HasPermission(PermissionCodes.OrgAccess)]
    [HttpGet("organizations")]
    public Task<IReadOnlyList<OrganizationDetails>> List(CancellationToken cancellationToken) =>
        enterprise.ListMineAsync(User.GetUserId(), cancellationToken);

    [HasPermission(PermissionCodes.ProviderOnboard)]
    [HttpPost("organizations")]
    public Task<OrganizationDetails> Create(CreateOrganizationRequest request, CancellationToken cancellationToken) =>
        enterprise.CreateAsync(User.GetUserId(), request.Name, request.Kind, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.OrgManage)]
    [HttpPut("organizations/{organizationId:guid}")]
    public Task<OrganizationDetails> Update(Guid organizationId, UpdateOrganizationRequest request, CancellationToken cancellationToken) =>
        enterprise.UpdateAsync(User.GetUserId(), organizationId, request.Name, request.IsActive, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.OrgAccess)]
    [HttpGet("organizations/{organizationId:guid}/clinics")]
    public Task<IReadOnlyList<OrganizationClinicDetails>> Clinics(Guid organizationId, CancellationToken cancellationToken) =>
        enterprise.ListClinicsAsync(User.GetUserId(), organizationId, cancellationToken);

    [HasPermission(PermissionCodes.OrgManage)]
    [HttpPost("organizations/{organizationId:guid}/clinics")]
    public Task<OrganizationClinicDetails> LinkClinic(Guid organizationId, LinkClinicRequest request, CancellationToken cancellationToken) =>
        enterprise.LinkClinicAsync(User.GetUserId(), organizationId, request.ClinicId, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.OrgStaff)]
    [HttpGet("organizations/{organizationId:guid}/members")]
    public Task<IReadOnlyList<OrganizationMembershipDetails>> Members(Guid organizationId, CancellationToken cancellationToken) =>
        enterprise.ListMembersAsync(User.GetUserId(), organizationId, cancellationToken);

    [HasPermission(PermissionCodes.OrgStaff)]
    [HttpPost("organizations/{organizationId:guid}/members")]
    public Task<OrganizationMembershipDetails> Invite(Guid organizationId, InviteOrgMemberRequest request, CancellationToken cancellationToken) =>
        enterprise.InviteMemberAsync(User.GetUserId(), organizationId, request.PhoneNumber, request.Role, request.ClinicId, request.BranchId, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.OrgStaff)]
    [HttpPost("organizations/{organizationId:guid}/members/{membershipId:guid}/transfer")]
    public Task<OrganizationMembershipDetails> Transfer(Guid organizationId, Guid membershipId, TransferOrgMemberRequest request, CancellationToken cancellationToken) =>
        enterprise.TransferMemberAsync(User.GetUserId(), organizationId, membershipId, request.ClinicId, request.BranchId, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.OrgStaff)]
    [HttpPost("organizations/{organizationId:guid}/members/{membershipId:guid}/active")]
    public async Task<IActionResult> SetActive(Guid organizationId, Guid membershipId, SetMemberActiveRequest request, CancellationToken cancellationToken)
    {
        await enterprise.SuspendMemberAsync(User.GetUserId(), organizationId, membershipId, request.IsActive, Meta(), cancellationToken);
        return NoContent();
    }

    [HasPermission(PermissionCodes.OrgAccess)]
    [HttpGet("organizations/{organizationId:guid}/settings")]
    public Task<IReadOnlyList<OrganizationSettingDetails>> Settings(Guid organizationId, [FromQuery] Guid? branchId, CancellationToken cancellationToken) =>
        enterprise.ListEffectiveSettingsAsync(User.GetUserId(), organizationId, branchId, cancellationToken);

    [HasPermission(PermissionCodes.OrgManage)]
    [HttpPut("organizations/{organizationId:guid}/settings")]
    public Task<OrganizationSettingDetails> PutSetting(Guid organizationId, UpsertOrgSettingRequest request, CancellationToken cancellationToken) =>
        enterprise.UpsertOrganizationSettingAsync(User.GetUserId(), organizationId, request.Key, request.Value, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.OrgManage)]
    [HttpPut("organizations/{organizationId:guid}/branches/{branchId:guid}/settings")]
    public Task<OrganizationSettingDetails> PutBranchSetting(Guid organizationId, Guid branchId, UpsertOrgSettingRequest request, CancellationToken cancellationToken) =>
        enterprise.UpsertBranchOverrideAsync(User.GetUserId(), organizationId, branchId, request.Key, request.Value, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.OrgManage)]
    [HttpGet("organizations/{organizationId:guid}/resources")]
    public Task<IReadOnlyList<ConsultationResourceDetails>> Resources(Guid organizationId, [FromQuery] Guid? branchId, CancellationToken cancellationToken) =>
        enterprise.ListResourcesAsync(User.GetUserId(), organizationId, branchId, cancellationToken);

    [HasPermission(PermissionCodes.OrgManage)]
    [HttpPost("organizations/{organizationId:guid}/resources")]
    public Task<ConsultationResourceDetails> CreateResource(Guid organizationId, CreateResourceRequest request, CancellationToken cancellationToken) =>
        enterprise.CreateResourceAsync(User.GetUserId(), organizationId, request.ClinicId, request.BranchId, request.Name, request.Kind, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.OrgManage)]
    [HttpPost("organizations/{organizationId:guid}/resource-allocations")]
    public Task<ResourceAllocationDetails> Allocate(Guid organizationId, AllocateResourceRequest request, CancellationToken cancellationToken) =>
        enterprise.AllocateResourceAsync(User.GetUserId(), organizationId, request.ResourceId, request.StartsAtUtc, request.EndsAtUtc, request.DoctorId, request.ScheduleId, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.OrgBilling)]
    [HttpGet("organizations/{organizationId:guid}/subscription")]
    public Task<OrganizationSubscriptionDetails> Subscription(Guid organizationId, CancellationToken cancellationToken) =>
        enterprise.GetSubscriptionAsync(User.GetUserId(), organizationId, cancellationToken);

    [HasPermission(PermissionCodes.OrgBilling)]
    [HttpPut("organizations/{organizationId:guid}/subscription")]
    public Task<OrganizationSubscriptionDetails> PutSubscription(Guid organizationId, UpdateOrgSubscriptionRequest request, CancellationToken cancellationToken) =>
        enterprise.UpdateSubscriptionAsync(
            User.GetUserId(),
            organizationId,
            request.PlanCode,
            request.Status,
            request.SeatLimit,
            request.BillingEmail,
            request.CurrentPeriodEndUtc,
            Meta(),
            cancellationToken);

    [HasPermission(PermissionCodes.OrgAnalytics)]
    [HttpGet("organizations/{organizationId:guid}/analytics")]
    public Task<OrganizationAnalytics> Analytics(
        Guid organizationId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? clinicId,
        [FromQuery] Guid? branchId,
        [FromQuery] Guid? doctorId,
        CancellationToken cancellationToken) =>
        enterprise.GetAnalyticsAsync(User.GetUserId(), organizationId, from, to, clinicId, branchId, doctorId, cancellationToken);

    private RequestMetadata Meta() => new(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext));
}
