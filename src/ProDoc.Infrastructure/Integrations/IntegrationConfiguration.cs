using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProDoc.Domain.Integrations;

namespace ProDoc.Infrastructure.Integrations;

public sealed class IntegrationProviderConfiguration : IEntityTypeConfiguration<IntegrationProvider>
{
    public void Configure(EntityTypeBuilder<IntegrationProvider> builder)
    {
        builder.ToTable("IntegrationProviders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(40).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Kind).HasConversion<int>();
        builder.Property(x => x.Mode).HasConversion<int>();
        builder.Property(x => x.BaseUrl).HasMaxLength(400);
        builder.Property(x => x.CredentialReference).HasMaxLength(200);
        builder.Property(x => x.ContractVersion).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public sealed class IntegrationConsentConfiguration : IEntityTypeConfiguration<IntegrationConsent>
{
    public void Configure(EntityTypeBuilder<IntegrationConsent> builder)
    {
        builder.ToTable("IntegrationConsents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProviderKind).HasConversion<int>();
        builder.Property(x => x.Purpose).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ScopeJson).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.HasIndex(x => new { x.UserId, x.ProviderKind, x.Purpose }).IsUnique();
    }
}

public sealed class IntegrationDeliveryLogConfiguration : IEntityTypeConfiguration<IntegrationDeliveryLog>
{
    public void Configure(EntityTypeBuilder<IntegrationDeliveryLog> builder)
    {
        builder.ToTable("IntegrationDeliveryLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProviderKind).HasConversion<int>();
        builder.Property(x => x.Operation).HasMaxLength(80).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.ReferenceType).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ExternalReference).HasMaxLength(120);
        builder.Property(x => x.ErrorCode).HasMaxLength(80);
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => new { x.ProviderKind, x.CreatedAtUtc });
    }
}

public sealed class IntegrationWebhookEventConfiguration : IEntityTypeConfiguration<IntegrationWebhookEvent>
{
    public void Configure(EntityTypeBuilder<IntegrationWebhookEvent> builder)
    {
        builder.ToTable("IntegrationWebhookEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProviderKind).HasConversion<int>();
        builder.Property(x => x.ProviderCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.EventId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(80).IsRequired();
        builder.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.ProviderCode, x.EventId }).IsUnique();
    }
}

public sealed class LabOrderConfiguration : IEntityTypeConfiguration<LabOrder>
{
    public void Configure(EntityTypeBuilder<LabOrder> builder)
    {
        builder.ToTable("LabOrders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProviderCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.CatalogCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.TestName).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.ExternalOrderId).HasMaxLength(120);
        builder.HasIndex(x => x.PatientUserId);
        builder.HasIndex(x => new { x.ProviderCode, x.ExternalOrderId });
    }
}

public sealed class LabReportConfiguration : IEntityTypeConfiguration<LabReport>
{
    public void Configure(EntityTypeBuilder<LabReport> builder)
    {
        builder.ToTable("LabReports");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Summary).HasMaxLength(400).IsRequired();
        builder.Property(x => x.ResultJson).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.Disclaimer).HasMaxLength(400).IsRequired();
        builder.HasIndex(x => x.PatientUserId);
        builder.HasIndex(x => x.LabOrderId);
    }
}

public sealed class PharmacyShareConfiguration : IEntityTypeConfiguration<PharmacyShare>
{
    public void Configure(EntityTypeBuilder<PharmacyShare> builder)
    {
        builder.ToTable("PharmacyShares");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProviderCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.PharmacyName).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.ExternalShareId).HasMaxLength(120);
        builder.HasIndex(x => x.PatientUserId);
        builder.HasIndex(x => new { x.ProviderCode, x.ExternalShareId });
    }
}

public sealed class AbhaLinkConfiguration : IEntityTypeConfiguration<AbhaLink>
{
    public void Configure(EntityTypeBuilder<AbhaLink> builder)
    {
        builder.ToTable("AbhaLinks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.SandboxHandle).HasMaxLength(120);
        builder.Property(x => x.Notes).HasMaxLength(400).IsRequired();
        builder.HasIndex(x => x.UserId).IsUnique();
    }
}
