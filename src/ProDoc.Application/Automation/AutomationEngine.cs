using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Mobile;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Automation;
using ProDoc.Domain.Common;
using ProDoc.Domain.Queue;

namespace ProDoc.Application.Automation;

public sealed class AutomationEngine(
    IAutomationStore store,
    IAutomationEventBus bus,
    IAutomationMessenger messenger,
    IMobileStore mobileStore,
    IPushNotificationSender push,
    IClock clock,
    ILogger<AutomationEngine> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> AllowedOps = new(StringComparer.OrdinalIgnoreCase)
    {
        "eq", "neq", "contains", "exists", "gte", "lte"
    };

    public async Task ProcessOutboxEventAsync(AutomationOutboxEvent item, CancellationToken cancellationToken)
    {
        if (item.EventType == AutomationTriggerTypes.AppointmentCancelled && item.SubjectId is Guid cancelledId)
        {
            await store.CancelPendingRemindersForSubjectAsync(
                cancelledId,
                AutomationTriggerTypes.AppointmentReminderDue,
                "Appointment cancelled",
                clock.UtcNow,
                cancellationToken);
        }

        if (item.EventType == AutomationTriggerTypes.AppointmentReminderDue)
        {
            if (!await ReminderStillValidAsync(item, cancellationToken))
            {
                item.Cancel("Reminder obsolete after cancel/reschedule", clock.UtcNow);
                await store.SaveChangesAsync(cancellationToken);
                return;
            }
        }

        using var payloadDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(item.PayloadJson) ? "{}" : item.PayloadJson);
        var workflows = await store.ListEnabledForTriggerAsync(item.EventType, item.OrganizationId, cancellationToken);
        // Also include platform-wide workflows (OrganizationId null).
        if (item.OrganizationId is not null)
        {
            var platform = await store.ListEnabledForTriggerAsync(item.EventType, null, cancellationToken);
            workflows = workflows.Concat(platform).GroupBy(w => w.Id).Select(g => g.First()).ToArray();
        }

        foreach (var workflow in workflows)
        {
            await ExecuteWorkflowAsync(workflow, item, payloadDoc.RootElement, cancellationToken);
        }

        item.MarkCompleted(clock.UtcNow);
        await store.SaveChangesAsync(cancellationToken);
    }

    private async Task ExecuteWorkflowAsync(
        AutomationWorkflow workflow,
        AutomationOutboxEvent item,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        var run = AutomationWorkflowRun.Start(workflow.Id, item.Id, item.EventType, clock.UtcNow);
        store.AddRun(run);

        try
        {
            var conditions = DeserializeConditions(workflow.ConditionsJson);
            if (!EvaluateConditions(conditions, payload))
            {
                run.Complete(WorkflowRunStatus.Skipped, "{\"reason\":\"conditions_not_met\"}", null, clock.UtcNow);
                await store.SaveChangesAsync(cancellationToken);
                return;
            }

            var actions = DeserializeActions(workflow.ActionsJson);
            var results = new List<object>();
            var failures = new List<string>();
            foreach (var action in actions)
            {
                try
                {
                    var detail = await ExecuteActionAsync(action, workflow, item, payload, cancellationToken);
                    results.Add(new { action.Type, detail, ok = true });
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Automation action {Action} failed for workflow {WorkflowId}", action.Type, workflow.Id);
                    failures.Add($"{action.Type}: {ex.Message}");
                    results.Add(new { action.Type, ok = false, error = ex.Message });
                }
            }

            run.Complete(
                failures.Count == 0 ? WorkflowRunStatus.Succeeded : failures.Count == actions.Count ? WorkflowRunStatus.Failed : WorkflowRunStatus.Partial,
                JsonSerializer.Serialize(results, JsonOptions),
                failures.Count == 0 ? null : string.Join("; ", failures),
                clock.UtcNow);
            await store.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            run.Complete(WorkflowRunStatus.Failed, "{}", ex.Message, clock.UtcNow);
            await store.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private async Task<string> ExecuteActionAsync(
        AutomationActionDto action,
        AutomationWorkflow workflow,
        AutomationOutboxEvent item,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        if (!AutomationActionTypes.Catalog.Contains(action.Type, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Action '{action.Type}' is not allowlisted.");
        }

        var title = Render(action.TitleTemplate ?? workflow.Name, payload);
        var body = Render(action.BodyTemplate ?? "ProDoc automation notification.", payload);
        var userId = item.UserId ?? ReadGuid(payload, "userId") ?? ReadGuid(payload, "accountUserId");

        switch (action.Type.ToLowerInvariant())
        {
            case AutomationActionTypes.ScheduleReminder:
            {
                var minutes = action.DelayMinutes ?? 60;
                var reminderType = string.IsNullOrWhiteSpace(action.ReminderEventType)
                    ? AutomationTriggerTypes.AppointmentReminderDue
                    : action.ReminderEventType!;
                if (!AutomationTriggerTypes.Catalog.Contains(reminderType, StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Reminder event type is not allowlisted.");
                }

                await bus.PublishAsync(
                    reminderType,
                    JsonSerializer.Deserialize<object>(item.PayloadJson) ?? new { },
                    $"{item.IdempotencyKey}:reminder:{workflow.Id:N}:{minutes}",
                    cancellationToken,
                    item.OrganizationId,
                    item.SubjectId,
                    item.UserId,
                    clock.UtcNow.AddMinutes(minutes));
                return $"scheduled:{minutes}m";
            }
            case AutomationActionTypes.CreateFollowUpTask:
            {
                store.AddFollowUpTask(AutomationFollowUpTask.Create(
                    title,
                    body,
                    clock.UtcNow,
                    item.OrganizationId,
                    assignedUserId: ReadGuid(payload, "assigneeUserId"),
                    subjectId: item.SubjectId,
                    dueAtUtc: clock.UtcNow.AddHours(24)));
                return "task_created";
            }
            case AutomationActionTypes.EscalateOverdue:
            {
                store.AddFollowUpTask(AutomationFollowUpTask.Create(
                    $"[Escalated] {title}",
                    body,
                    clock.UtcNow,
                    item.OrganizationId,
                    subjectId: item.SubjectId,
                    dueAtUtc: clock.UtcNow));
                return "escalated";
            }
            case AutomationActionTypes.SendInApp:
            case AutomationActionTypes.SendEmail:
            case AutomationActionTypes.SendSms:
            case AutomationActionTypes.SendPush:
            {
                if (userId is null)
                {
                    return "skipped_no_user";
                }

                var prefs = await store.FindPreferenceAsync(userId.Value, false, cancellationToken)
                    ?? UserNotificationPreference.CreateDefault(userId.Value, clock.UtcNow);
                var essential = IsEssential(item.EventType);
                var kind = $"automation.{item.EventType}";
                var since = clock.UtcNow.AddMinutes(-5);
                if (await store.HasRecentNotificationAsync(userId.Value, kind, item.SubjectId, since, cancellationToken))
                {
                    return "duplicate_suppressed";
                }

                if (action.Type.Equals(AutomationActionTypes.SendInApp, StringComparison.OrdinalIgnoreCase))
                {
                    if (!essential && !prefs.InAppEnabled)
                    {
                        return "skipped_preference";
                    }

                    if (item.EventType.Contains("reminder", StringComparison.Ordinal) && !prefs.AppointmentRemindersEnabled && !essential)
                    {
                        return "skipped_reminder_opt_out";
                    }

                    await store.NotifyAsync(UserNotification.Create(userId.Value, kind, item.SubjectId, title, body, clock.UtcNow), cancellationToken);
                    return "in_app_queued";
                }

                if (action.Type.Equals(AutomationActionTypes.SendPush, StringComparison.OrdinalIgnoreCase))
                {
                    if (!essential && !prefs.PushEnabled)
                    {
                        return "skipped_preference";
                    }

                    var devices = await mobileStore.ListActiveForUserAsync(userId.Value, cancellationToken);
                    if (devices.Count == 0)
                    {
                        return "no_devices";
                    }

                    await push.SendAsync(
                        devices.Select(d => d.Token).ToArray(),
                        title,
                        body,
                        new Dictionary<string, string>
                        {
                            ["kind"] = kind,
                            ["subjectId"] = item.SubjectId?.ToString() ?? ""
                        },
                        cancellationToken);
                    return "push_sent";
                }

                if (action.Type.Equals(AutomationActionTypes.SendEmail, StringComparison.OrdinalIgnoreCase))
                {
                    if (!essential && !prefs.EmailEnabled)
                    {
                        return "skipped_preference";
                    }

                    if (action.ChannelHint == "marketing" && !prefs.MarketingEnabled)
                    {
                        return "skipped_marketing_opt_out";
                    }

                    var email = await store.FindUserEmailAsync(userId.Value, cancellationToken);
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        return "no_email";
                    }

                    await messenger.SendEmailAsync(email, title, body, cancellationToken);
                    return "email_sent";
                }

                if (action.Type.Equals(AutomationActionTypes.SendSms, StringComparison.OrdinalIgnoreCase))
                {
                    if (!essential && !prefs.SmsEnabled)
                    {
                        return "skipped_preference";
                    }

                    var phone = await store.FindUserPhoneAsync(userId.Value, cancellationToken);
                    if (string.IsNullOrWhiteSpace(phone))
                    {
                        return "no_phone";
                    }

                    await messenger.SendSmsAsync(phone, body, cancellationToken);
                    return "sms_sent";
                }

                return "noop";
            }
            default:
                throw new InvalidOperationException($"Unhandled action '{action.Type}'.");
        }
    }

    private async Task<bool> ReminderStillValidAsync(AutomationOutboxEvent item, CancellationToken cancellationToken)
    {
        if (item.SubjectId is not Guid appointmentId)
        {
            return false;
        }

        var appointment = await store.FindAppointmentAsync(appointmentId, cancellationToken);
        if (appointment is null)
        {
            return false;
        }

        if (appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.Rejected or AppointmentStatus.Expired)
        {
            return false;
        }

        if (appointment.Status is not (AppointmentStatus.Confirmed or AppointmentStatus.Requested or AppointmentStatus.CheckedIn))
        {
            return false;
        }

        // Payload may include expectedStartsAtUtc; if rescheduled, skip.
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(item.PayloadJson) ? "{}" : item.PayloadJson);
        if (doc.RootElement.TryGetProperty("startsAtUtc", out var starts) &&
            starts.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(starts.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expected) &&
            Math.Abs((expected - appointment.StartsAtUtc).TotalMinutes) > 1)
        {
            return false;
        }

        return true;
    }

    public static void ValidateConfiguration(IReadOnlyList<AutomationConditionDto> conditions, IReadOnlyList<AutomationActionDto> actions)
    {
        foreach (var condition in conditions)
        {
            if (string.IsNullOrWhiteSpace(condition.Field) || condition.Field.Length > 80)
            {
                throw DomainException.Validation("condition_field_invalid", "Condition field is invalid.");
            }

            if (!AllowedOps.Contains(condition.Op))
            {
                throw DomainException.Validation("condition_op_invalid", "Condition operator is not allowlisted.");
            }
        }

        if (actions.Count == 0)
        {
            throw DomainException.Validation("workflow_actions_required", "Add at least one allowlisted action.");
        }

        foreach (var action in actions)
        {
            if (!AutomationActionTypes.Catalog.Contains(action.Type, StringComparer.OrdinalIgnoreCase))
            {
                throw DomainException.Validation("workflow_action_invalid", $"Action '{action.Type}' is not allowlisted.");
            }
        }
    }

    public static bool EvaluateConditions(IReadOnlyList<AutomationConditionDto> conditions, JsonElement payload)
    {
        foreach (var condition in conditions)
        {
            if (!payload.TryGetProperty(condition.Field, out var value) &&
                !TryGetIgnoreCase(payload, condition.Field, out value))
            {
                if (condition.Op.Equals("exists", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (condition.Op.Equals("neq", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return false;
            }

            var text = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? "",
                JsonValueKind.Number => value.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "",
                _ => value.ToString()
            };

            switch (condition.Op.ToLowerInvariant())
            {
                case "eq":
                    if (!string.Equals(text, condition.Value, StringComparison.OrdinalIgnoreCase)) return false;
                    break;
                case "neq":
                    if (string.Equals(text, condition.Value, StringComparison.OrdinalIgnoreCase)) return false;
                    break;
                case "contains":
                    if (!text.Contains(condition.Value, StringComparison.OrdinalIgnoreCase)) return false;
                    break;
                case "exists":
                    break;
                case "gte":
                    if (!decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var leftGte) ||
                        !decimal.TryParse(condition.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var rightGte) ||
                        leftGte < rightGte) return false;
                    break;
                case "lte":
                    if (!decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var leftLte) ||
                        !decimal.TryParse(condition.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var rightLte) ||
                        leftLte > rightLte) return false;
                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    private static bool TryGetIgnoreCase(JsonElement payload, string field, out JsonElement value)
    {
        foreach (var prop in payload.EnumerateObject())
        {
            if (string.Equals(prop.Name, field, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static IReadOnlyList<AutomationConditionDto> DeserializeConditions(string json) =>
        JsonSerializer.Deserialize<List<AutomationConditionDto>>(json, JsonOptions) ?? [];

    private static IReadOnlyList<AutomationActionDto> DeserializeActions(string json) =>
        JsonSerializer.Deserialize<List<AutomationActionDto>>(json, JsonOptions) ?? [];

    private static string Render(string template, JsonElement payload)
    {
        var result = template;
        foreach (var prop in payload.EnumerateObject())
        {
            result = result.Replace($"{{{prop.Name}}}", prop.Value.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static Guid? ReadGuid(JsonElement payload, string name)
    {
        if (!TryGetIgnoreCase(payload, name, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id) ? id : null;
    }

    private static bool IsEssential(string eventType) =>
        eventType is AutomationTriggerTypes.AppointmentConfirmed
            or AutomationTriggerTypes.AppointmentCancelled
            or AutomationTriggerTypes.PaymentFailed
            or AutomationTriggerTypes.PaymentCompleted
            or AutomationTriggerTypes.SupportTicketStatusChanged;
}
