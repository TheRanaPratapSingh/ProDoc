using FluentValidation;

namespace ProDoc.Api.Contracts;

public sealed record PreviewCouponRequest(Guid AppointmentId, string CouponCode);

public sealed class PreviewCouponRequestValidator : AbstractValidator<PreviewCouponRequest>
{
    public PreviewCouponRequestValidator()
    {
        RuleFor(request => request.AppointmentId).NotEmpty();
        RuleFor(request => request.CouponCode).NotEmpty().MaximumLength(32);
    }
}

public sealed record RedeemPointsRequest(int Points);

public sealed class RedeemPointsRequestValidator : AbstractValidator<RedeemPointsRequest>
{
    public RedeemPointsRequestValidator()
    {
        RuleFor(request => request.Points).GreaterThan(0);
    }
}

public sealed record MarketingConsentRequest(bool MarketingOptIn);

public sealed record CreateCouponRequest(
    string Code,
    string Name,
    string DiscountType,
    decimal DiscountValue,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    decimal? MinBookingAmount,
    decimal? MaxDiscountAmount,
    bool FirstBookingOnly,
    bool NewUserOnly,
    Guid? ClinicId,
    Guid? DoctorId,
    int? MaxRedemptions,
    int? MaxRedemptionsPerUser);

public sealed class CreateCouponRequestValidator : AbstractValidator<CreateCouponRequest>
{
    public CreateCouponRequestValidator()
    {
        RuleFor(request => request.Code).NotEmpty().MaximumLength(32);
        RuleFor(request => request.Name).NotEmpty().MaximumLength(120);
        RuleFor(request => request.DiscountType).NotEmpty();
        RuleFor(request => request.DiscountValue).GreaterThan(0);
    }
}

public sealed record SetActiveRequest(bool IsActive);

public sealed record CreateCampaignRequest(string Name, string Kind, string Title, string Body);

public sealed class CreateCampaignRequestValidator : AbstractValidator<CreateCampaignRequest>
{
    public CreateCampaignRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(120);
        RuleFor(request => request.Kind).NotEmpty();
        RuleFor(request => request.Title).NotEmpty().MaximumLength(120);
        RuleFor(request => request.Body).NotEmpty().MaximumLength(280);
    }
}

public sealed record AdminAdjustRewardRequest(Guid UserId, int PointsDelta, decimal CreditDeltaInr, string Reason);

public sealed class AdminAdjustRewardRequestValidator : AbstractValidator<AdminAdjustRewardRequest>
{
    public AdminAdjustRewardRequestValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.Reason).NotEmpty().MaximumLength(200);
    }
}
