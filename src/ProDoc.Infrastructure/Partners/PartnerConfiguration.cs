using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProDoc.Domain.Partners;

namespace ProDoc.Infrastructure.Partners;

public sealed class PartnerOrganizationConfiguration : IEntityTypeConfiguration<PartnerOrganization>
{
    public void Configure(EntityTypeBuilder<PartnerOrganization> builder)
    {
        builder.ToTable("PartnerOrganizations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(160).IsRequired();
        builder.Property(x => x.ContactEmail).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Website).HasMaxLength(240);
        builder.Property(x => x.ReviewNote).HasMaxLength(1000);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.HasIndex(x => x.OwnerUserId).IsUnique();
        builder.HasIndex(x => x.Status);
    }
}

public sealed class PartnerAppConfiguration : IEntityTypeConfiguration<PartnerApp>
{
    public void Configure(EntityTypeBuilder<PartnerApp> builder)
    {
        builder.ToTable("PartnerApps");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ClientId).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ScopesCsv).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Environment).HasConversion<int>();
        builder.HasIndex(x => x.ClientId).IsUnique();
        builder.HasIndex(x => x.PartnerOrganizationId);
    }
}

public sealed class PartnerCredentialConfiguration : IEntityTypeConfiguration<PartnerCredential>
{
    public void Configure(EntityTypeBuilder<PartnerCredential> builder)
    {
        builder.ToTable("PartnerCredentials");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SecretHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.SecretPrefix).HasMaxLength(16).IsRequired();
        builder.HasIndex(x => x.PartnerAppId);
        builder.HasIndex(x => x.SecretHash);
    }
}

public sealed class PartnerWebhookEndpointConfiguration : IEntityTypeConfiguration<PartnerWebhookEndpoint>
{
    public void Configure(EntityTypeBuilder<PartnerWebhookEndpoint> builder)
    {
        builder.ToTable("PartnerWebhookEndpoints");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TargetUrl).HasMaxLength(500).IsRequired();
        builder.Property(x => x.SigningSecret).HasMaxLength(120).IsRequired();
        builder.Property(x => x.SecretPrefix).HasMaxLength(16).IsRequired();
        builder.Property(x => x.EventTypesCsv).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => x.PartnerAppId);
        builder.HasIndex(x => x.IsActive);
    }
}

public sealed class PartnerWebhookDeliveryConfiguration : IEntityTypeConfiguration<PartnerWebhookDelivery>
{
    public void Configure(EntityTypeBuilder<PartnerWebhookDelivery> builder)
    {
        builder.ToTable("PartnerWebhookDeliveries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).HasMaxLength(80).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(160).IsRequired();
        builder.Property(x => x.PayloadJson).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(1000);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => new { x.Status, x.AvailableAtUtc });
        builder.HasIndex(x => x.PartnerAppId);
    }
}

public sealed class PartnerApiUsageDailyConfiguration : IEntityTypeConfiguration<PartnerApiUsageDaily>
{
    public void Configure(EntityTypeBuilder<PartnerApiUsageDaily> builder)
    {
        builder.ToTable("PartnerApiUsageDaily");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.PartnerAppId, x.Day }).IsUnique();
    }
}

public sealed class PartnerBookingConfiguration : IEntityTypeConfiguration<PartnerBooking>
{
    public void Configure(EntityTypeBuilder<PartnerBooking> builder)
    {
        builder.ToTable("PartnerBookings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalPatientRef).HasMaxLength(80).IsRequired();
        builder.Property(x => x.PatientDisplayName).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(120);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.HasIndex(x => x.PartnerAppId);
        builder.HasIndex(x => new { x.PartnerAppId, x.IdempotencyKey });
    }
}
