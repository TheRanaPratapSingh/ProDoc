using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProDoc.Domain.Reviews;

namespace ProDoc.Infrastructure.Reviews;

public sealed class PatientReviewConfiguration : IEntityTypeConfiguration<PatientReview>
{
    public void Configure(EntityTypeBuilder<PatientReview> builder)
    {
        builder.ToTable("PatientReviews");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TargetKind).HasConversion<int>();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Body).HasMaxLength(ReviewPolicy.MaxBodyLength);
        builder.HasIndex(x => new { x.AppointmentId, x.TargetKind }).IsUnique();
        builder.HasIndex(x => new { x.DoctorId, x.Status, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.ClinicId, x.Status, x.CreatedAtUtc });
        builder.HasIndex(x => x.AuthorUserId);
        builder.HasMany(x => x.Responses).WithOne().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Reports).WithOne().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.ModerationEvents).WithOne().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Responses).HasField("_responses").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Reports).HasField("_reports").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.ModerationEvents).HasField("_moderationEvents").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ReviewResponseConfiguration : IEntityTypeConfiguration<ReviewResponse>
{
    public void Configure(EntityTypeBuilder<ReviewResponse> builder)
    {
        builder.ToTable("ReviewResponses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ResponderKind).HasConversion<int>();
        builder.Property(x => x.Body).HasMaxLength(ReviewPolicy.MaxResponseLength).IsRequired();
        builder.HasIndex(x => new { x.ReviewId, x.ResponderKind }).IsUnique();
    }
}

public sealed class ReviewReportConfiguration : IEntityTypeConfiguration<ReviewReport>
{
    public void Configure(EntityTypeBuilder<ReviewReport> builder)
    {
        builder.ToTable("ReviewReports");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ResolutionNote).HasMaxLength(500);
        builder.HasIndex(x => new { x.ReviewId, x.ReporterUserId, x.ResolvedAtUtc });
    }
}

public sealed class ReviewModerationEventConfiguration : IEntityTypeConfiguration<ReviewModerationEvent>
{
    public void Configure(EntityTypeBuilder<ReviewModerationEvent> builder)
    {
        builder.ToTable("ReviewModerationEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FromStatus).HasConversion<int>();
        builder.Property(x => x.ToStatus).HasConversion<int>();
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.ReviewId, x.CreatedAtUtc });
    }
}

public sealed class FeedbackSubmissionConfiguration : IEntityTypeConfiguration<FeedbackSubmission>
{
    public void Configure(EntityTypeBuilder<FeedbackSubmission> builder)
    {
        builder.ToTable("FeedbackSubmissions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Category).HasConversion<int>();
        builder.Property(x => x.Body).HasMaxLength(2000).IsRequired();
        builder.HasIndex(x => new { x.AuthorUserId, x.CreatedAtUtc });
    }
}
