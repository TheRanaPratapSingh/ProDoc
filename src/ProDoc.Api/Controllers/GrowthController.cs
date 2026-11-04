using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Api.Contracts;
using ProDoc.Application.Growth;
using ProDoc.Application.Providers;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class GrowthController(GrowthService growth) : ControllerBase
{
    [HasPermission(PermissionCodes.GrowthUse)]
    [HttpGet("growth/referral")]
    public Task<ReferralDashboard> Referral(CancellationToken cancellationToken) =>
        growth.GetReferralDashboardAsync(User.GetUserId(), cancellationToken);

    [HasPermission(PermissionCodes.GrowthUse)]
    [HttpGet("growth/wallet")]
    public Task<RewardWalletDetails> Wallet(CancellationToken cancellationToken) =>
        growth.GetWalletAsync(User.GetUserId(), cancellationToken);

    [HasPermission(PermissionCodes.GrowthUse)]
    [HttpPost("growth/wallet/redeem")]
    public Task<RewardWalletDetails> Redeem(RedeemPointsRequest request, CancellationToken cancellationToken) =>
        growth.RedeemPointsAsync(User.GetUserId(), request.Points, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.GrowthUse)]
    [HttpGet("growth/coupons")]
    public Task<IReadOnlyList<CouponDetails>> Coupons(CancellationToken cancellationToken) =>
        growth.ListActiveCouponsAsync(cancellationToken);

    [HasPermission(PermissionCodes.GrowthUse)]
    [HttpPost("growth/coupons/preview")]
    public Task<CouponPreview> PreviewCoupon(PreviewCouponRequest request, CancellationToken cancellationToken) =>
        growth.PreviewCouponAsync(User.GetUserId(), request.AppointmentId, request.CouponCode, cancellationToken);

    [HasPermission(PermissionCodes.GrowthUse)]
    [HttpGet("growth/marketing-consent")]
    public Task<MarketingConsentDetails> GetConsent(CancellationToken cancellationToken) =>
        growth.GetConsentAsync(User.GetUserId(), cancellationToken);

    [HasPermission(PermissionCodes.GrowthUse)]
    [HttpPut("growth/marketing-consent")]
    public Task<MarketingConsentDetails> PutConsent(MarketingConsentRequest request, CancellationToken cancellationToken) =>
        growth.SetConsentAsync(User.GetUserId(), request.MarketingOptIn, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.GrowthManage)]
    [HttpGet("admin/growth/dashboard")]
    public Task<GrowthDashboard> Dashboard(CancellationToken cancellationToken) =>
        growth.GetGrowthDashboardAsync(cancellationToken);

    [HasPermission(PermissionCodes.GrowthManage)]
    [HttpGet("admin/growth/coupons")]
    public Task<IReadOnlyList<CouponDetails>> AdminCoupons(CancellationToken cancellationToken) =>
        growth.AdminListCouponsAsync(cancellationToken);

    [HasPermission(PermissionCodes.GrowthManage)]
    [HttpPost("admin/growth/coupons")]
    public Task<CouponDetails> CreateCoupon(CreateCouponRequest request, CancellationToken cancellationToken) =>
        growth.AdminCreateCouponAsync(
            User.GetUserId(),
            request.Code,
            request.Name,
            request.DiscountType,
            request.DiscountValue,
            request.StartsAtUtc,
            request.EndsAtUtc,
            request.MinBookingAmount,
            request.MaxDiscountAmount,
            request.FirstBookingOnly,
            request.NewUserOnly,
            request.ClinicId,
            request.DoctorId,
            request.MaxRedemptions,
            request.MaxRedemptionsPerUser,
            Meta(),
            cancellationToken);

    [HasPermission(PermissionCodes.GrowthManage)]
    [HttpPost("admin/growth/coupons/{couponId:guid}/active")]
    public Task<CouponDetails> SetCouponActive(Guid couponId, SetActiveRequest request, CancellationToken cancellationToken) =>
        growth.AdminSetCouponActiveAsync(User.GetUserId(), couponId, request.IsActive, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.GrowthManage)]
    [HttpGet("admin/growth/campaigns")]
    public Task<IReadOnlyList<CampaignDetails>> Campaigns(CancellationToken cancellationToken) =>
        growth.ListCampaignsAsync(cancellationToken);

    [HasPermission(PermissionCodes.GrowthManage)]
    [HttpPost("admin/growth/campaigns")]
    public Task<CampaignDetails> CreateCampaign(CreateCampaignRequest request, CancellationToken cancellationToken) =>
        growth.CreateCampaignAsync(User.GetUserId(), request.Name, request.Kind, request.Title, request.Body, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.GrowthManage)]
    [HttpPost("admin/growth/campaigns/{campaignId:guid}/active")]
    public Task<CampaignDetails> SetCampaignActive(Guid campaignId, SetActiveRequest request, CancellationToken cancellationToken) =>
        growth.SetCampaignActiveAsync(User.GetUserId(), campaignId, request.IsActive, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.GrowthManage)]
    [HttpPost("admin/growth/rewards/adjust")]
    public Task<RewardWalletDetails> AdjustRewards(AdminAdjustRewardRequest request, CancellationToken cancellationToken) =>
        growth.AdminAdjustAsync(User.GetUserId(), request.UserId, request.PointsDelta, request.CreditDeltaInr, request.Reason, Meta(), cancellationToken);

    private RequestMetadata Meta() => new(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext));
}
