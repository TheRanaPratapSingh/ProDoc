using ProDoc.Domain.Appointments;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Growth;
using ProDoc.Domain.Queue;

namespace ProDoc.Application.Growth;

public interface IGrowthStore
{
    Task<ReferralCode?> FindReferralCodeByUserAsync(Guid userId, bool tracking, CancellationToken cancellationToken);

    Task<ReferralCode?> FindReferralCodeByCodeAsync(string code, bool tracking, CancellationToken cancellationToken);

    void AddReferralCode(ReferralCode code);

    Task<ReferralRelationship?> FindRelationshipByReferredAsync(Guid referredUserId, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReferralRelationship>> ListRelationshipsForReferrerAsync(Guid referrerUserId, int take, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, string>> FindPatientDisplayNamesAsync(IReadOnlyList<Guid> userIds, CancellationToken cancellationToken);

    Task<int> CountReferralRewardsTodayAsync(Guid referrerUserId, DateTimeOffset dayStartUtc, CancellationToken cancellationToken);

    void AddReferralRelationship(ReferralRelationship relationship);

    Task<RewardAccount?> FindRewardAccountAsync(Guid userId, bool tracking, CancellationToken cancellationToken);

    void AddRewardAccount(RewardAccount account);

    /// <summary>Idempotent create under concurrent dashboard loads (unique UserId).</summary>
    Task<RewardAccount> GetOrCreateRewardAccountAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<ReferralCode> GetOrCreateReferralCodeAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<MarketingConsent> GetOrCreateConsentAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<RewardTransaction?> FindRewardByIdempotencyAsync(string idempotencyKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<RewardTransaction>> ListRewardHistoryAsync(Guid userId, int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<RewardTransaction>> ListExpirableEarnsAsync(DateTimeOffset now, int take, CancellationToken cancellationToken);

    void AddRewardTransaction(RewardTransaction transaction);

    Task<Coupon?> FindCouponByCodeAsync(string code, bool tracking, CancellationToken cancellationToken);

    Task<Coupon?> FindCouponAsync(Guid couponId, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<Coupon>> ListActiveCouponsAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task<IReadOnlyList<Coupon>> ListAllCouponsAsync(CancellationToken cancellationToken);

    void AddCoupon(Coupon coupon);

    Task<CouponRedemption?> FindRedemptionByAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken);

    Task<CouponRedemption?> FindRedemptionByIdempotencyAsync(string idempotencyKey, CancellationToken cancellationToken);

    Task<int> CountUserRedemptionsAsync(Guid couponId, Guid userId, CancellationToken cancellationToken);

    Task<int> CountPaidAppointmentsForUserAsync(Guid userId, CancellationToken cancellationToken);

    void AddCouponRedemption(CouponRedemption redemption);

    Task<MarketingConsent?> FindConsentAsync(Guid userId, bool tracking, CancellationToken cancellationToken);

    void AddConsent(MarketingConsent consent);

    Task<MarketingCampaign?> FindCampaignAsync(Guid campaignId, bool tracking, CancellationToken cancellationToken);

    Task<MarketingCampaign?> FindActiveCampaignByKindAsync(CampaignKind kind, CancellationToken cancellationToken);

    Task<IReadOnlyList<MarketingCampaign>> ListCampaignsAsync(CancellationToken cancellationToken);

    void AddCampaign(MarketingCampaign campaign);

    Task<bool> HasRecentCampaignDeliveryAsync(Guid campaignId, Guid userId, DateTimeOffset since, CancellationToken cancellationToken);

    Task<int> CountCampaignDeliveriesAsync(Guid campaignId, CancellationToken cancellationToken);

    void AddCampaignDelivery(CampaignDelivery delivery);

    Task<Appointment?> FindAppointmentAsync(Guid appointmentId, bool tracking, CancellationToken cancellationToken);

    Task<bool> HasRecentNotificationAsync(Guid userId, string kind, Guid? subjectId, DateTimeOffset since, CancellationToken cancellationToken);

    Task NotifyAsync(UserNotification notification, CancellationToken cancellationToken);

    Task<string?> FindSettingAsync(string key, CancellationToken cancellationToken);

    Task<GrowthDashboard> BuildGrowthDashboardAsync(CancellationToken cancellationToken);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
