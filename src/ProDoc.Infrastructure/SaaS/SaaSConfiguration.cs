using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProDoc.Domain.SaaS;

namespace ProDoc.Infrastructure.SaaS;

public sealed class SaaSPlanConfiguration : IEntityTypeConfiguration<SaaSPlan>
{
    public void Configure(EntityTypeBuilder<SaaSPlan> builder)
    {
        builder.ToTable("SaaSPlans");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(8).IsRequired();
        builder.Property(x => x.MonthlyPrice).HasPrecision(18, 2);
        builder.Property(x => x.AnnualPrice).HasPrecision(18, 2);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new { x.IsActive, x.IsPublic, x.SortOrder });
        builder.HasMany(x => x.Entitlements).WithOne().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Entitlements).HasField("_entitlements").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class SaaSPlanEntitlementConfiguration : IEntityTypeConfiguration<SaaSPlanEntitlement>
{
    public void Configure(EntityTypeBuilder<SaaSPlanEntitlement> builder)
    {
        builder.ToTable("SaaSPlanEntitlements");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FeatureCode).HasMaxLength(80).IsRequired();
        builder.HasIndex(x => new { x.PlanId, x.FeatureCode }).IsUnique();
    }
}

public sealed class OrganizationBrandingConfiguration : IEntityTypeConfiguration<OrganizationBranding>
{
    public void Configure(EntityTypeBuilder<OrganizationBranding> builder)
    {
        builder.ToTable("OrganizationBrandings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LogoStorageKey).HasMaxLength(64);
        builder.Property(x => x.PrimaryColorHex).HasMaxLength(7).IsRequired();
        builder.Property(x => x.AccentColorHex).HasMaxLength(7).IsRequired();
        builder.Property(x => x.LoginHeadline).HasMaxLength(120);
        builder.Property(x => x.LoginSubheadline).HasMaxLength(200);
        builder.Property(x => x.EmailFromName).HasMaxLength(120);
        builder.Property(x => x.EmailFooterText).HasMaxLength(500);
        builder.Property(x => x.NotificationAccentHex).HasMaxLength(7);
        builder.Property(x => x.CustomDomain).HasMaxLength(200);
        builder.Property(x => x.SubdomainSlug).HasMaxLength(63);
        builder.HasIndex(x => x.OrganizationId).IsUnique();
        builder.HasIndex(x => x.CustomDomain).IsUnique().HasFilter("[CustomDomain] IS NOT NULL");
        builder.HasIndex(x => x.SubdomainSlug).IsUnique().HasFilter("[SubdomainSlug] IS NOT NULL");
    }
}

public sealed class OrganizationFeatureOverrideConfiguration : IEntityTypeConfiguration<OrganizationFeatureOverride>
{
    public void Configure(EntityTypeBuilder<OrganizationFeatureOverride> builder)
    {
        builder.ToTable("OrganizationFeatureOverrides");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FeatureCode).HasMaxLength(80).IsRequired();
        builder.HasIndex(x => new { x.OrganizationId, x.FeatureCode }).IsUnique();
    }
}

public sealed class SaaSSubscriptionChargeConfiguration : IEntityTypeConfiguration<SaaSSubscriptionCharge>
{
    public void Configure(EntityTypeBuilder<SaaSSubscriptionCharge> builder)
    {
        builder.ToTable("SaaSSubscriptionCharges");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.PlanCodeSnapshot).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Provider).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ProviderOrderId).HasMaxLength(120);
        builder.Property(x => x.ProviderPaymentId).HasMaxLength(120);
        builder.Property(x => x.CheckoutPayload).HasMaxLength(4000);
        builder.Property(x => x.FailureReason).HasMaxLength(500);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Interval).HasConversion<int>();
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => new { x.OrganizationId, x.CreatedAtUtc });
        builder.HasIndex(x => x.InvoiceId);
    }
}
