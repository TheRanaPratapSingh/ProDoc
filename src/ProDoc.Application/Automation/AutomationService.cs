using System.Text.Json;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Providers;
using ProDoc.Domain.Automation;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;

namespace ProDoc.Application.Automation;

public sealed class AutomationService(
    IAutomationStore store,
    AutomationEngine engine,
    IClock clock) : IAutomationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task SeedDefaultsAsync(CancellationToken cancellationToken)
    {
        var existing = await store.ListWorkflowsAsync(null, cancellationToken);
        if (existing.Count > 0)
        {
            return;
        }

        var systemUser = Guid.Empty;
        await CreateInternalAsync(
            systemUser,
            "Appointment confirmed — schedule reminder",
            AutomationTriggerTypes.AppointmentConfirmed,
            [],
            [
                new AutomationActionDto(
                    AutomationActionTypes.ScheduleReminder,
                    DelayMinutes: 60,
                    ReminderEventType: AutomationTriggerTypes.AppointmentReminderDue),
                new AutomationActionDto(
                    AutomationActionTypes.SendInApp,
                    TitleTemplate: "Appointment confirmed",
                    BodyTemplate: "Your visit on {startsAtUtc} is confirmed.")
            ],
            0,
            null,
            enable: true,
            cancellationToken);

        await CreateInternalAsync(
            systemUser,
            "Appointment reminder due — notify patient",
            AutomationTriggerTypes.AppointmentReminderDue,
            [],
            [
                new AutomationActionDto(
                    AutomationActionTypes.SendInApp,
                    TitleTemplate: "Appointment reminder",
                    BodyTemplate: "Your appointment starts at {startsAtUtc}."),
                new AutomationActionDto(
                    AutomationActionTypes.SendPush,
                    TitleTemplate: "Appointment reminder",
                    BodyTemplate: "Your appointment starts at {startsAtUtc}.")
            ],
            0,
            null,
            enable: true,
            cancellationToken);

        await CreateInternalAsync(
            systemUser,
            "Payment failed — alert patient",
            AutomationTriggerTypes.PaymentFailed,
            [],
            [
                new AutomationActionDto(
                    AutomationActionTypes.SendInApp,
                    TitleTemplate: "Payment failed",
                    BodyTemplate: "We could not complete your payment. Please try again.")
            ],
            0,
            null,
            enable: true,
            cancellationToken);

        await CreateInternalAsync(
            systemUser,
            "Support ticket updated — notify requester",
            AutomationTriggerTypes.SupportTicketStatusChanged,
            [],
            [
                new AutomationActionDto(
                    AutomationActionTypes.SendInApp,
                    TitleTemplate: "Support update",
                    BodyTemplate: "Your ticket status is now {status}.")
            ],
            0,
            null,
            enable: true,
            cancellationToken);
    }

    public AutomationCatalog Catalog() =>
        new(
            AutomationTriggerTypes.Catalog.ToArray(),
            AutomationActionTypes.Catalog.ToArray(),
            ["eq", "neq", "contains", "exists", "gte", "lte"]);

    public async Task<IReadOnlyList<AutomationWorkflowDetails>> ListWorkflowsAsync(
        Guid actorUserId,
        Guid? organizationId,
        CancellationToken cancellationToken)
    {
        _ = actorUserId;
        var rows = await store.ListWorkflowsAsync(organizationId, cancellationToken);
        return rows.Select(MapWorkflow).ToArray();
    }

    public async Task<AutomationWorkflowDetails> CreateWorkflowAsync(
        Guid actorUserId,
        string name,
        string triggerType,
        IReadOnlyList<AutomationConditionDto> conditions,
        IReadOnlyList<AutomationActionDto> actions,
        int delayMinutes,
        Guid? organizationId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        AutomationEngine.ValidateConfiguration(conditions, actions);
        var workflow = AutomationWorkflow.Create(
            name,
            triggerType,
            JsonSerializer.Serialize(conditions, JsonOptions),
            JsonSerializer.Serialize(actions, JsonOptions),
            delayMinutes,
            actorUserId,
            clock.UtcNow,
            organizationId);
        store.AddWorkflow(workflow);
        await store.AddAuditAsync(Audit(actorUserId, AuditActions.AutomationWorkflowChanged, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapWorkflow(workflow);
    }

    public async Task<AutomationWorkflowDetails> UpdateWorkflowAsync(
        Guid actorUserId,
        Guid workflowId,
        string name,
        IReadOnlyList<AutomationConditionDto> conditions,
        IReadOnlyList<AutomationActionDto> actions,
        int delayMinutes,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        AutomationEngine.ValidateConfiguration(conditions, actions);
        var workflow = await store.FindWorkflowAsync(workflowId, true, cancellationToken)
            ?? throw DomainException.NotFound("workflow_not_found", "That workflow was not found.");
        workflow.Update(
            name,
            JsonSerializer.Serialize(conditions, JsonOptions),
            JsonSerializer.Serialize(actions, JsonOptions),
            delayMinutes,
            clock.UtcNow);
        await store.AddAuditAsync(Audit(actorUserId, AuditActions.AutomationWorkflowChanged, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapWorkflow(workflow);
    }

    public async Task<AutomationWorkflowDetails> SetEnabledAsync(
        Guid actorUserId,
        Guid workflowId,
        bool enabled,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var workflow = await store.FindWorkflowAsync(workflowId, true, cancellationToken)
            ?? throw DomainException.NotFound("workflow_not_found", "That workflow was not found.");
        if (enabled)
        {
            var conditions = JsonSerializer.Deserialize<List<AutomationConditionDto>>(workflow.ConditionsJson, JsonOptions) ?? [];
            var actions = JsonSerializer.Deserialize<List<AutomationActionDto>>(workflow.ActionsJson, JsonOptions) ?? [];
            AutomationEngine.ValidateConfiguration(conditions, actions);
        }

        workflow.SetEnabled(enabled, clock.UtcNow);
        await store.AddAuditAsync(Audit(actorUserId, AuditActions.AutomationWorkflowChanged, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapWorkflow(workflow);
    }

    public async Task<IReadOnlyList<AutomationWorkflowRunDetails>> ListRunsAsync(Guid? workflowId, CancellationToken cancellationToken)
    {
        var runs = await store.ListRunsAsync(workflowId, 100, cancellationToken);
        var names = new Dictionary<Guid, string>();
        var result = new List<AutomationWorkflowRunDetails>();
        foreach (var run in runs)
        {
            if (!names.TryGetValue(run.WorkflowId, out var name))
            {
                var workflow = await store.FindWorkflowAsync(run.WorkflowId, false, cancellationToken);
                name = workflow?.Name ?? "Workflow";
                names[run.WorkflowId] = name;
            }

            result.Add(new AutomationWorkflowRunDetails(
                run.Id,
                run.WorkflowId,
                name,
                run.TriggerType,
                run.Status.ToString(),
                run.DetailJson,
                run.FailureDetails,
                run.StartedAtUtc,
                run.CompletedAtUtc));
        }

        return result;
    }

    public async Task<IReadOnlyList<AutomationOutboxDetails>> ListOutboxAsync(string? status, CancellationToken cancellationToken)
    {
        OutboxStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OutboxStatus>(status, true, out var value))
        {
            parsed = value;
        }

        var rows = await store.ListOutboxAsync(parsed, 100, cancellationToken);
        return rows.Select(item => new AutomationOutboxDetails(
            item.Id,
            item.EventType,
            item.Status.ToString(),
            item.AttemptCount,
            item.OccurredAtUtc,
            item.AvailableAtUtc,
            item.ProcessedAtUtc,
            item.LastError,
            item.DeadLetterReason,
            item.SubjectId,
            item.UserId)).ToArray();
    }

    public Task<AutomationAnalytics> AnalyticsAsync(CancellationToken cancellationToken) =>
        store.BuildAnalyticsAsync(cancellationToken);

    public async Task<NotificationPreferenceDetails> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var prefs = await store.FindPreferenceAsync(userId, false, cancellationToken)
            ?? UserNotificationPreference.CreateDefault(userId, clock.UtcNow);
        return MapPrefs(prefs);
    }

    public async Task<NotificationPreferenceDetails> UpdatePreferencesAsync(
        Guid userId,
        bool inApp,
        bool email,
        bool sms,
        bool push,
        bool marketing,
        bool appointmentReminders,
        bool queueUpdates,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var prefs = await store.FindPreferenceAsync(userId, true, cancellationToken);
        if (prefs is null)
        {
            prefs = UserNotificationPreference.CreateDefault(userId, clock.UtcNow);
            store.AddPreference(prefs);
        }

        prefs.Update(inApp, email, sms, push, marketing, appointmentReminders, queueUpdates, clock.UtcNow);
        await store.AddAuditAsync(Audit(userId, AuditActions.NotificationPreferencesChanged, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapPrefs(prefs);
    }

    public async Task ProcessDueAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var batch = await store.ClaimPendingOutboxAsync(25, clock.UtcNow, cancellationToken);
        for (var index = 0; index < batch.Count; index++)
        {
            var item = batch[index];
            if (cancellationToken.IsCancellationRequested)
            {
                await ReleaseClaimedFromAsync(batch, index, CancellationToken.None);
                cancellationToken.ThrowIfCancellationRequested();
            }

            try
            {
                // Per-event timeout for stuck DB/HTTP work; linked so host stop still cancels cleanly.
                using var workCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                workCts.CancelAfter(TimeSpan.FromSeconds(60));
                await engine.ProcessOutboxEventAsync(item, workCts.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Do not treat shutdown as a failed attempt — keep events pending for the next run.
                await ReleaseClaimedFromAsync(batch, index, CancellationToken.None);
                throw;
            }
            catch (Exception ex)
            {
                var message = ex is OperationCanceledException
                    ? "Automation processing timed out."
                    : ex.Message;

                if (item.AttemptCount >= 5)
                {
                    item.MarkDeadLetter(message, clock.UtcNow);
                }
                else
                {
                    // Exponential backoff: 2/4/8/16 minutes based on attempt count.
                    var delay = TimeSpan.FromMinutes(Math.Pow(2, Math.Min(item.AttemptCount, 4)));
                    item.MarkRetry(message, clock.UtcNow.Add(delay), clock.UtcNow);
                }

                // Persist retry/dead-letter even if the host token is already canceled.
                await store.SaveChangesAsync(CancellationToken.None);
            }
        }
    }

    private async Task ReleaseClaimedFromAsync(
        IReadOnlyList<AutomationOutboxEvent> batch,
        int fromIndex,
        CancellationToken cancellationToken)
    {
        var changed = false;
        for (var i = fromIndex; i < batch.Count; i++)
        {
            var item = batch[i];
            if (item.Status != OutboxStatus.Processing)
            {
                continue;
            }

            item.ReleaseClaim(clock.UtcNow);
            changed = true;
        }

        if (changed)
        {
            await store.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task CreateInternalAsync(
        Guid actorUserId,
        string name,
        string trigger,
        IReadOnlyList<AutomationConditionDto> conditions,
        IReadOnlyList<AutomationActionDto> actions,
        int delay,
        Guid? organizationId,
        bool enable,
        CancellationToken cancellationToken)
    {
        AutomationEngine.ValidateConfiguration(conditions, actions);
        var workflow = AutomationWorkflow.Create(
            name,
            trigger,
            JsonSerializer.Serialize(conditions, JsonOptions),
            JsonSerializer.Serialize(actions, JsonOptions),
            delay,
            actorUserId == Guid.Empty ? Guid.Parse("00000000-0000-0000-0000-000000000001") : actorUserId,
            clock.UtcNow,
            organizationId);
        if (enable)
        {
            workflow.SetEnabled(true, clock.UtcNow);
        }

        store.AddWorkflow(workflow);
        await store.SaveChangesAsync(cancellationToken);
    }

    private static AutomationWorkflowDetails MapWorkflow(AutomationWorkflow workflow) =>
        new(
            workflow.Id,
            workflow.OrganizationId,
            workflow.Name,
            workflow.TriggerType,
            JsonSerializer.Deserialize<List<AutomationConditionDto>>(workflow.ConditionsJson, JsonOptions) ?? [],
            JsonSerializer.Deserialize<List<AutomationActionDto>>(workflow.ActionsJson, JsonOptions) ?? [],
            workflow.DelayMinutes,
            workflow.IsEnabled,
            workflow.UpdatedAtUtc);

    private static NotificationPreferenceDetails MapPrefs(UserNotificationPreference prefs) =>
        new(
            prefs.InAppEnabled,
            prefs.EmailEnabled,
            prefs.SmsEnabled,
            prefs.PushEnabled,
            prefs.MarketingEnabled,
            prefs.AppointmentRemindersEnabled,
            prefs.QueueUpdatesEnabled,
            prefs.UpdatedAtUtc);

    private AuditLog Audit(Guid userId, string action, RequestMetadata metadata) =>
        AuditLog.Record(userId == Guid.Empty ? null : userId, action, clock.UtcNow, metadata.IpAddress, metadata.UserAgent);
}
