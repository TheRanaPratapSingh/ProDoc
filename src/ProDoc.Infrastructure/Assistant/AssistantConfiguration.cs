using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProDoc.Domain.Assistant;

namespace ProDoc.Infrastructure.Assistant;

public sealed class AssistantConversationConfiguration : IEntityTypeConfiguration<AssistantConversation>
{
    public void Configure(EntityTypeBuilder<AssistantConversation> builder)
    {
        builder.ToTable("AssistantConversations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Language).HasMaxLength(8).IsRequired();
        builder.Property(x => x.PromptVersion).HasMaxLength(40).IsRequired();
        builder.Property(x => x.PendingActionJson).HasMaxLength(4000);
        builder.HasIndex(x => new { x.UserId, x.UpdatedAtUtc });
        builder.HasIndex(x => x.DeletedAtUtc);
        builder.HasMany(x => x.Messages).WithOne().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Messages).HasField("_messages").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class AssistantMessageConfiguration : IEntityTypeConfiguration<AssistantMessage>
{
    public void Configure(EntityTypeBuilder<AssistantMessage> builder)
    {
        builder.ToTable("AssistantMessages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Role).HasConversion<int>();
        builder.Property(x => x.Content).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.ToolName).HasMaxLength(80);
        builder.Property(x => x.SourcesJson).HasMaxLength(2000);
        builder.HasIndex(x => new { x.ConversationId, x.CreatedAtUtc });
    }
}

public sealed class AssistantConsentConfiguration : IEntityTypeConfiguration<AssistantConsent>
{
    public void Configure(EntityTypeBuilder<AssistantConsent> builder)
    {
        builder.ToTable("AssistantConsents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ConsentVersion).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.UserId).IsUnique();
    }
}

public sealed class AssistantUsageDailyConfiguration : IEntityTypeConfiguration<AssistantUsageDaily>
{
    public void Configure(EntityTypeBuilder<AssistantUsageDaily> builder)
    {
        builder.ToTable("AssistantUsageDaily");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.UserId, x.UsageDate }).IsUnique();
    }
}
