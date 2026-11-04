using Microsoft.EntityFrameworkCore;
using ProDoc.Application.Automation;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Automation;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Queue;
using ProDoc.Infrastructure.Identity;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.Automation;

public sealed class AutomationStore(ProDocDbContext db) : IAutomationStore
{
    public async Task<IReadOnlyList<AutomationWorkflow>> ListWorkflowsAsync(Guid? organizationId, CancellationToken cancellationToken)
    {
        var query = db.AutomationWorkflows.AsNoTracking().AsQueryable();
        query = organizationId is null
            ? query.Where(item => item.OrganizationId == null)
            : query.Where(item => item.OrganizationId == organizationId || item.OrganizationId == null);
        return await query.OrderByDescending(item => item.UpdatedAtUtc).ToListAsync(cancellationToken);
    }

    public Task<AutomationWorkflow?> FindWorkflowAsync(Guid workflowId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.AutomationWorkflows.AsQueryable() : db.AutomationWorkflows.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == workflowId, cancellationToken);
    }

    public async Task<IReadOnlyList<AutomationWorkflow>> ListEnabledForTriggerAsync(
        string triggerType,
        Guid? organizationId,
        CancellationToken cancellationToken)
    {
        var trigger = triggerType.Trim().ToLowerInvariant();
        var query = db.AutomationWorkflows.AsNoTracking()
            .Where(item => item.IsEnabled && item.TriggerType == trigger);
        query = organizationId is null
            ? query.Where(item => item.OrganizationId == null)
            : query.Where(item => item.OrganizationId == organizationId);
        return await query.ToListAsync(cancellationToken);
    }

    public void AddWorkflow(AutomationWorkflow workflow) => db.AutomationWorkflows.Add(workflow);

    public Task<bool> OutboxExistsAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        db.AutomationOutboxEvents.AsNoTracking().AnyAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);

    public void AddOutbox(AutomationOutboxEvent item) => db.AutomationOutboxEvents.Add(item);

    public async Task<IReadOnlyList<AutomationOutboxEvent>> ClaimPendingOutboxAsync(
        int take,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Serializable claim + lease reclaim keeps dual workers from double-processing and
        // recovers rows left in Processing after a crash or abrupt host stop.
        // Must run inside the SQL Server retrying execution strategy when using user transactions.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable,
                cancellationToken);

            var rows = await db.AutomationOutboxEvents
                .Where(item =>
                    (item.Status == OutboxStatus.Pending && item.AvailableAtUtc <= now)
                    || (item.Status == OutboxStatus.Processing && item.AvailableAtUtc <= now))
                .OrderBy(item => item.AvailableAtUtc)
                .Take(take)
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                row.MarkProcessing(now);
            }

            if (rows.Count > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return (IReadOnlyList<AutomationOutboxEvent>)rows;
        });
    }

    public Task<AutomationOutboxEvent?> FindOutboxAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.AutomationOutboxEvents.AsQueryable() : db.AutomationOutboxEvents.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task CancelPendingRemindersForSubjectAsync(
        Guid subjectId,
        string eventTypePrefix,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var rows = await db.AutomationOutboxEvents
            .Where(item => item.SubjectId == subjectId
                && item.Status == OutboxStatus.Pending
                && item.EventType.StartsWith(eventTypePrefix))
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            row.Cancel(reason, now);
        }
    }

    public void AddRun(AutomationWorkflowRun run) => db.AutomationWorkflowRuns.Add(run);

    public async Task<IReadOnlyList<AutomationWorkflowRun>> ListRunsAsync(Guid? workflowId, int take, CancellationToken cancellationToken)
    {
        var query = db.AutomationWorkflowRuns.AsNoTracking().AsQueryable();
        if (workflowId is Guid id)
        {
            query = query.Where(item => item.WorkflowId == id);
        }

        return await query.OrderByDescending(item => item.StartedAtUtc).Take(take).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AutomationOutboxEvent>> ListOutboxAsync(OutboxStatus? status, int take, CancellationToken cancellationToken)
    {
        var query = db.AutomationOutboxEvents.AsNoTracking().AsQueryable();
        if (status is OutboxStatus value)
        {
            query = query.Where(item => item.Status == value);
        }

        return await query.OrderByDescending(item => item.OccurredAtUtc).Take(take).ToListAsync(cancellationToken);
    }

    public Task<UserNotificationPreference?> FindPreferenceAsync(Guid userId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.UserNotificationPreferences.AsQueryable() : db.UserNotificationPreferences.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
    }

    public void AddPreference(UserNotificationPreference preference) => db.UserNotificationPreferences.Add(preference);

    public Task<Appointment?> FindAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken) =>
        db.Appointments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == appointmentId, cancellationToken);

    public Task NotifyAsync(UserNotification notification, CancellationToken cancellationToken)
    {
        db.UserNotifications.Add(notification);
        return Task.CompletedTask;
    }

    public Task<bool> HasRecentNotificationAsync(Guid userId, string kind, Guid? subjectId, DateTimeOffset sinceUtc, CancellationToken cancellationToken) =>
        db.UserNotifications.AsNoTracking().AnyAsync(
            item => item.UserId == userId
                && item.Kind == kind
                && item.SubjectId == subjectId
                && item.CreatedAtUtc >= sinceUtc,
            cancellationToken);

    public void AddFollowUpTask(AutomationFollowUpTask task) => db.AutomationFollowUpTasks.Add(task);

    public async Task<string?> FindUserEmailAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        return user?.Email;
    }

    public async Task<string?> FindUserPhoneAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        return user?.PhoneNumber;
    }

    public async Task<AutomationAnalytics> BuildAnalyticsAsync(CancellationToken cancellationToken)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-7);
        var enabled = await db.AutomationWorkflows.AsNoTracking().CountAsync(item => item.IsEnabled, cancellationToken);
        var runs = await db.AutomationWorkflowRuns.AsNoTracking().Where(item => item.StartedAtUtc >= since).ToListAsync(cancellationToken);
        var dead = await db.AutomationOutboxEvents.AsNoTracking().CountAsync(item => item.Status == OutboxStatus.DeadLetter, cancellationToken);
        var pending = await db.AutomationOutboxEvents.AsNoTracking().CountAsync(item => item.Status == OutboxStatus.Pending, cancellationToken);
        var byTrigger = runs
            .GroupBy(item => item.TriggerType)
            .Select(group => new NamedCount(group.Key, group.Count()))
            .OrderByDescending(item => item.Count)
            .ToArray();
        return new AutomationAnalytics(
            enabled,
            runs.Count,
            runs.Count(item => item.Status is WorkflowRunStatus.Failed or WorkflowRunStatus.Partial),
            dead,
            pending,
            byTrigger);
    }

    public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(audit);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
