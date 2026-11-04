using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProDoc.Domain.Billing;

namespace ProDoc.Infrastructure.Billing;

public sealed class ClinicPaymentSettingsConfiguration : IEntityTypeConfiguration<ClinicPaymentSettings>
{
    public void Configure(EntityTypeBuilder<ClinicPaymentSettings> builder)
    {
        builder.ToTable("ClinicPaymentSettings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Policy).HasConversion<int>();
        builder.Property(x => x.AdvancePercent).HasPrecision(5, 2);
        builder.HasIndex(x => x.ClinicId).IsUnique();
        builder.HasOne<Domain.Providers.Clinic>().WithMany().HasForeignKey(x => x.ClinicId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TaxRuleConfiguration : IEntityTypeConfiguration<TaxRule>
{
    public void Configure(EntityTypeBuilder<TaxRule> builder)
    {
        builder.ToTable("TaxRules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(80).IsRequired();
        builder.Property(x => x.RatePercent).HasPrecision(5, 2);
        builder.HasIndex(x => x.Code);
    }
}

public sealed class CommissionRuleConfiguration : IEntityTypeConfiguration<CommissionRule>
{
    public void Configure(EntityTypeBuilder<CommissionRule> builder)
    {
        builder.ToTable("CommissionRules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Basis).HasConversion<int>();
        builder.Property(x => x.Rate).HasPrecision(10, 2);
        builder.Property(x => x.MinimumAmount).HasPrecision(10, 2);
        builder.Property(x => x.MaximumAmount).HasPrecision(10, 2);
        builder.HasIndex(x => new { x.IsActive, x.Priority });
    }
}

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Number).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Channel).HasConversion<int>();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Amount).HasPrecision(10, 2);
        builder.Property(x => x.ConsultationFeeSnapshot).HasPrecision(10, 2);
        builder.Property(x => x.GatewayFeeEstimated).HasPrecision(10, 2);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.Provider).HasMaxLength(40);
        builder.Property(x => x.ProviderOrderId).HasMaxLength(80);
        builder.Property(x => x.ProviderPaymentId).HasMaxLength(80);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(80).IsRequired();
        builder.Property(x => x.CheckoutPayload).HasMaxLength(2000);
        builder.Property(x => x.FailureReason).HasMaxLength(240);
        builder.HasIndex(x => x.Number).IsUnique();
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => x.AppointmentId);
        builder.HasIndex(x => x.ProviderOrderId);
    }
}

public sealed class PaymentWebhookEventConfiguration : IEntityTypeConfiguration<PaymentWebhookEvent>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookEvent> builder)
    {
        builder.ToTable("PaymentWebhookEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).HasMaxLength(40).IsRequired();
        builder.Property(x => x.EventId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(80).IsRequired();
        builder.HasIndex(x => new { x.Provider, x.EventId }).IsUnique();
    }
}

public sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("Refunds");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Number).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Amount).HasPrecision(10, 2);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.Reason).HasMaxLength(240);
        builder.Property(x => x.ProviderRefundId).HasMaxLength(80);
        builder.HasIndex(x => x.Number).IsUnique();
        builder.HasIndex(x => x.PaymentId);
    }
}

public sealed class CommissionSnapshotConfiguration : IEntityTypeConfiguration<CommissionSnapshot>
{
    public void Configure(EntityTypeBuilder<CommissionSnapshot> builder)
    {
        builder.ToTable("CommissionSnapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RuleNameSnapshot).HasMaxLength(120);
        builder.Property(x => x.BasisSnapshot).HasConversion<int>();
        builder.Property(x => x.RateSnapshot).HasPrecision(10, 2);
        builder.Property(x => x.ConsultationFee).HasPrecision(10, 2);
        builder.Property(x => x.CommissionAmount).HasPrecision(10, 2);
        builder.Property(x => x.ClinicPayableAmount).HasPrecision(10, 2);
        builder.Property(x => x.GatewayFeeSnapshot).HasPrecision(10, 2);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.HasIndex(x => x.AppointmentId).IsUnique();
        builder.HasIndex(x => new { x.ClinicId, x.CapturedAtUtc });
        builder.HasIndex(x => x.SettlementBatchId);
    }
}

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Number).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Kind).HasConversion<int>();
        builder.Property(x => x.BillToName).HasMaxLength(160);
        builder.Property(x => x.BillToDetail).HasMaxLength(320);
        builder.Property(x => x.Description).HasMaxLength(320);
        builder.Property(x => x.Subtotal).HasPrecision(10, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(10, 2);
        builder.Property(x => x.Total).HasPrecision(10, 2);
        builder.Property(x => x.AmountPaid).HasPrecision(10, 2);
        builder.Property(x => x.Outstanding).HasPrecision(10, 2);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.TaxCodeSnapshot).HasMaxLength(32);
        builder.Property(x => x.TaxRateSnapshot).HasPrecision(5, 2);
        builder.Property(x => x.Status).HasMaxLength(24);
        builder.Property(x => x.PdfStorageKey).HasMaxLength(64);
        builder.HasIndex(x => x.Number).IsUnique();
        builder.HasIndex(x => x.AppointmentId);
        builder.HasIndex(x => x.ClinicId);
    }
}

public sealed class SettlementBatchConfiguration : IEntityTypeConfiguration<SettlementBatch>
{
    public void Configure(EntityTypeBuilder<SettlementBatch> builder)
    {
        builder.ToTable("SettlementBatches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Number).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.GrossConsultationFees).HasPrecision(12, 2);
        builder.Property(x => x.TotalCommission).HasPrecision(12, 2);
        builder.Property(x => x.TotalGatewayFees).HasPrecision(12, 2);
        builder.Property(x => x.NetPayable).HasPrecision(12, 2);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.HasIndex(x => x.Number).IsUnique();
        builder.HasIndex(x => new { x.ClinicId, x.PeriodFrom, x.PeriodTo });
        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.SettlementBatchId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class SettlementLineConfiguration : IEntityTypeConfiguration<SettlementLine>
{
    public void Configure(EntityTypeBuilder<SettlementLine> builder)
    {
        builder.ToTable("SettlementLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ConsultationFee).HasPrecision(10, 2);
        builder.Property(x => x.CommissionAmount).HasPrecision(10, 2);
        builder.Property(x => x.GatewayFee).HasPrecision(10, 2);
        builder.Property(x => x.ClinicPayable).HasPrecision(10, 2);
        builder.HasIndex(x => x.SettlementBatchId);
        builder.HasIndex(x => x.CommissionSnapshotId).IsUnique();
    }
}
