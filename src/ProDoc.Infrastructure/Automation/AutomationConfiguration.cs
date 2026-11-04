using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProDoc.Domain.Automation;

namespace ProDoc.Infrastructure.Automation;

public sealed class AutomationWorkflowConfiguration : IEntityTypeConfiguration<AutomationWorkflow>
{
    public void Configure(EntityTypeBuilder<AutomationWorkflow> builder)
    {
        builder.ToTable("AutomationWorkflows");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.TriggerType).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ConditionsJson).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.ActionsJson).HasMaxLength(16000).IsRequired();
        builder.HasIndex(x => new { x.TriggerType, x.IsEnabled });
        builder.HasIndex(x => x.OrganizationId);
    }
}

public sealed class AutomationOutboxEventConfiguration : IEntityTypeConfiguration<AutomationOutboxEvent>
{
    public void Configure(EntityTypeBuilder<AutomationOutboxEvent> builder)
    {
        builder.ToTable("AutomationOutboxEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).HasMaxLength(80).IsRequired();
        builder.Property(x => x.PayloadJson).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(160).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(1000);
        builder.Property(x => x.DeadLetterReason).HasMaxLength(1000);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => new { x.Status, x.AvailableAtUtc });
        builder.HasIndex(x => x.SubjectId);
    }
}

public sealed class AutomationWorkflowRunConfiguration : IEntityTypeConfiguration<AutomationWorkflowRun>
{
    public void Configure(EntityTypeBuilder<AutomationWorkflowRun> builder)
    {
        builder.ToTable("AutomationWorkflowRuns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TriggerType).HasMaxLength(80).IsRequired();
        builder.Property(x => x.DetailJson).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.FailureDetails).HasMaxLength(2000);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.HasIndex(x => new { x.WorkflowId, x.StartedAtUtc });
        builder.HasIndex(x => x.OutboxEventId);
    }
}

public sealed class UserNotificationPreferenceConfiguration : IEntityTypeConfiguration<UserNotificationPreference>
{
    public void Configure(EntityTypeBuilder<UserNotificationPreference> builder)
    {
        builder.ToTable("UserNotificationPreferences");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.UserId).IsUnique();
    }
}

public sealed class AutomationFollowUpTaskConfiguration : IEntityTypeConfiguration<AutomationFollowUpTask>
{
    public void Configure(EntityTypeBuilder<AutomationFollowUpTask> builder)
    {
        builder.ToTable("AutomationFollowUpTasks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => new { x.Status, x.DueAtUtc });
        builder.HasIndex(x => x.OrganizationId);
    }
}
