using ProDoc.Domain.Appointments;
using ProDoc.Domain.Automation;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Queue;

namespace ProDoc.Application.Automation;

public interface IAutomationStore
{
    Task<IReadOnlyList<AutomationWorkflow>> ListWorkflowsAsync(Guid? organizationId, CancellationToken cancellationToken);

    Task<AutomationWorkflow?> FindWorkflowAsync(Guid workflowId, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<AutomationWorkflow>> ListEnabledForTriggerAsync(string triggerType, Guid? organizationId, CancellationToken cancellationToken);

    void AddWorkflow(AutomationWorkflow workflow);

    Task<bool> OutboxExistsAsync(string idempotencyKey, CancellationToken cancellationToken);

    void AddOutbox(AutomationOutboxEvent item);

    Task<IReadOnlyList<AutomationOutboxEvent>> ClaimPendingOutboxAsync(int take, DateTimeOffset now, CancellationToken cancellationToken);

    Task<AutomationOutboxEvent?> FindOutboxAsync(Guid id, bool tracking, CancellationToken cancellationToken);

    Task CancelPendingRemindersForSubjectAsync(Guid subjectId, string eventTypePrefix, string reason, DateTimeOffset now, CancellationToken cancellationToken);

    void AddRun(AutomationWorkflowRun run);

    Task<IReadOnlyList<AutomationWorkflowRun>> ListRunsAsync(Guid? workflowId, int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<AutomationOutboxEvent>> ListOutboxAsync(OutboxStatus? status, int take, CancellationToken cancellationToken);

    Task<UserNotificationPreference?> FindPreferenceAsync(Guid userId, bool tracking, CancellationToken cancellationToken);

    void AddPreference(UserNotificationPreference preference);

    Task<Appointment?> FindAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken);

    Task NotifyAsync(UserNotification notification, CancellationToken cancellationToken);

    Task<bool> HasRecentNotificationAsync(Guid userId, string kind, Guid? subjectId, DateTimeOffset sinceUtc, CancellationToken cancellationToken);

    void AddFollowUpTask(AutomationFollowUpTask task);

    Task<string?> FindUserEmailAsync(Guid userId, CancellationToken cancellationToken);

    Task<string?> FindUserPhoneAsync(Guid userId, CancellationToken cancellationToken);

    Task<AutomationAnalytics> BuildAnalyticsAsync(CancellationToken cancellationToken);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IAutomationEventBus
{
    Task PublishAsync(
        string eventType,
        object payload,
        string idempotencyKey,
        CancellationToken cancellationToken,
        Guid? organizationId = null,
        Guid? subjectId = null,
        Guid? userId = null,
        DateTimeOffset? availableAtUtc = null);
}

public interface IAutomationMessenger
{
    Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken);

    Task SendSmsAsync(string phoneE164, string body, CancellationToken cancellationToken);
}
