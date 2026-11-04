namespace ProDoc.Application.Automation;

public sealed record AutomationConditionDto(string Field, string Op, string Value);

public sealed record AutomationActionDto(
    string Type,
    string? TitleTemplate = null,
    string? BodyTemplate = null,
    string? ChannelHint = null,
    int? DelayMinutes = null,
    string? ReminderEventType = null);

public sealed record AutomationWorkflowDetails(
    Guid Id,
    Guid? OrganizationId,
    string Name,
    string TriggerType,
    IReadOnlyList<AutomationConditionDto> Conditions,
    IReadOnlyList<AutomationActionDto> Actions,
    int DelayMinutes,
    bool IsEnabled,
    DateTimeOffset UpdatedAtUtc);

public sealed record AutomationWorkflowRunDetails(
    Guid Id,
    Guid WorkflowId,
    string WorkflowName,
    string TriggerType,
    string Status,
    string DetailJson,
    string? FailureDetails,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record AutomationOutboxDetails(
    Guid Id,
    string EventType,
    string Status,
    int AttemptCount,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset AvailableAtUtc,
    DateTimeOffset? ProcessedAtUtc,
    string? LastError,
    string? DeadLetterReason,
    Guid? SubjectId,
    Guid? UserId);

public sealed record NotificationPreferenceDetails(
    bool InAppEnabled,
    bool EmailEnabled,
    bool SmsEnabled,
    bool PushEnabled,
    bool MarketingEnabled,
    bool AppointmentRemindersEnabled,
    bool QueueUpdatesEnabled,
    DateTimeOffset UpdatedAtUtc);

public sealed record AutomationAnalytics(
    int EnabledWorkflows,
    int RunsLast7Days,
    int FailedRunsLast7Days,
    int DeadLetters,
    int PendingOutbox,
    IReadOnlyList<NamedCount> RunsByTrigger);

public sealed record NamedCount(string Label, int Count);

public sealed record AutomationCatalog(
    IReadOnlyList<string> Triggers,
    IReadOnlyList<string> Actions,
    IReadOnlyList<string> ConditionOperators);
