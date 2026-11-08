using FluentValidation;
using ProDoc.Api.Validation;

namespace ProDoc.Api.Contracts;

public sealed record CreateOrganizationRequest(string Name, string Kind);

public sealed class CreateOrganizationRequestValidator : AbstractValidator<CreateOrganizationRequest>
{
    public CreateOrganizationRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(160);
        RuleFor(request => request.Kind).NotEmpty();
    }
}

public sealed record UpdateOrganizationRequest(string Name, bool? IsActive);

public sealed record LinkClinicRequest(Guid ClinicId);

public sealed record InviteOrgMemberRequest(string PhoneNumber, string Role, Guid? ClinicId, Guid? BranchId);

public sealed class InviteOrgMemberRequestValidator : AbstractValidator<InviteOrgMemberRequest>
{
    public InviteOrgMemberRequestValidator()
    {
        RuleFor(request => request.PhoneNumber).NotEmpty().MaximumLength(20).MustBeIndianMobile();
        RuleFor(request => request.Role).NotEmpty();
    }
}

public sealed record TransferOrgMemberRequest(Guid ClinicId, Guid? BranchId);

public sealed record SetMemberActiveRequest(bool IsActive);

public sealed record UpsertOrgSettingRequest(string Key, string Value);

public sealed record CreateResourceRequest(Guid ClinicId, Guid BranchId, string Name, string Kind);

public sealed record AllocateResourceRequest(Guid ResourceId, DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc, Guid? DoctorId, Guid? ScheduleId);

public sealed record UpdateOrgSubscriptionRequest(
    string PlanCode,
    string Status,
    int SeatLimit,
    string? BillingEmail,
    DateTimeOffset? CurrentPeriodEndUtc);
