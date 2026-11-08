using ProDoc.Application.Abstractions;
using ProDoc.Application.Providers;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Growth;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Queue;

namespace ProDoc.Application.Growth;

public sealed class GrowthService(IGrowthStore store, IClock clock)
{
    private const string ShareBaseUrl = "https://prodoc.app/invite/";

    public async Task SeedDefaultsAsync(CancellationToken cancellationToken)
    {
        var campaigns = await store.ListCampaignsAsync(cancellationToken);
        if (campaigns.Count == 0)
        {
            store.AddCampaign(MarketingCampaign.Create(
                "New patient welcome",
                CampaignKind.Onboarding,
                "Welcome to ProDoc",
                "Book your first visit with a verified doctor when you are ready.",
                clock.UtcNow));
            store.AddCampaign(MarketingCampaign.Create(
                "Incomplete booking reminder",
                CampaignKind.IncompleteBooking,
                "Finish your booking",
                "Your appointment hold may expire soon. Complete booking when ready.",
                clock.UtcNow));
            store.AddCampaign(MarketingCampaign.Create(
                "Post-visit follow-up",
                CampaignKind.AppointmentFollowUp,
                "How was your visit?",
                "Thank you for visiting. Open ProDoc for records and follow-up options.",
                clock.UtcNow));
            store.AddCampaign(MarketingCampaign.Create(
                "Referral invitation",
                CampaignKind.Referral,
                "Invite a friend",
                "Share your ProDoc referral code and earn rewards after their first paid visit.",
                clock.UtcNow));
        }

        var coupons = await store.ListAllCouponsAsync(cancellationToken);
        if (coupons.Count == 0)
        {
            store.AddCoupon(Coupon.Create(
                "WELCOME10",
                "Welcome 10% off first booking",
                CouponDiscountType.Percent,
                10m,
                clock.UtcNow.AddDays(-1),
                clock.UtcNow.AddYears(1),
                clock.UtcNow,
                minBookingAmount: 100m,
                maxDiscountAmount: 200m,
                firstBookingOnly: true,
                newUserOnly: true,
                maxRedemptions: 10000,
                maxPerUser: 1));
        }

        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task<ReferralDashboard> GetReferralDashboardAsync(Guid userId, CancellationToken cancellationToken)
    {
        var code = await EnsureReferralCodeAsync(userId, cancellationToken);
        var account = await EnsureRewardAccountAsync(userId, cancellationToken);
        var rows = await store.ListRelationshipsForReferrerAsync(userId, 50, cancellationToken);
        var names = await store.FindPatientDisplayNamesAsync(rows.Select(item => item.ReferredUserId).Distinct().ToArray(), cancellationToken);
        var rewardPoints = await IntSettingAsync("growth.referral_reward_points", GrowthDefaults.ReferralRewardPoints, cancellationToken);
        var shareUrl = ShareBaseUrl + code.Code;
        var shareText =
            $"Join ProDoc and easily book appointments with verified doctors near you. Use my referral code {code.Code} when registering: {shareUrl}";
        return new ReferralDashboard(
            code.Code,
            shareUrl,
            shareText,
            rows.Count(item => item.Status != ReferralStatus.Rejected),
            rows.Count(item => item.Status is ReferralStatus.Converted or ReferralStatus.Rewarded),
            rows.Count(item => item.Status == ReferralStatus.Rewarded),
            account.PointsBalance,
            account.CreditBalanceInr,
            rows.Select(item => new ReferralItem(
                item.Id,
                item.Status.ToString(),
                MapReferralDisplayStatus(item.Status),
                MaskReferralLabel(names.GetValueOrDefault(item.ReferredUserId)),
                item.RegisteredAtUtc,
                item.ConvertedAtUtc,
                item.RewardedAtUtc,
                item.Status == ReferralStatus.Rewarded ? rewardPoints : null)).ToArray());
    }

    public async Task AttributeReferralOnSignupAsync(Guid referredUserId, string? referralCode, RequestMetadata metadata, CancellationToken cancellationToken)
    {
        // Every new patient gets a durable referral code + wallet (idempotent).
        await EnsureReferralCodeAsync(referredUserId, cancellationToken);
        await EnsureRewardAccountAsync(referredUserId, cancellationToken);

        if (string.IsNullOrWhiteSpace(referralCode))
        {
            await TrySendCampaignKindAsync(referredUserId, CampaignKind.Onboarding, referredUserId, cancellationToken);
            await store.SaveChangesAsync(cancellationToken);
            return;
        }

        string normalized;
        try
        {
            normalized = ReferralCode.Normalize(referralCode);
        }
        catch (DomainException)
        {
            await TrySendCampaignKindAsync(referredUserId, CampaignKind.Onboarding, referredUserId, cancellationToken);
            await store.SaveChangesAsync(cancellationToken);
            return;
        }

        if (await store.FindRelationshipByReferredAsync(referredUserId, false, cancellationToken) is not null)
        {
            await store.SaveChangesAsync(cancellationToken);
            return;
        }

        var code = await store.FindReferralCodeByCodeAsync(normalized, false, cancellationToken);
        if (code is null || code.UserId == referredUserId)
        {
            await TrySendCampaignKindAsync(referredUserId, CampaignKind.Onboarding, referredUserId, cancellationToken);
            await store.SaveChangesAsync(cancellationToken);
            return;
        }

        store.AddReferralRelationship(ReferralRelationship.Create(code.UserId, referredUserId, normalized, clock.UtcNow));
        await store.AddAuditAsync(Audit(referredUserId, AuditActions.ReferralAttributed, metadata), cancellationToken);
        await TrySendCampaignKindAsync(referredUserId, CampaignKind.Onboarding, referredUserId, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task OnPaymentPaidAsync(Guid appointmentId, Guid paymentId, CancellationToken cancellationToken)
    {
        var appointment = await store.FindAppointmentAsync(appointmentId, false, cancellationToken);
        if (appointment is null || appointment.IsWalkIn)
        {
            return;
        }

        if (appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.Rejected or AppointmentStatus.Expired)
        {
            return;
        }

        var relationship = await store.FindRelationshipByReferredAsync(appointment.AccountUserId, true, cancellationToken);
        if (relationship is null || relationship.Status is not ReferralStatus.Registered)
        {
            return;
        }

        relationship.MarkConverted(appointment.Id, clock.UtcNow);
        await TryRewardReferrerAsync(relationship, appointment.Id, paymentId, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task OnAppointmentCompletedAsync(Guid appointmentId, CancellationToken cancellationToken)
    {
        var appointment = await store.FindAppointmentAsync(appointmentId, false, cancellationToken);
        if (appointment is null || appointment.IsWalkIn)
        {
            return;
        }

        if (appointment.Status != AppointmentStatus.Completed)
        {
            return;
        }

        if (appointment.PaymentStatus != "Paid")
        {
            return;
        }

        var key = $"loyalty:{appointment.Id:N}";
        if (await store.FindRewardByIdempotencyAsync(key, cancellationToken) is not null)
        {
            return;
        }

        var points = await IntSettingAsync("growth.loyalty_points_per_visit", GrowthDefaults.LoyaltyPointsPerCompletedVisit, cancellationToken);
        if (points <= 0)
        {
            return;
        }

        await CreditAsync(
            appointment.AccountUserId,
            RewardEntryType.Earn,
            points,
            0m,
            "Loyalty points for completed visit",
            key,
            "appointment",
            appointment.Id,
            clock.UtcNow.AddDays(365),
            cancellationToken);

        var relationship = await store.FindRelationshipByReferredAsync(appointment.AccountUserId, true, cancellationToken);
        if (relationship is not null && relationship.Status == ReferralStatus.Converted)
        {
            await TryRewardReferrerAsync(relationship, appointment.Id, null, cancellationToken);
        }

        await TrySendCampaignKindAsync(appointment.AccountUserId, CampaignKind.AppointmentFollowUp, appointment.Id, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task NotifyIncompleteBookingAsync(Guid userId, Guid appointmentId, CancellationToken cancellationToken)
    {
        await TrySendCampaignKindAsync(userId, CampaignKind.IncompleteBooking, appointmentId, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task<RewardWalletDetails> GetWalletAsync(Guid userId, CancellationToken cancellationToken)
    {
        var account = await EnsureRewardAccountAsync(userId, cancellationToken);
        await ExpireDueRewardsAsync(userId, cancellationToken);
        account = await EnsureRewardAccountAsync(userId, cancellationToken);
        var history = await store.ListRewardHistoryAsync(userId, 100, cancellationToken);
        var pointsPerInr = await IntSettingAsync("growth.points_per_inr", GrowthDefaults.PointsPerInr, cancellationToken);
        if (pointsPerInr <= 0)
        {
            pointsPerInr = GrowthDefaults.PointsPerInr;
        }

        var earned = history.Where(item => item.PointsDelta > 0).Sum(item => item.PointsDelta);
        var redeemed = history.Where(item => item.PointsDelta < 0).Sum(item => Math.Abs(item.PointsDelta));
        return new RewardWalletDetails(
            account.PointsBalance,
            account.CreditBalanceInr,
            earned,
            redeemed,
            pointsPerInr,
            pointsPerInr <= 0 ? 0m : Math.Floor(account.PointsBalance / (decimal)pointsPerInr),
            history.Select(MapLedger).ToArray());
    }

    public async Task<RewardWalletDetails> RedeemPointsAsync(
        Guid userId,
        int points,
        string? idempotencyKey,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var pointsPerInr = await IntSettingAsync("growth.points_per_inr", GrowthDefaults.PointsPerInr, cancellationToken);
        if (pointsPerInr <= 0)
        {
            pointsPerInr = GrowthDefaults.PointsPerInr;
        }

        if (points <= 0 || points % pointsPerInr != 0)
        {
            throw DomainException.Validation(
                "reward_redeem_invalid",
                $"Redeem points in multiples of {pointsPerInr}.");
        }

        var key = string.IsNullOrWhiteSpace(idempotencyKey)
            ? $"redeem:{userId:N}:{points}:{Guid.NewGuid():N}"
            : $"redeem:{userId:N}:{idempotencyKey.Trim()}";
        if (key.Length > 120)
        {
            throw DomainException.Validation("reward_redeem_key_invalid", "That redemption request is invalid.");
        }

        if (await store.FindRewardByIdempotencyAsync(key, cancellationToken) is not null)
        {
            return await GetWalletAsync(userId, cancellationToken);
        }

        await ExpireDueRewardsAsync(userId, cancellationToken);
        var account = await EnsureRewardAccountAsync(userId, cancellationToken);
        if (account.PointsBalance < points)
        {
            throw DomainException.Validation("reward_balance_insufficient", "Insufficient reward balance.");
        }

        var credit = points / (decimal)pointsPerInr;
        await CreditAsync(userId, RewardEntryType.Redeem, -points, credit, "Points redeemed to ProDoc booking credit", key, "redeem", null, null, cancellationToken);
        await store.AddAuditAsync(Audit(userId, AuditActions.RewardRedeemed, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return await GetWalletAsync(userId, cancellationToken);
    }

    public async Task<RewardWalletDetails> AdminAdjustAsync(
        Guid adminUserId,
        Guid targetUserId,
        int pointsDelta,
        decimal creditDeltaInr,
        string reason,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (pointsDelta == 0 && creditDeltaInr == 0)
        {
            throw DomainException.Validation("reward_adjust_empty", "Provide a non-zero adjustment.");
        }

        var key = $"adjust:{targetUserId:N}:{Guid.NewGuid():N}";
        await CreditAsync(
            targetUserId,
            RewardEntryType.Adjust,
            pointsDelta,
            creditDeltaInr,
            reason,
            key,
            "admin_adjust",
            adminUserId,
            null,
            cancellationToken);
        await store.AddAuditAsync(Audit(adminUserId, AuditActions.RewardAdjusted, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return await GetWalletAsync(targetUserId, cancellationToken);
    }

    public async Task<CouponPreview> PreviewCouponAsync(
        Guid userId,
        Guid appointmentId,
        string couponCode,
        CancellationToken cancellationToken)
    {
        var appointment = await RequirePayableAppointmentAsync(userId, appointmentId, cancellationToken);
        var (coupon, discount) = await ValidateCouponAsync(userId, appointment, couponCode, cancellationToken);
        return new CouponPreview(
            coupon.Code,
            appointment.ConsultationFee,
            discount,
            appointment.ConsultationFee - discount,
            "Coupon is valid for this booking.");
    }

    public async Task<(decimal DiscountAmount, CouponRedemption? Redemption)> ApplyCouponForPaymentAsync(
        Guid userId,
        Appointment appointment,
        string? couponCode,
        string paymentIdempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(couponCode))
        {
            return (0m, null);
        }

        var existing = await store.FindRedemptionByIdempotencyAsync($"coupon:{paymentIdempotencyKey}", cancellationToken);
        if (existing is not null)
        {
            return (existing.DiscountAmount, existing);
        }

        if (await store.FindRedemptionByAppointmentAsync(appointment.Id, cancellationToken) is not null)
        {
            throw DomainException.Conflict("coupon_already_applied", "A coupon was already applied to this appointment.");
        }

        var (coupon, discount) = await ValidateCouponAsync(userId, appointment, couponCode, cancellationToken);
        var redemption = CouponRedemption.Create(
            coupon.Id,
            userId,
            appointment.Id,
            discount,
            $"coupon:{paymentIdempotencyKey}",
            clock.UtcNow);
        coupon.IncrementRedemption();
        store.AddCouponRedemption(redemption);
        return (discount, redemption);
    }

    public async Task<(decimal CreditUsed, RewardTransaction? Transaction)> ApplyRewardCreditForPaymentAsync(
        Guid userId,
        Appointment appointment,
        decimal payableBeforeCredit,
        bool useRewardCredit,
        string paymentIdempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!useRewardCredit || payableBeforeCredit <= 0)
        {
            return (0m, null);
        }

        var key = $"paycredit:{paymentIdempotencyKey}";
        var existing = await store.FindRewardByIdempotencyAsync(key, cancellationToken);
        if (existing is not null)
        {
            return (Math.Abs(existing.CreditDeltaInr), existing);
        }

        await ExpireDueRewardsAsync(userId, cancellationToken);
        var account = await EnsureRewardAccountAsync(userId, cancellationToken);
        var use = Math.Min(account.CreditBalanceInr, payableBeforeCredit);
        if (use <= 0)
        {
            return (0m, null);
        }

        var tx = await CreditAsync(
            userId,
            RewardEntryType.Redeem,
            0,
            -use,
            "Promotional credit applied to booking payment",
            key,
            "appointment",
            appointment.Id,
            null,
            cancellationToken);
        return (use, tx);
    }

    public async Task<IReadOnlyList<CouponDetails>> ListActiveCouponsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var coupons = await store.ListActiveCouponsAsync(clock.UtcNow, cancellationToken);
        var paidCount = await store.CountPaidAppointmentsForUserAsync(userId, cancellationToken);
        var mapped = new List<CouponDetails>(coupons.Count);
        foreach (var coupon in coupons)
        {
            var used = await store.CountUserRedemptionsAsync(coupon.Id, userId, cancellationToken);
            var alreadyUsed = coupon.MaxRedemptionsPerUser is int perUser && used >= perUser;
            string? note = null;
            var eligible = true;
            if (alreadyUsed)
            {
                eligible = false;
                note = "Already used";
            }
            else if (coupon.FirstBookingOnly && paidCount > 0)
            {
                eligible = false;
                note = "First booking only";
            }
            else if (coupon.NewUserOnly && paidCount > 0)
            {
                eligible = false;
                note = "New patients only";
            }
            else if (coupon.MaxRedemptions is int globalMax && coupon.RedemptionCount >= globalMax)
            {
                eligible = false;
                note = "Fully redeemed";
            }

            mapped.Add(MapCoupon(coupon, used, alreadyUsed, eligible, note));
        }

        return mapped;
    }

    public Task<IReadOnlyList<CouponDetails>> AdminListCouponsAsync(CancellationToken cancellationToken) =>
        MapCouponsAsync(store.ListAllCouponsAsync(cancellationToken));

    public async Task<CouponDetails> AdminCreateCouponAsync(
        Guid adminUserId,
        string code,
        string name,
        string discountType,
        decimal discountValue,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        decimal? minBookingAmount,
        decimal? maxDiscountAmount,
        bool firstBookingOnly,
        bool newUserOnly,
        Guid? clinicId,
        Guid? doctorId,
        int? maxRedemptions,
        int? maxPerUser,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CouponDiscountType>(discountType, true, out var type))
        {
            throw DomainException.Validation("coupon_type_invalid", "Choose fixed or percent discount.");
        }

        if (await store.FindCouponByCodeAsync(Coupon.NormalizeCode(code), false, cancellationToken) is not null)
        {
            throw DomainException.Conflict("coupon_exists", "That coupon code already exists.");
        }

        var coupon = Coupon.Create(
            code,
            name,
            type,
            discountValue,
            startsAtUtc,
            endsAtUtc,
            clock.UtcNow,
            minBookingAmount,
            maxDiscountAmount,
            firstBookingOnly,
            newUserOnly,
            clinicId,
            doctorId,
            maxRedemptions,
            maxPerUser);
        store.AddCoupon(coupon);
        await store.AddAuditAsync(Audit(adminUserId, AuditActions.CouponCreated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapCoupon(coupon);
    }

    public async Task<CouponDetails> AdminSetCouponActiveAsync(Guid adminUserId, Guid couponId, bool isActive, RequestMetadata metadata, CancellationToken cancellationToken)
    {
        var coupon = await store.FindCouponAsync(couponId, true, cancellationToken)
            ?? throw DomainException.NotFound("coupon_not_found", "That coupon was not found.");
        coupon.SetActive(isActive);
        await store.AddAuditAsync(Audit(adminUserId, AuditActions.CouponUpdated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapCoupon(coupon);
    }

    public async Task<MarketingConsentDetails> GetConsentAsync(Guid userId, CancellationToken cancellationToken)
    {
        var consent = await EnsureConsentAsync(userId, cancellationToken);
        return new MarketingConsentDetails(consent.MarketingOptIn, consent.UpdatedAtUtc);
    }

    public async Task<MarketingConsentDetails> SetConsentAsync(Guid userId, bool optIn, RequestMetadata metadata, CancellationToken cancellationToken)
    {
        var consent = await EnsureConsentAsync(userId, cancellationToken);
        consent.SetOptIn(optIn, clock.UtcNow);
        await store.AddAuditAsync(Audit(userId, AuditActions.MarketingConsentChanged, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return new MarketingConsentDetails(consent.MarketingOptIn, consent.UpdatedAtUtc);
    }

    public async Task<IReadOnlyList<CampaignDetails>> ListCampaignsAsync(CancellationToken cancellationToken)
    {
        var rows = await store.ListCampaignsAsync(cancellationToken);
        var result = new List<CampaignDetails>();
        foreach (var row in rows)
        {
            result.Add(new CampaignDetails(
                row.Id,
                row.Name,
                row.Kind.ToString(),
                row.Title,
                row.Body,
                row.IsActive,
                row.CreatedAtUtc,
                row.UpdatedAtUtc,
                await store.CountCampaignDeliveriesAsync(row.Id, cancellationToken)));
        }

        return result;
    }

    public async Task<CampaignDetails> CreateCampaignAsync(
        Guid adminUserId,
        string name,
        string kind,
        string title,
        string body,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CampaignKind>(kind, true, out var parsed))
        {
            throw DomainException.Validation("campaign_kind_invalid", "Choose a valid campaign kind.");
        }

        var campaign = MarketingCampaign.Create(name, parsed, title, body, clock.UtcNow);
        store.AddCampaign(campaign);
        await store.AddAuditAsync(Audit(adminUserId, AuditActions.CampaignCreated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return new CampaignDetails(campaign.Id, campaign.Name, campaign.Kind.ToString(), campaign.Title, campaign.Body, campaign.IsActive, campaign.CreatedAtUtc, campaign.UpdatedAtUtc, 0);
    }

    public async Task<CampaignDetails> SetCampaignActiveAsync(Guid adminUserId, Guid campaignId, bool isActive, RequestMetadata metadata, CancellationToken cancellationToken)
    {
        var campaign = await store.FindCampaignAsync(campaignId, true, cancellationToken)
            ?? throw DomainException.NotFound("campaign_not_found", "That campaign was not found.");
        campaign.SetActive(isActive, clock.UtcNow);
        await store.AddAuditAsync(Audit(adminUserId, AuditActions.CampaignUpdated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return new CampaignDetails(
            campaign.Id,
            campaign.Name,
            campaign.Kind.ToString(),
            campaign.Title,
            campaign.Body,
            campaign.IsActive,
            campaign.CreatedAtUtc,
            campaign.UpdatedAtUtc,
            await store.CountCampaignDeliveriesAsync(campaign.Id, cancellationToken));
    }

    public Task<GrowthDashboard> GetGrowthDashboardAsync(CancellationToken cancellationToken) =>
        store.BuildGrowthDashboardAsync(cancellationToken);

    private async Task TryRewardReferrerAsync(
        ReferralRelationship relationship,
        Guid appointmentId,
        Guid? paymentId,
        CancellationToken cancellationToken)
    {
        if (relationship.Status != ReferralStatus.Converted)
        {
            return;
        }

        var dayStart = clock.UtcNow.UtcDateTime.Date;
        var dayStartUtc = new DateTimeOffset(dayStart, TimeSpan.Zero);
        var maxPerDay = await IntSettingAsync("growth.max_referral_rewards_per_day", GrowthDefaults.MaxReferralRewardsPerDay, cancellationToken);
        if (await store.CountReferralRewardsTodayAsync(relationship.ReferrerUserId, dayStartUtc, cancellationToken) >= maxPerDay)
        {
            return;
        }

        var key = $"referral:{relationship.Id:N}";
        if (await store.FindRewardByIdempotencyAsync(key, cancellationToken) is not null)
        {
            relationship.MarkRewarded(clock.UtcNow);
            return;
        }

        var points = await IntSettingAsync("growth.referral_reward_points", GrowthDefaults.ReferralRewardPoints, cancellationToken);
        var credit = await DecimalSettingAsync("growth.referral_reward_credit", GrowthDefaults.ReferralRewardCreditInr, cancellationToken);
        await CreditAsync(
            relationship.ReferrerUserId,
            RewardEntryType.Earn,
            points,
            credit,
            "Referral reward after successful conversion",
            key,
            "referral",
            relationship.Id,
            clock.UtcNow.AddDays(365),
            cancellationToken);
        relationship.MarkRewarded(clock.UtcNow);
        await NotifyAsync(
            relationship.ReferrerUserId,
            "referral_reward",
            relationship.Id,
            "Referral reward earned",
            "A referred patient completed their first paid visit. Your reward was added to your wallet.",
            cancellationToken);
        _ = paymentId;
        _ = appointmentId;
    }

    private async Task<(Coupon Coupon, decimal Discount)> ValidateCouponAsync(
        Guid userId,
        Appointment appointment,
        string couponCode,
        CancellationToken cancellationToken)
    {
        var coupon = await store.FindCouponByCodeAsync(Coupon.NormalizeCode(couponCode), true, cancellationToken)
            ?? throw DomainException.Validation("coupon_not_found", "That coupon was not found.");
        coupon.EnsureRedeemable(clock.UtcNow);

        if (coupon.ClinicId is Guid clinicId && clinicId != appointment.ClinicId)
        {
            throw DomainException.Validation("coupon_clinic_mismatch", "That coupon is not valid at this clinic.");
        }

        if (coupon.DoctorId is Guid doctorId && doctorId != appointment.DoctorId)
        {
            throw DomainException.Validation("coupon_doctor_mismatch", "That coupon is not valid for this doctor.");
        }

        if (coupon.MinBookingAmount is decimal min && appointment.ConsultationFee < min)
        {
            throw DomainException.Validation("coupon_min_amount", "Booking amount is below the coupon minimum.");
        }

        var paidCount = await store.CountPaidAppointmentsForUserAsync(userId, cancellationToken);
        if (coupon.FirstBookingOnly && paidCount > 0)
        {
            throw DomainException.Validation("coupon_first_booking_only", "That coupon is only for a first booking.");
        }

        if (coupon.NewUserOnly && paidCount > 0)
        {
            throw DomainException.Validation("coupon_new_user_only", "That coupon is only for new patients.");
        }

        if (coupon.MaxRedemptionsPerUser is int perUser)
        {
            var used = await store.CountUserRedemptionsAsync(coupon.Id, userId, cancellationToken);
            if (used >= perUser)
            {
                throw DomainException.Conflict("coupon_user_limit", "You have already used this coupon the maximum number of times.");
            }
        }

        var discount = coupon.CalculateDiscount(appointment.ConsultationFee);
        if (discount <= 0)
        {
            throw DomainException.Validation("coupon_no_discount", "That coupon does not reduce this booking.");
        }

        return (coupon, discount);
    }

    private async Task<Appointment> RequirePayableAppointmentAsync(Guid userId, Guid appointmentId, CancellationToken cancellationToken)
    {
        var appointment = await store.FindAppointmentAsync(appointmentId, false, cancellationToken)
            ?? throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        if (appointment.AccountUserId != userId || appointment.IsWalkIn)
        {
            throw DomainException.NotFound("appointment_not_found", "That appointment was not found.");
        }

        if (appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.Rejected or AppointmentStatus.Expired)
        {
            throw DomainException.Conflict("appointment_not_payable", "This appointment cannot accept a coupon.");
        }

        return appointment;
    }

    private async Task TrySendCampaignKindAsync(Guid userId, CampaignKind kind, Guid? subjectId, CancellationToken cancellationToken)
    {
        var consent = await EnsureConsentAsync(userId, cancellationToken);
        var requiresOptIn = kind is CampaignKind.Promotion or CampaignKind.Referral or CampaignKind.Reengagement or CampaignKind.IncompleteBooking;
        if (requiresOptIn && !consent.MarketingOptIn)
        {
            return;
        }

        var campaign = await store.FindActiveCampaignByKindAsync(kind, cancellationToken);
        if (campaign is null)
        {
            return;
        }

        var cooldownDays = await IntSettingAsync("growth.campaign_cooldown_days", 7, cancellationToken);
        var since = clock.UtcNow.AddDays(-cooldownDays);
        if (await store.HasRecentCampaignDeliveryAsync(campaign.Id, userId, since, cancellationToken))
        {
            return;
        }

        if (await store.HasRecentNotificationAsync(userId, $"campaign:{campaign.Kind}", subjectId, clock.UtcNow - QueueDefaults.NotificationCooldown, cancellationToken))
        {
            return;
        }

        store.AddCampaignDelivery(CampaignDelivery.Create(campaign.Id, userId, subjectId, clock.UtcNow));
        await store.NotifyAsync(
            UserNotification.Create(userId, $"campaign:{campaign.Kind}", subjectId, campaign.Title, campaign.Body, clock.UtcNow),
            cancellationToken);
    }

    private async Task ExpireDueRewardsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var due = await store.ListExpirableEarnsAsync(clock.UtcNow, 200, cancellationToken);
        foreach (var earn in due.Where(item => item.UserId == userId && item.PointsDelta > 0))
        {
            var reverseKey = $"expire:{earn.Id:N}";
            if (await store.FindRewardByIdempotencyAsync(reverseKey, cancellationToken) is not null)
            {
                continue;
            }

            var account = await EnsureRewardAccountAsync(userId, cancellationToken);
            var points = Math.Min(account.PointsBalance, earn.PointsDelta);
            if (points <= 0)
            {
                continue;
            }

            await CreditAsync(
                userId,
                RewardEntryType.Expire,
                -points,
                0m,
                "Expired reward points",
                reverseKey,
                "reward_transaction",
                earn.Id,
                null,
                cancellationToken);
        }
    }

    private async Task<RewardTransaction> CreditAsync(
        Guid userId,
        RewardEntryType type,
        int pointsDelta,
        decimal creditDelta,
        string reason,
        string idempotencyKey,
        string? referenceType,
        Guid? referenceId,
        DateTimeOffset? expiresAtUtc,
        CancellationToken cancellationToken)
    {
        var existing = await store.FindRewardByIdempotencyAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var account = await EnsureRewardAccountAsync(userId, cancellationToken);
        account.Apply(pointsDelta, creditDelta, clock.UtcNow);
        var tx = RewardTransaction.Record(
            account.Id,
            userId,
            type,
            pointsDelta,
            creditDelta,
            reason,
            idempotencyKey,
            clock.UtcNow,
            referenceType,
            referenceId,
            expiresAtUtc);
        store.AddRewardTransaction(tx);
        return tx;
    }

    private Task<ReferralCode> EnsureReferralCodeAsync(Guid userId, CancellationToken cancellationToken) =>
        store.GetOrCreateReferralCodeAsync(userId, clock.UtcNow, cancellationToken);

    private Task<RewardAccount> EnsureRewardAccountAsync(Guid userId, CancellationToken cancellationToken) =>
        store.GetOrCreateRewardAccountAsync(userId, clock.UtcNow, cancellationToken);

    private Task<MarketingConsent> EnsureConsentAsync(Guid userId, CancellationToken cancellationToken) =>
        store.GetOrCreateConsentAsync(userId, clock.UtcNow, cancellationToken);

    private async Task NotifyAsync(Guid userId, string kind, Guid subjectId, string title, string body, CancellationToken cancellationToken)
    {
        var since = clock.UtcNow - QueueDefaults.NotificationCooldown;
        if (await store.HasRecentNotificationAsync(userId, kind, subjectId, since, cancellationToken))
        {
            return;
        }

        await store.NotifyAsync(UserNotification.Create(userId, kind, subjectId, title, body, clock.UtcNow), cancellationToken);
    }

    private async Task<int> IntSettingAsync(string key, int fallback, CancellationToken cancellationToken)
    {
        var raw = await store.FindSettingAsync(key, cancellationToken);
        return int.TryParse(raw, out var value) ? value : fallback;
    }

    private async Task<decimal> DecimalSettingAsync(string key, decimal fallback, CancellationToken cancellationToken)
    {
        var raw = await store.FindSettingAsync(key, cancellationToken);
        return decimal.TryParse(raw, out var value) ? value : fallback;
    }

    private static async Task<IReadOnlyList<CouponDetails>> MapCouponsAsync(Task<IReadOnlyList<Coupon>> source)
    {
        var rows = await source;
        return rows.Select(item => MapCoupon(item)).ToArray();
    }

    private static CouponDetails MapCoupon(
        Coupon coupon,
        int userRedemptionCount = 0,
        bool alreadyUsed = false,
        bool eligibleForUser = true,
        string? eligibilityNote = null) =>
        new(
            coupon.Id,
            coupon.Code,
            coupon.Name,
            coupon.DiscountType.ToString(),
            coupon.DiscountValue,
            coupon.MinBookingAmount,
            coupon.MaxDiscountAmount,
            coupon.FirstBookingOnly,
            coupon.NewUserOnly,
            coupon.ClinicId,
            coupon.DoctorId,
            coupon.MaxRedemptions,
            coupon.MaxRedemptionsPerUser,
            coupon.RedemptionCount,
            coupon.StartsAtUtc,
            coupon.EndsAtUtc,
            coupon.IsActive,
            userRedemptionCount,
            alreadyUsed,
            eligibleForUser,
            eligibilityNote);

    private static RewardLedgerItem MapLedger(RewardTransaction item) =>
        new(
            item.Id,
            item.EntryType.ToString(),
            item.PointsDelta,
            item.CreditDeltaInr,
            item.Reason,
            item.ReferenceType,
            item.ReferenceId,
            item.CreatedAtUtc,
            item.ExpiresAtUtc);

    private AuditLog Audit(Guid userId, string action, RequestMetadata metadata) =>
        AuditLog.Record(userId, action, clock.UtcNow, metadata.IpAddress, metadata.UserAgent);

    private static string MapReferralDisplayStatus(ReferralStatus status) =>
        status switch
        {
            ReferralStatus.Registered => "Registered",
            ReferralStatus.Converted => "Qualified",
            ReferralStatus.Rewarded => "Rewarded",
            ReferralStatus.Rejected => "Rejected",
            _ => status.ToString()
        };

    private static string MaskReferralLabel(string? fullName)
    {
        var name = (fullName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return "Patient";
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            return parts[0].Length <= 1 ? "Patient" : $"{parts[0][0]}***";
        }

        return $"{parts[0]} {parts[^1][0]}.";
    }
}
