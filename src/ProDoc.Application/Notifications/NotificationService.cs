using System.Globalization;
using Microsoft.Extensions.Logging;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Mobile;
using ProDoc.Application.Providers;
using ProDoc.Application.Queue;
using ProDoc.Domain.Automation;
using ProDoc.Domain.Common;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Queue;

namespace ProDoc.Application.Notifications;

/// <summary>
/// Centralized in-app (+ optional push) notifications for all portals.
/// Appointment fan-out is idempotent and never fails the originating business transaction.
/// </summary>
public sealed class NotificationService(
    INotificationStore notifications,
    IQueueStore queue,
    IProviderStore providers,
    IMobileStore mobile,
    IPushNotificationSender push,
    INotificationRealtime realtime,
    IClock clock,
    ILogger<NotificationService> logger)
{
    public async Task<IReadOnlyList<NotificationInboxItem>> ListMineAsync(
        Guid userId,
        bool? unreadOnly,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        take = Math.Clamp(take, 1, 100);
        skip = Math.Max(0, skip);
        var rows = await notifications.ListForUserAsync(userId, unreadOnly, skip, take, cancellationToken);
        return rows.Select(Map).ToArray();
    }

    public async Task<NotificationUnreadCount> UnreadCountAsync(Guid userId, CancellationToken cancellationToken) =>
        new(await notifications.CountUnreadAsync(userId, cancellationToken));

    public async Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken)
    {
        var row = await notifications.FindAsync(notificationId, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("notification_not_found", "That notification was not found.");
        if (row.UserId != userId)
        {
            throw DomainException.NotFound("notification_not_found", "That notification was not found.");
        }

        row.MarkRead(clock.UtcNow);
        await notifications.SaveChangesAsync(cancellationToken);
        await PublishUnreadAsync(userId, cancellationToken);
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await notifications.ListUnreadTrackedAsync(userId, cancellationToken);
        var now = clock.UtcNow;
        foreach (var row in rows)
        {
            row.MarkRead(now);
        }

        await notifications.SaveChangesAsync(cancellationToken);
        await PublishUnreadAsync(userId, cancellationToken);
    }

    public async Task DeleteAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken)
    {
        var row = await notifications.FindAsync(notificationId, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("notification_not_found", "That notification was not found.");
        if (row.UserId != userId)
        {
            throw DomainException.NotFound("notification_not_found", "That notification was not found.");
        }

        notifications.Remove(row);
        await notifications.SaveChangesAsync(cancellationToken);
        await PublishUnreadAsync(userId, cancellationToken);
    }

    public async Task NotifyAppointmentPartiesAsync(
        string eventType,
        AppointmentPartyNotificationContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var recipients = await ResolveRecipientsAsync(context, cancellationToken);
            foreach (var recipient in recipients)
            {
                var (kind, title, body) = Compose(eventType, recipient.Role, context);
                await DeliverAsync(recipient.UserId, kind, context.AppointmentId, title, body, essential: true, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to fan-out {EventType} notifications for appointment {AppointmentId}",
                eventType,
                context.AppointmentId);
        }
    }

    public async Task NotifyUsersAsync(
        IEnumerable<Guid> userIds,
        string kind,
        Guid? subjectId,
        string title,
        string body,
        bool essential,
        CancellationToken cancellationToken)
    {
        foreach (var userId in userIds.Distinct())
        {
            try
            {
                await DeliverAsync(userId, kind, subjectId, title, body, essential, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to notify user {UserId} for {Kind}", userId, kind);
            }
        }
    }

    private async Task DeliverAsync(
        Guid userId,
        string kind,
        Guid? subjectId,
        string title,
        string body,
        bool essential,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return;
        }

        var prefs = await notifications.FindPreferenceAsync(userId, tracking: false, cancellationToken)
            ?? UserNotificationPreference.CreateDefault(userId, clock.UtcNow);

        var since = clock.UtcNow.AddMinutes(-10);
        if (await notifications.HasRecentAsync(userId, kind, subjectId, since, cancellationToken))
        {
            return;
        }

        var isReminder = kind.Contains("reminder", StringComparison.OrdinalIgnoreCase);
        if (isReminder && !prefs.AppointmentRemindersEnabled)
        {
            return;
        }

        if (essential || prefs.InAppEnabled)
        {
            var notification = UserNotification.Create(userId, kind, subjectId, title, body, clock.UtcNow);
            notifications.Add(notification);
            await notifications.SaveChangesAsync(cancellationToken);

            try
            {
                await realtime.PublishToUserAsync(
                    userId,
                    "notification.created",
                    new
                    {
                        id = notification.Id,
                        kind = notification.Kind,
                        title = notification.Title,
                        body = notification.Body,
                        subjectId = notification.SubjectId,
                        createdAtUtc = notification.CreatedAtUtc,
                        deepLink = NotificationDeepLinks.Resolve(notification.Kind, notification.SubjectId),
                        unreadCount = await notifications.CountUnreadAsync(userId, cancellationToken)
                    },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "SignalR notify failed for {UserId}", userId);
            }
        }

        if (prefs.PushEnabled && (essential || prefs.PushEnabled))
        {
            try
            {
                var devices = await mobile.ListActiveForUserAsync(userId, cancellationToken);
                if (devices.Count > 0)
                {
                    var (safeTitle, safeBody) = NotificationDeepLinks.SanitizeForPush(kind, title);
                    await push.SendAsync(
                        devices.Select(d => d.Token).ToArray(),
                        safeTitle,
                        safeBody,
                        new Dictionary<string, string>
                        {
                            ["kind"] = kind,
                            ["subjectId"] = subjectId?.ToString() ?? string.Empty
                        },
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Push notify failed for {UserId}", userId);
            }
        }
    }

    private async Task PublishUnreadAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var count = await notifications.CountUnreadAsync(userId, cancellationToken);
            await realtime.PublishToUserAsync(userId, "notification.unread", new { unreadCount = count }, cancellationToken);
        }
        catch
        {
            // Non-fatal.
        }
    }

    private async Task<IReadOnlyList<(Guid UserId, string Role)>> ResolveRecipientsAsync(
        AppointmentPartyNotificationContext context,
        CancellationToken cancellationToken)
    {
        var recipients = new List<(Guid UserId, string Role)>();
        if (context.PatientUserId != Guid.Empty)
        {
            recipients.Add((context.PatientUserId, RoleNames.Patient));
        }

        var doctor = await providers.FindDoctorAsync(context.DoctorId, tracking: false, cancellationToken);
        if (doctor?.UserId is Guid doctorUserId && doctorUserId != Guid.Empty)
        {
            recipients.Add((doctorUserId, RoleNames.Doctor));
        }

        var clinic = await providers.FindClinicAsync(context.ClinicId, tracking: false, cancellationToken);
        if (clinic?.OwnerUserId is Guid ownerId && ownerId != Guid.Empty && recipients.All(r => r.UserId != ownerId))
        {
            recipients.Add((ownerId, RoleNames.ClinicAdmin));
        }

        var staff = await queue.ListStaffAsync(context.ClinicId, cancellationToken);
        foreach (var member in staff.Where(item => item.IsActive && item.Role == RoleNames.Receptionist))
        {
            if (!member.CoversBranch(context.BranchId))
            {
                continue;
            }

            var assigned = await queue.ListReceptionistAssignedDoctorIdsAsync(member.UserId, context.ClinicId, cancellationToken);
            if (assigned.Count > 0 && !assigned.Contains(context.DoctorId))
            {
                continue;
            }

            recipients.Add((member.UserId, RoleNames.Receptionist));
        }

        return recipients
            .GroupBy(item => item.UserId)
            .Select(group => group.First())
            .ToArray();
    }

    private static (string Kind, string Title, string Body) Compose(
        string eventType,
        string role,
        AppointmentPartyNotificationContext context)
    {
        var when = FormatWhen(context);
        var token = string.IsNullOrWhiteSpace(context.TokenLabel) ? string.Empty : $" Token {context.TokenLabel}.";
        var reference = string.IsNullOrWhiteSpace(context.Reference) ? string.Empty : $" Ref {context.Reference}.";

        if (eventType is AutomationTriggerTypes.AppointmentConfirmed or AutomationTriggerTypes.AppointmentCreated)
        {
            return role switch
            {
                RoleNames.Patient => (
                    NotificationKinds.AppointmentConfirmed,
                    "Appointment Confirmed",
                    $"Your appointment with {context.DoctorName} at {context.ClinicName} is booked for {when}.{token}{reference}"),
                RoleNames.Doctor => (
                    NotificationKinds.AppointmentConfirmed,
                    "New Appointment Received",
                    $"New appointment booked by {context.PatientName} for {when} at {context.ClinicName}.{token}{reference}"),
                _ => (
                    NotificationKinds.AppointmentConfirmed,
                    "New Patient Appointment",
                    $"{context.PatientName} booked with {context.DoctorName} for {when}.{token}{reference}")
            };
        }

        if (eventType == AutomationTriggerTypes.AppointmentCancelled)
        {
            var reason = string.IsNullOrWhiteSpace(context.CancellationReason)
                ? string.Empty
                : $" Reason: {context.CancellationReason.Trim()}.";
            return role switch
            {
                RoleNames.Patient => (
                    NotificationKinds.AppointmentCancelled,
                    "Appointment Cancelled",
                    $"Your appointment with {context.DoctorName} ({when}) was cancelled successfully.{reference}"),
                RoleNames.Doctor => (
                    NotificationKinds.AppointmentCancelled,
                    "Appointment Cancelled",
                    $"{context.PatientName} cancelled their appointment scheduled for {when}.{reason}{reference}"),
                _ => (
                    NotificationKinds.AppointmentCancelled,
                    "Appointment Cancellation Received",
                    $"{context.PatientName} cancelled with {context.DoctorName} ({when}).{reason}{reference}")
            };
        }

        if (eventType == AutomationTriggerTypes.AppointmentRescheduled)
        {
            var previous = context.PreviousStartsAtUtc is DateTimeOffset prev
                ? FormatInstant(prev, context.TimeZoneId)
                : "previous time";
            return role switch
            {
                RoleNames.Patient => (
                    NotificationKinds.AppointmentRescheduled,
                    "Appointment Rescheduled",
                    $"Your appointment with {context.DoctorName} moved from {previous} to {when}.{token}{reference}"),
                RoleNames.Doctor => (
                    NotificationKinds.AppointmentRescheduled,
                    "Appointment Rescheduled",
                    $"{context.PatientName} rescheduled from {previous} to {when}.{token}{reference}"),
                _ => (
                    NotificationKinds.AppointmentRescheduled,
                    "Appointment Rescheduled",
                    $"{context.PatientName} changed their appointment with {context.DoctorName} to {when}.{token}{reference}")
            };
        }

        if (eventType == AutomationTriggerTypes.AppointmentReminderDue)
        {
            return role switch
            {
                RoleNames.Patient => (
                    NotificationKinds.AppointmentReminder,
                    "Appointment Reminder",
                    $"Reminder: your appointment with {context.DoctorName} is scheduled for {when}.{token}"),
                RoleNames.Doctor => (
                    NotificationKinds.AppointmentReminder,
                    "Appointment Reminder",
                    $"Reminder: {context.PatientName} is scheduled for {when} at {context.ClinicName}.{token}"),
                _ => (
                    NotificationKinds.AppointmentReminder,
                    "Appointment Reminder",
                    $"Reminder: {context.PatientName} with {context.DoctorName} is scheduled for {when}.{token}")
            };
        }

        if (eventType == AutomationTriggerTypes.PatientCheckedIn)
        {
            return role switch
            {
                RoleNames.Patient => (
                    NotificationKinds.AppointmentCheckedIn,
                    "Checked In",
                    $"You are checked in for your visit with {context.DoctorName}.{token}"),
                RoleNames.Doctor => (
                    NotificationKinds.AppointmentCheckedIn,
                    "Patient Checked In",
                    $"{context.PatientName} checked in for {when}.{token}"),
                _ => (
                    NotificationKinds.AppointmentCheckedIn,
                    "Patient Checked In",
                    $"{context.PatientName} checked in with {context.DoctorName}.{token}")
            };
        }

        return (
            eventType,
            "ProDoc update",
            $"An update is available for your appointment on {when}.{reference}");
    }

    private static string FormatWhen(AppointmentPartyNotificationContext context)
    {
        var start = FormatInstant(context.StartsAtUtc, context.TimeZoneId);
        if (string.Equals(context.BookingMode, "HourlyQueue", StringComparison.OrdinalIgnoreCase))
        {
            var end = FormatInstant(context.EndsAtUtc, context.TimeZoneId, timeOnly: true);
            return $"{start}–{end}";
        }

        return start;
    }

    private static string FormatInstant(DateTimeOffset utc, string timeZoneId, bool timeOnly = false)
    {
        TimeZoneInfo zone;
        if (TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var resolved))
        {
            zone = resolved;
        }
        else if (TimeZoneInfo.TryFindSystemTimeZoneById("Asia/Kolkata", out var india)
                 || TimeZoneInfo.TryFindSystemTimeZoneById("India Standard Time", out india))
        {
            zone = india;
        }
        else
        {
            zone = TimeZoneInfo.Utc;
        }

        var local = TimeZoneInfo.ConvertTime(utc, zone);
        return timeOnly
            ? local.ToString("h:mm tt", CultureInfo.GetCultureInfo("en-IN"))
            : local.ToString("d MMM yyyy, h:mm tt", CultureInfo.GetCultureInfo("en-IN"));
    }

    private static NotificationInboxItem Map(UserNotification item) =>
        new(
            item.Id,
            item.Kind,
            item.Title,
            item.Body,
            item.CreatedAtUtc,
            item.ReadAtUtc is not null,
            item.SubjectId,
            NotificationDeepLinks.Resolve(item.Kind, item.SubjectId));
}
