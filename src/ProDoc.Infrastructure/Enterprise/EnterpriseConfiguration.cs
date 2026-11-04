using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProDoc.Domain.Enterprise;

namespace ProDoc.Infrastructure.Enterprise;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Kind).HasConversion<int>();
        builder.HasIndex(x => x.CreatedByUserId);
        builder.HasIndex(x => new { x.IsActive, x.Name });
    }
}

public sealed class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
    {
        builder.ToTable("OrganizationMemberships");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Role).HasConversion<int>();
        builder.HasIndex(x => new { x.OrganizationId, x.UserId }).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.IsActive });
        builder.HasIndex(x => new { x.OrganizationId, x.ClinicId, x.BranchId });
    }
}

public sealed class OrganizationSettingConfiguration : IEntityTypeConfiguration<OrganizationSetting>
{
    public void Configure(EntityTypeBuilder<OrganizationSetting> builder)
    {
        builder.ToTable("OrganizationSettings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(2000).IsRequired();
        builder.HasIndex(x => new { x.OrganizationId, x.Key }).IsUnique();
    }
}

public sealed class BranchSettingOverrideConfiguration : IEntityTypeConfiguration<BranchSettingOverride>
{
    public void Configure(EntityTypeBuilder<BranchSettingOverride> builder)
    {
        builder.ToTable("BranchSettingOverrides");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(2000).IsRequired();
        builder.HasIndex(x => new { x.BranchId, x.Key }).IsUnique();
        builder.HasIndex(x => x.OrganizationId);
    }
}

public sealed class ConsultationResourceConfiguration : IEntityTypeConfiguration<ConsultationResource>
{
    public void Configure(EntityTypeBuilder<ConsultationResource> builder)
    {
        builder.ToTable("ConsultationResources");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Kind).HasConversion<int>();
        builder.HasIndex(x => new { x.OrganizationId, x.BranchId, x.IsActive });
    }
}

public sealed class ResourceAllocationConfiguration : IEntityTypeConfiguration<ResourceAllocation>
{
    public void Configure(EntityTypeBuilder<ResourceAllocation> builder)
    {
        builder.ToTable("ResourceAllocations");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ResourceId, x.StartsAtUtc, x.EndsAtUtc });
        builder.HasIndex(x => x.OrganizationId);
    }
}

public sealed class OrganizationSubscriptionConfiguration : IEntityTypeConfiguration<OrganizationSubscription>
{
    public void Configure(EntityTypeBuilder<OrganizationSubscription> builder)
    {
        builder.ToTable("OrganizationSubscriptions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PlanCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.BillingEmail).HasMaxLength(160);
        builder.Property(x => x.PendingPlanCode).HasMaxLength(40);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => x.OrganizationId).IsUnique();
        builder.HasIndex(x => x.PlanId);
        builder.HasIndex(x => x.Status);
    }
}
