using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProDoc.Domain.Support;

namespace ProDoc.Infrastructure.Support;

public sealed class HelpArticleCategoryConfiguration : IEntityTypeConfiguration<HelpArticleCategory>
{
    public void Configure(EntityTypeBuilder<HelpArticleCategory> builder)
    {
        builder.ToTable("HelpArticleCategories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(40).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(120).IsRequired();
        builder.Property(x => x.NameHi).HasMaxLength(120).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public sealed class HelpArticleConfiguration : IEntityTypeConfiguration<HelpArticle>
{
    public void Configure(EntityTypeBuilder<HelpArticle> builder)
    {
        builder.ToTable("HelpArticles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Slug).HasMaxLength(80).IsRequired();
        builder.Property(x => x.TitleEn).HasMaxLength(160).IsRequired();
        builder.Property(x => x.TitleHi).HasMaxLength(160).IsRequired();
        builder.Property(x => x.BodyEn).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.BodyHi).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.Tags).HasMaxLength(240).IsRequired();
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasIndex(x => new { x.CategoryId, x.IsPublished });
    }
}

public sealed class SupportAttachmentConfiguration : IEntityTypeConfiguration<SupportAttachment>
{
    public void Configure(EntityTypeBuilder<SupportAttachment> builder)
    {
        builder.ToTable("SupportAttachments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FileName).HasMaxLength(160).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.SupportTicketId);
        builder.HasIndex(x => x.StorageKey).IsUnique();
    }
}

public sealed class TicketStatusHistoryConfiguration : IEntityTypeConfiguration<TicketStatusHistory>
{
    public void Configure(EntityTypeBuilder<TicketStatusHistory> builder)
    {
        builder.ToTable("TicketStatusHistory");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FromStatus).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ToStatus).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(400);
        builder.HasIndex(x => x.SupportTicketId);
    }
}

public sealed class DisputeCaseConfiguration : IEntityTypeConfiguration<DisputeCase>
{
    public void Configure(EntityTypeBuilder<DisputeCase> builder)
    {
        builder.ToTable("DisputeCases");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Number).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Kind).HasConversion<int>();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Decision).HasConversion<int>();
        builder.Property(x => x.Summary).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.EvidenceRequest).HasMaxLength(2000);
        builder.Property(x => x.DecisionNotes).HasMaxLength(2000);
        builder.HasIndex(x => x.Number).IsUnique();
        builder.HasIndex(x => x.SupportTicketId).IsUnique();
        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc });
    }
}
