using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProDoc.Domain.Admin;

namespace ProDoc.Infrastructure.Admin;

public sealed class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> builder)
    {
        builder.ToTable("SupportTickets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Number).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.ResolutionDetails).HasMaxLength(2000);
        builder.Property(x => x.Category).HasConversion<int>();
        builder.Property(x => x.Priority).HasConversion<int>();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.HasIndex(x => x.Number).IsUnique();
        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc });
        builder.HasIndex(x => x.CreatedByUserId);
        builder.HasIndex(x => new { x.AssignedToUserId, x.Status });
        builder.HasIndex(x => x.DueAtUtc);
        builder.HasMany(x => x.Notes).WithOne().HasForeignKey(x => x.SupportTicketId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Notes).HasField("_notes").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class SupportTicketNoteConfiguration : IEntityTypeConfiguration<SupportTicketNote>
{
    public void Configure(EntityTypeBuilder<SupportTicketNote> builder)
    {
        builder.ToTable("SupportTicketNotes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        builder.HasIndex(x => x.SupportTicketId);
    }
}

public sealed class PlatformSettingConfiguration : IEntityTypeConfiguration<PlatformSetting>
{
    public void Configure(EntityTypeBuilder<PlatformSetting> builder)
    {
        builder.ToTable("PlatformSettings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(240);
        builder.HasIndex(x => x.Key).IsUnique();
    }
}

public sealed class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> builder)
    {
        builder.ToTable("FeatureFlags");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(240);
        builder.HasIndex(x => x.Key).IsUnique();
    }
}
