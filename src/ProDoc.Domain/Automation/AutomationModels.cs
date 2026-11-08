using ProDoc.Domain.Common;

namespace ProDoc.Domain.Automation;

public enum OutboxStatus
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4,
    DeadLetter = 5,
    Cancelled = 6
}

public enum WorkflowRunStatus
{
    Succeeded = 1,
    Failed = 2,
    Skipped = 3,
    Partial = 4
}

public static class AutomationTriggerTypes
{
    public const string AppointmentCreated = "appointment.created";
    public const string AppointmentConfirmed = "appointment.confirmed";
    public const string AppointmentCancelled = "appointment.cancelled";
    public const string AppointmentRescheduled = "appointment.rescheduled";
    public const string AppointmentReminderDue = "appointment.reminder_due";
    public const string FollowUpReminderDue = "clinical.follow_up_reminder_due";
    public const string PatientCheckedIn = "appointment.checked_in";
    public const string QueuePositionChanged = "queue.position_changed";
    public const string PrescriptionFinalized = "clinical.prescription_finalized";
    public const string PaymentCompleted = "billing.payment_completed";
    public const string PaymentFailed = "billing.payment_failed";
    public const string SubscriptionRenewalApproaching = "saas.subscription_renewal_approaching";
    public const string SupportTicketStatusChanged = "support.ticket_status_changed";
    public const string DoctorRegistrationExpiring = "provider.registration_expiring";
    public const string ScheduleAvailabilityChanged = "schedule.availability_changed";

    public static IReadOnlyList<string> Catalog { get; } =
    [
        AppointmentCreated,
        AppointmentConfirmed,
        AppointmentCancelled,
        AppointmentRescheduled,
        AppointmentReminderDue,
        FollowUpReminderDue,
        PatientCheckedIn,
        QueuePositionChanged,
        PrescriptionFinalized,
        PaymentCompleted,
        PaymentFailed,
        SubscriptionRenewalApproaching,
        SupportTicketStatusChanged,
        DoctorRegistrationExpiring,
        ScheduleAvailabilityChanged
    ];
}

public static class AutomationActionTypes
{
    public const string SendInApp = "send_in_app";
    public const string SendEmail = "send_email";
    public const string SendSms = "send_sms";
    public const string SendPush = "send_push";
    public const string CreateFollowUpTask = "create_follow_up_task";
    public const string ScheduleReminder = "schedule_reminder";
    public const string EscalateOverdue = "escalate_overdue";

    public static IReadOnlyList<string> Catalog { get; } =
    [
        SendInApp,
        SendEmail,
        SendSms,
        SendPush,
        CreateFollowUpTask,
        ScheduleReminder,
        EscalateOverdue
    ];
}

public sealed class AutomationWorkflow
{
    private AutomationWorkflow()
    {
    }

    public Guid Id { get; private set; }

    public Guid? OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string TriggerType { get; private set; } = string.Empty;

    public string ConditionsJson { get; private set; } = "[]";

    public string ActionsJson { get; private set; } = "[]";

    public int DelayMinutes { get; private set; }

    public bool IsEnabled { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static AutomationWorkflow Create(
        string name,
        string triggerType,
        string conditionsJson,
        string actionsJson,
        int delayMinutes,
        Guid createdByUserId,
        DateTimeOffset now,
        Guid? organizationId = null)
    {
        Validate(name, triggerType, conditionsJson, actionsJson, delayMinutes);
        return new AutomationWorkflow
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name.Trim(),
            TriggerType = triggerType.Trim().ToLowerInvariant(),
            ConditionsJson = conditionsJson,
            ActionsJson = actionsJson,
            DelayMinutes = delayMinutes,
            IsEnabled = false,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Update(string name, string conditionsJson, string actionsJson, int delayMinutes, DateTimeOffset now)
    {
        Validate(name, TriggerType, conditionsJson, actionsJson, delayMinutes);
        Name = name.Trim();
        ConditionsJson = conditionsJson;
        ActionsJson = actionsJson;
        DelayMinutes = delayMinutes;
        UpdatedAtUtc = now;
    }

    public void SetEnabled(bool enabled, DateTimeOffset now)
    {
        if (enabled)
        {
            Validate(Name, TriggerType, ConditionsJson, ActionsJson, DelayMinutes);
        }

        IsEnabled = enabled;
        UpdatedAtUtc = now;
    }

    private static void Validate(string name, string triggerType, string conditionsJson, string actionsJson, int delayMinutes)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length is < 2 or > 120)
        {
            throw DomainException.Validation("workflow_name_invalid", "Workflow name must be 2–120 characters.");
        }

        if (!AutomationTriggerTypes.Catalog.Contains(triggerType.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            throw DomainException.Validation("workflow_trigger_invalid", "Choose a supported workflow trigger.");
        }

        if (delayMinutes is < 0 or > 60 * 24 * 30)
        {
            throw DomainException.Validation("workflow_delay_invalid", "Delay must be between 0 and 30 days.");
        }

        if (string.IsNullOrWhiteSpace(conditionsJson) || conditionsJson.Length > 8000)
        {
            throw DomainException.Validation("workflow_conditions_invalid", "Conditions JSON is required and must be under 8KB.");
        }

        if (string.IsNullOrWhiteSpace(actionsJson) || actionsJson.Length > 16000)
        {
            throw DomainException.Validation("workflow_actions_invalid", "Actions JSON is required and must be under 16KB.");
        }
    }
}

public sealed class AutomationOutboxEvent
{
    private AutomationOutboxEvent()
    {
    }

    public Guid Id { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public Guid? OrganizationId { get; private set; }

    public Guid? SubjectId { get; private set; }

    public Guid? UserId { get; private set; }

    public string PayloadJson { get; private set; } = "{}";

    public string IdempotencyKey { get; private set; } = string.Empty;

    public OutboxStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public DateTimeOffset AvailableAtUtc { get; private set; }

    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public string? DeadLetterReason { get; private set; }

    public static AutomationOutboxEvent Create(
        string eventType,
        string payloadJson,
        string idempotencyKey,
        DateTimeOffset now,
        Guid? organizationId = null,
        Guid? subjectId = null,
        Guid? userId = null,
        DateTimeOffset? availableAtUtc = null)
    {
        if (!AutomationTriggerTypes.Catalog.Contains(eventType, StringComparer.OrdinalIgnoreCase))
        {
            throw DomainException.Validation("automation_event_invalid", "Unsupported automation event type.");
        }

        return new AutomationOutboxEvent
        {
            Id = Guid.NewGuid(),
            EventType = eventType.Trim().ToLowerInvariant(),
            OrganizationId = organizationId,
            SubjectId = subjectId,
            UserId = userId,
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
            IdempotencyKey = idempotencyKey.Trim(),
            Status = OutboxStatus.Pending,
            AttemptCount = 0,
            OccurredAtUtc = now,
            AvailableAtUtc = availableAtUtc ?? now
        };
    }

    /// <summary>
    /// Claims the event for work. <see cref="AvailableAtUtc"/> becomes the processing lease expiry
    /// so a crashed worker can be reclaimed without losing the outbox row.
    /// </summary>
    public void MarkProcessing(DateTimeOffset now, TimeSpan? lease = null)
    {
        Status = OutboxStatus.Processing;
        AttemptCount += 1;
        AvailableAtUtc = now.Add(lease ?? TimeSpan.FromMinutes(5));
        LastError = null;
    }

    public void MarkCompleted(DateTimeOffset now)
    {
        Status = OutboxStatus.Completed;
        ProcessedAtUtc = now;
        LastError = null;
    }

    /// <summary>
    /// Host stop / abort while claimed — return to pending without burning the attempt.
    /// </summary>
    public void ReleaseClaim(DateTimeOffset now)
    {
        Status = OutboxStatus.Pending;
        AvailableAtUtc = now;
        if (AttemptCount > 0)
        {
            AttemptCount -= 1;
        }

        LastError = null;
    }

    public void MarkRetry(string error, DateTimeOffset nextAttempt, DateTimeOffset now)
    {
        Status = OutboxStatus.Pending;
        LastError = Trim(error, 1000);
        AvailableAtUtc = nextAttempt;
        _ = now;
    }

    public void MarkDeadLetter(string reason, DateTimeOffset now)
    {
        Status = OutboxStatus.DeadLetter;
        DeadLetterReason = Trim(reason, 1000);
        LastError = DeadLetterReason;
        ProcessedAtUtc = now;
    }

    public void Cancel(string reason, DateTimeOffset now)
    {
        Status = OutboxStatus.Cancelled;
        DeadLetterReason = Trim(reason, 1000);
        ProcessedAtUtc = now;
    }

    private static string Trim(string value, int max)
    {
        var text = value.Trim();
        return text.Length <= max ? text : text[..max];
    }
}

public sealed class AutomationWorkflowRun
{
    private AutomationWorkflowRun()
    {
    }

    public Guid Id { get; private set; }

    public Guid WorkflowId { get; private set; }

    public Guid OutboxEventId { get; private set; }

    public string TriggerType { get; private set; } = string.Empty;

    public WorkflowRunStatus Status { get; private set; }

    public string DetailJson { get; private set; } = "{}";

    public string? FailureDetails { get; private set; }

    public DateTimeOffset StartedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public static AutomationWorkflowRun Start(
        Guid workflowId,
        Guid outboxEventId,
        string triggerType,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            OutboxEventId = outboxEventId,
            TriggerType = triggerType,
            Status = WorkflowRunStatus.Succeeded,
            StartedAtUtc = now
        };

    public void Complete(WorkflowRunStatus status, string detailJson, string? failure, DateTimeOffset now)
    {
        Status = status;
        DetailJson = detailJson;
        FailureDetails = string.IsNullOrWhiteSpace(failure) ? null : failure.Trim();
        CompletedAtUtc = now;
    }
}

public sealed class UserNotificationPreference
{
    private UserNotificationPreference()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public bool InAppEnabled { get; private set; } = true;

    public bool EmailEnabled { get; private set; } = true;

    public bool SmsEnabled { get; private set; }

    public bool PushEnabled { get; private set; } = true;

    public bool MarketingEnabled { get; private set; }

    public bool AppointmentRemindersEnabled { get; private set; } = true;

    public bool QueueUpdatesEnabled { get; private set; } = true;

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static UserNotificationPreference CreateDefault(Guid userId, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UpdatedAtUtc = now
        };

    public void Update(
        bool inApp,
        bool email,
        bool sms,
        bool push,
        bool marketing,
        bool appointmentReminders,
        bool queueUpdates,
        DateTimeOffset now)
    {
        // Essential service channels remain available for critical ops; preferences gate non-essential paths.
        InAppEnabled = inApp;
        EmailEnabled = email;
        SmsEnabled = sms;
        PushEnabled = push;
        MarketingEnabled = marketing;
        AppointmentRemindersEnabled = appointmentReminders;
        QueueUpdatesEnabled = queueUpdates;
        UpdatedAtUtc = now;
    }
}

public sealed class AutomationFollowUpTask
{
    private AutomationFollowUpTask()
    {
    }

    public Guid Id { get; private set; }

    public Guid? OrganizationId { get; private set; }

    public Guid? AssignedUserId { get; private set; }

    public Guid? SubjectId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public string Status { get; private set; } = "Open";

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? DueAtUtc { get; private set; }

    public static AutomationFollowUpTask Create(
        string title,
        string body,
        DateTimeOffset now,
        Guid? organizationId = null,
        Guid? assignedUserId = null,
        Guid? subjectId = null,
        DateTimeOffset? dueAtUtc = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            AssignedUserId = assignedUserId,
            SubjectId = subjectId,
            Title = title.Trim(),
            Body = body.Trim(),
            CreatedAtUtc = now,
            DueAtUtc = dueAtUtc
        };

    public void Escalate(DateTimeOffset now)
    {
        Status = "Escalated";
        _ = now;
    }
}
