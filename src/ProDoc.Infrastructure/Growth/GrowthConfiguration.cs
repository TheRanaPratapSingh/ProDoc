using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProDoc.Domain.Growth;

namespace ProDoc.Infrastructure.Growth;

public sealed class ReferralCodeConfiguration : IEntityTypeConfiguration<ReferralCode>
{
    public void Configure(EntityTypeBuilder<ReferralCode> builder)
    {
        builder.ToTable("ReferralCodes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(16).IsRequired();
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public sealed class ReferralRelationshipConfiguration : IEntityTypeConfiguration<ReferralRelationship>
{
    public void Configure(EntityTypeBuilder<ReferralRelationship> builder)
    {
        builder.ToTable("ReferralRelationships");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CodeUsed).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.HasIndex(x => x.ReferredUserId).IsUnique();
        builder.HasIndex(x => new { x.ReferrerUserId, x.Status, x.RegisteredAtUtc });
        builder.HasIndex(x => new { x.ReferrerUserId, x.RewardedAtUtc });
    }
}

public sealed class RewardAccountConfiguration : IEntityTypeConfiguration<RewardAccount>
{
    public void Configure(EntityTypeBuilder<RewardAccount> builder)
    {
        builder.ToTable("RewardAccounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CreditBalanceInr).HasPrecision(18, 2);
        builder.HasIndex(x => x.UserId).IsUnique();
    }
}

public sealed class RewardTransactionConfiguration : IEntityTypeConfiguration<RewardTransaction>
{
    public void Configure(EntityTypeBuilder<RewardTransaction> builder)
    {
        builder.ToTable("RewardTransactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EntryType).HasConversion<int>();
        builder.Property(x => x.CreditDeltaInr).HasPrecision(18, 2);
        builder.Property(x => x.Reason).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ReferenceType).HasMaxLength(40);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(120).IsRequired();
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.ExpiresAtUtc, x.EntryType });
    }
}

public sealed class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("Coupons");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.DiscountType).HasConversion<int>();
        builder.Property(x => x.DiscountValue).HasPrecision(18, 2);
        builder.Property(x => x.MinBookingAmount).HasPrecision(18, 2);
        builder.Property(x => x.MaxDiscountAmount).HasPrecision(18, 2);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new { x.IsActive, x.EndsAtUtc });
    }
}

public sealed class CouponRedemptionConfiguration : IEntityTypeConfiguration<CouponRedemption>
{
    public void Configure(EntityTypeBuilder<CouponRedemption> builder)
    {
        builder.ToTable("CouponRedemptions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(120).IsRequired();
        builder.HasIndex(x => x.AppointmentId).IsUnique();
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => new { x.CouponId, x.UserId });
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}

public sealed class MarketingConsentConfiguration : IEntityTypeConfiguration<MarketingConsent>
{
    public void Configure(EntityTypeBuilder<MarketingConsent> builder)
    {
        builder.ToTable("MarketingConsents");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.UserId).IsUnique();
    }
}

public sealed class MarketingCampaignConfiguration : IEntityTypeConfiguration<MarketingCampaign>
{
    public void Configure(EntityTypeBuilder<MarketingCampaign> builder)
    {
        builder.ToTable("MarketingCampaigns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Kind).HasConversion<int>();
        builder.Property(x => x.Title).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(280).IsRequired();
        builder.HasIndex(x => new { x.Kind, x.IsActive });
    }
}

public sealed class CampaignDeliveryConfiguration : IEntityTypeConfiguration<CampaignDelivery>
{
    public void Configure(EntityTypeBuilder<CampaignDelivery> builder)
    {
        builder.ToTable("CampaignDeliveries");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.CampaignId, x.UserId, x.SentAtUtc });
        builder.HasIndex(x => x.SentAtUtc);
    }
}
