using ProDoc.Application.Providers;

namespace ProDoc.Application.Automation;

public interface IAutomationService
{
    Task SeedDefaultsAsync(CancellationToken cancellationToken);

    AutomationCatalog Catalog();

    Task<IReadOnlyList<AutomationWorkflowDetails>> ListWorkflowsAsync(Guid actorUserId, Guid? organizationId, CancellationToken cancellationToken);

    Task<AutomationWorkflowDetails> CreateWorkflowAsync(
        Guid actorUserId,
        string name,
        string triggerType,
        IReadOnlyList<AutomationConditionDto> conditions,
        IReadOnlyList<AutomationActionDto> actions,
        int delayMinutes,
        Guid? organizationId,
        RequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<AutomationWorkflowDetails> UpdateWorkflowAsync(
        Guid actorUserId,
        Guid workflowId,
        string name,
        IReadOnlyList<AutomationConditionDto> conditions,
        IReadOnlyList<AutomationActionDto> actions,
        int delayMinutes,
        RequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<AutomationWorkflowDetails> SetEnabledAsync(
        Guid actorUserId,
        Guid workflowId,
        bool enabled,
        RequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AutomationWorkflowRunDetails>> ListRunsAsync(Guid? workflowId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AutomationOutboxDetails>> ListOutboxAsync(string? status, CancellationToken cancellationToken);

    Task<AutomationAnalytics> AnalyticsAsync(CancellationToken cancellationToken);

    Task<NotificationPreferenceDetails> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken);

    Task<NotificationPreferenceDetails> UpdatePreferencesAsync(
        Guid userId,
        bool inApp,
        bool email,
        bool sms,
        bool push,
        bool marketing,
        bool appointmentReminders,
        bool queueUpdates,
        RequestMetadata metadata,
        CancellationToken cancellationToken);

    Task ProcessDueAsync(CancellationToken cancellationToken);
}
