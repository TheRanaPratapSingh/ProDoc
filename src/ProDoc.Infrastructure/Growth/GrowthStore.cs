using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProDoc.Application.Growth;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Growth;
using ProDoc.Domain.Queue;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.Growth;

public sealed class GrowthStore(ProDocDbContext db) : IGrowthStore
{
    public Task<ReferralCode?> FindReferralCodeByUserAsync(Guid userId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.ReferralCodes.AsQueryable() : db.ReferralCodes.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
    }

    public Task<ReferralCode?> FindReferralCodeByCodeAsync(string code, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.ReferralCodes.AsQueryable() : db.ReferralCodes.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Code == code, cancellationToken);
    }

    public void AddReferralCode(ReferralCode code) => db.ReferralCodes.Add(code);

    public Task<ReferralRelationship?> FindRelationshipByReferredAsync(Guid referredUserId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.ReferralRelationships.AsQueryable() : db.ReferralRelationships.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.ReferredUserId == referredUserId, cancellationToken);
    }

    public async Task<IReadOnlyList<ReferralRelationship>> ListRelationshipsForReferrerAsync(Guid referrerUserId, int take, CancellationToken cancellationToken) =>
        await db.ReferralRelationships.AsNoTracking()
            .Where(item => item.ReferrerUserId == referrerUserId)
            .OrderByDescending(item => item.RegisteredAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> FindPatientDisplayNamesAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var rows = await db.Patients.AsNoTracking()
            .Where(item => userIds.Contains(item.UserId))
            .Select(item => new { item.UserId, item.FullName })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(item => item.UserId, item => item.FullName ?? string.Empty);
    }

    public Task<int> CountReferralRewardsTodayAsync(Guid referrerUserId, DateTimeOffset dayStartUtc, CancellationToken cancellationToken) =>
        db.ReferralRelationships.AsNoTracking()
            .CountAsync(
                item => item.ReferrerUserId == referrerUserId
                    && item.Status == ReferralStatus.Rewarded
                    && item.RewardedAtUtc >= dayStartUtc,
                cancellationToken);

    public void AddReferralRelationship(ReferralRelationship relationship) => db.ReferralRelationships.Add(relationship);

    public Task<RewardAccount?> FindRewardAccountAsync(Guid userId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.RewardAccounts.AsQueryable() : db.RewardAccounts.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
    }

    public void AddRewardAccount(RewardAccount account) => db.RewardAccounts.Add(account);

    public async Task<RewardAccount> GetOrCreateRewardAccountAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await FindRewardAccountAsync(userId, tracking: true, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = RewardAccount.Create(userId, now);
        db.RewardAccounts.Add(created);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return created;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            db.Entry(created).State = EntityState.Detached;
            var recovered = await FindRewardAccountAsync(userId, tracking: true, cancellationToken);
            if (recovered is null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
            }

            return recovered;
        }
    }

    public async Task<ReferralCode> GetOrCreateReferralCodeAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await FindReferralCodeByUserAsync(userId, tracking: true, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = ReferralCode.Create(userId, now);
        db.ReferralCodes.Add(created);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return created;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            db.Entry(created).State = EntityState.Detached;
            var recovered = await FindReferralCodeByUserAsync(userId, tracking: true, cancellationToken);
            if (recovered is null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
            }

            return recovered;
        }
    }

    public async Task<MarketingConsent> GetOrCreateConsentAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await FindConsentAsync(userId, tracking: true, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = MarketingConsent.Create(userId, false, now);
        db.MarketingConsents.Add(created);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return created;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            db.Entry(created).State = EntityState.Detached;
            var recovered = await FindConsentAsync(userId, tracking: true, cancellationToken);
            if (recovered is null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
            }

            return recovered;
        }
    }

    public Task<RewardTransaction?> FindRewardByIdempotencyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        db.RewardTransactions.AsNoTracking().SingleOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<IReadOnlyList<RewardTransaction>> ListRewardHistoryAsync(Guid userId, int take, CancellationToken cancellationToken) =>
        await db.RewardTransactions.AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RewardTransaction>> ListExpirableEarnsAsync(DateTimeOffset now, int take, CancellationToken cancellationToken) =>
        await db.RewardTransactions.AsNoTracking()
            .Where(item => item.EntryType == RewardEntryType.Earn && item.ExpiresAtUtc != null && item.ExpiresAtUtc <= now && item.PointsDelta > 0)
            .OrderBy(item => item.ExpiresAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

    public void AddRewardTransaction(RewardTransaction transaction) => db.RewardTransactions.Add(transaction);

    public Task<Coupon?> FindCouponByCodeAsync(string code, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.Coupons.AsQueryable() : db.Coupons.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Code == code, cancellationToken);
    }

    public Task<Coupon?> FindCouponAsync(Guid couponId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.Coupons.AsQueryable() : db.Coupons.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == couponId, cancellationToken);
    }

    public async Task<IReadOnlyList<Coupon>> ListActiveCouponsAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.Coupons.AsNoTracking()
            .Where(item => item.IsActive && item.StartsAtUtc <= now && item.EndsAtUtc >= now)
            .OrderBy(item => item.Code)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Coupon>> ListAllCouponsAsync(CancellationToken cancellationToken) =>
        await db.Coupons.AsNoTracking().OrderByDescending(item => item.CreatedAtUtc).ToListAsync(cancellationToken);

    public void AddCoupon(Coupon coupon) => db.Coupons.Add(coupon);

    public Task<CouponRedemption?> FindRedemptionByAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken) =>
        db.CouponRedemptions.AsNoTracking().SingleOrDefaultAsync(item => item.AppointmentId == appointmentId, cancellationToken);

    public Task<CouponRedemption?> FindRedemptionByIdempotencyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        db.CouponRedemptions.SingleOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);

    public Task<int> CountUserRedemptionsAsync(Guid couponId, Guid userId, CancellationToken cancellationToken) =>
        db.CouponRedemptions.AsNoTracking().CountAsync(item => item.CouponId == couponId && item.UserId == userId, cancellationToken);

    public Task<int> CountPaidAppointmentsForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Appointments.AsNoTracking()
            .CountAsync(item => item.AccountUserId == userId && item.PaymentStatus == "Paid" && !item.IsWalkIn, cancellationToken);

    public void AddCouponRedemption(CouponRedemption redemption) => db.CouponRedemptions.Add(redemption);

    public Task<MarketingConsent?> FindConsentAsync(Guid userId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.MarketingConsents.AsQueryable() : db.MarketingConsents.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
    }

    public void AddConsent(MarketingConsent consent) => db.MarketingConsents.Add(consent);

    public Task<MarketingCampaign?> FindCampaignAsync(Guid campaignId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.MarketingCampaigns.AsQueryable() : db.MarketingCampaigns.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == campaignId, cancellationToken);
    }

    public Task<MarketingCampaign?> FindActiveCampaignByKindAsync(CampaignKind kind, CancellationToken cancellationToken) =>
        db.MarketingCampaigns.AsNoTracking()
            .Where(item => item.Kind == kind && item.IsActive)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<MarketingCampaign>> ListCampaignsAsync(CancellationToken cancellationToken) =>
        await db.MarketingCampaigns.AsNoTracking().OrderByDescending(item => item.UpdatedAtUtc).ToListAsync(cancellationToken);

    public void AddCampaign(MarketingCampaign campaign) => db.MarketingCampaigns.Add(campaign);

    public Task<bool> HasRecentCampaignDeliveryAsync(Guid campaignId, Guid userId, DateTimeOffset since, CancellationToken cancellationToken) =>
        db.CampaignDeliveries.AsNoTracking()
            .AnyAsync(item => item.CampaignId == campaignId && item.UserId == userId && item.SentAtUtc >= since, cancellationToken);

    public Task<int> CountCampaignDeliveriesAsync(Guid campaignId, CancellationToken cancellationToken) =>
        db.CampaignDeliveries.AsNoTracking().CountAsync(item => item.CampaignId == campaignId, cancellationToken);

    public void AddCampaignDelivery(CampaignDelivery delivery) => db.CampaignDeliveries.Add(delivery);

    public Task<Appointment?> FindAppointmentAsync(Guid appointmentId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.Appointments.AsQueryable() : db.Appointments.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == appointmentId, cancellationToken);
    }

    public Task<bool> HasRecentNotificationAsync(Guid userId, string kind, Guid? subjectId, DateTimeOffset since, CancellationToken cancellationToken) =>
        db.UserNotifications.AsNoTracking()
            .AnyAsync(
                item => item.UserId == userId && item.Kind == kind && item.SubjectId == subjectId && item.CreatedAtUtc >= since,
                cancellationToken);

    public Task NotifyAsync(UserNotification notification, CancellationToken cancellationToken)
    {
        db.UserNotifications.Add(notification);
        return Task.CompletedTask;
    }

    public async Task<string?> FindSettingAsync(string key, CancellationToken cancellationToken)
    {
        var setting = await db.PlatformSettings.AsNoTracking().SingleOrDefaultAsync(item => item.Key == key, cancellationToken);
        return setting?.Value;
    }

    public async Task<GrowthDashboard> BuildGrowthDashboardAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var d30 = now.AddDays(-30);
        var d90 = now.AddDays(-90);

        var newRegs = await db.Users.AsNoTracking().CountAsync(item => item.CreatedAtUtc >= d30, cancellationToken);
        var referralRegs = await db.ReferralRelationships.AsNoTracking().CountAsync(item => item.RegisteredAtUtc >= d30, cancellationToken);
        var referralConverted = await db.ReferralRelationships.AsNoTracking()
            .CountAsync(item => item.Status == ReferralStatus.Converted || item.Status == ReferralStatus.Rewarded, cancellationToken);
        var referralRewarded = await db.ReferralRelationships.AsNoTracking()
            .CountAsync(item => item.Status == ReferralStatus.Rewarded, cancellationToken);
        var referralTotal = await db.ReferralRelationships.AsNoTracking().CountAsync(cancellationToken);
        var conversionRate = referralTotal == 0 ? 0m : decimal.Round((decimal)referralConverted * 100m / referralTotal, 2);

        var couponRows = await db.CouponRedemptions.AsNoTracking()
            .Where(item => item.CreatedAtUtc >= d30)
            .ToListAsync(cancellationToken);
        var couponsRedeemed = couponRows.Count;
        var couponDiscount = couponRows.Sum(item => item.DiscountAmount);

        var outstandingPoints = await db.RewardAccounts.AsNoTracking().SumAsync(item => (int?)item.PointsBalance, cancellationToken) ?? 0;
        var outstandingCredits = await db.RewardAccounts.AsNoTracking().SumAsync(item => (decimal?)item.CreditBalanceInr, cancellationToken) ?? 0m;

        var patientBookingGroups = await db.Appointments.AsNoTracking()
            .Where(item => !item.IsWalkIn && item.CreatedAtUtc >= d30 && item.Status != AppointmentStatus.Cancelled)
            .GroupBy(item => item.AccountUserId)
            .Select(group => new { UserId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var repeatBookers = patientBookingGroups.Count(item => item.Count >= 2);

        var patients90 = await db.Appointments.AsNoTracking()
            .Where(item => !item.IsWalkIn && item.CreatedAtUtc >= d90 && item.Status != AppointmentStatus.Cancelled)
            .Select(item => item.AccountUserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var priorPatients = await db.Appointments.AsNoTracking()
            .Where(item => !item.IsWalkIn && item.CreatedAtUtc < d90 && patients90.Contains(item.AccountUserId))
            .Select(item => item.AccountUserId)
            .Distinct()
            .CountAsync(cancellationToken);
        var retention = patients90.Count == 0 ? 0m : decimal.Round((decimal)priorPatients * 100m / patients90.Count, 2);

        var campaignDeliveries = await db.CampaignDeliveries.AsNoTracking().CountAsync(item => item.SentAtUtc >= d30, cancellationToken);

        var registrationDates = await db.Users.AsNoTracking()
            .Where(item => item.CreatedAtUtc >= d30)
            .Select(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var registrationTrend = registrationDates
            .GroupBy(item => item.UtcDateTime.Date)
            .OrderBy(group => group.Key)
            .Select(group => new GrowthSeriesPoint(group.Key.ToString("yyyy-MM-dd"), group.Count(), 0m))
            .ToArray();

        var referralConvertedAt = await db.ReferralRelationships.AsNoTracking()
            .Where(item => item.ConvertedAtUtc != null && item.ConvertedAtUtc >= d30)
            .Select(item => item.ConvertedAtUtc!.Value)
            .ToListAsync(cancellationToken);
        var referralTrend = referralConvertedAt
            .GroupBy(item => item.UtcDateTime.Date)
            .OrderBy(group => group.Key)
            .Select(group => new GrowthSeriesPoint(group.Key.ToString("yyyy-MM-dd"), group.Count(), 0m))
            .ToArray();

        var couponUsage = await (
            from redemption in db.CouponRedemptions.AsNoTracking()
            join coupon in db.Coupons.AsNoTracking() on redemption.CouponId equals coupon.Id
            where redemption.CreatedAtUtc >= d30
            select new { coupon.Code, coupon.Name, redemption.DiscountAmount }
            ).ToListAsync(cancellationToken);
        var topCoupons = couponUsage
            .GroupBy(item => new { item.Code, item.Name })
            .Select(group => new CouponUsageRow(group.Key.Code, group.Key.Name, group.Count(), group.Sum(item => item.DiscountAmount)))
            .OrderByDescending(item => item.Redemptions)
            .Take(10)
            .ToArray();

        return new GrowthDashboard(
            newRegs,
            referralRegs,
            referralConverted,
            referralRewarded,
            conversionRate,
            couponsRedeemed,
            couponDiscount,
            outstandingPoints,
            outstandingCredits,
            repeatBookers,
            priorPatients,
            retention,
            campaignDeliveries,
            registrationTrend,
            referralTrend,
            topCoupons);
    }

    public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(audit);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sql && sql.Number is 2601 or 2627;
}
