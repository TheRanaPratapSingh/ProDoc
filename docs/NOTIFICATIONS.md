# ProDoc notifications

Centralized in-app notifications with transactional outbox processing, SignalR realtime delivery, reminder scheduling, and Expo push registration.

## Architecture

1. Booking / cancel / reschedule / check-in / payment / schedule-leave succeed in their domain transaction.
2. An `AutomationOutboxEvent` is written with an idempotency key (transactional outbox).
3. `AutomationOutboxProcessor` claims due events and `AutomationEngine` fans out to authorized recipients via `NotificationService`.
4. Rows are stored in `UserNotifications` (history + unread badge).
5. Connected clients receive `notification.created` / `notification.unread` on `/hubs/notifications`.
6. Optional Expo push is attempted after in-app persistence; push failure never rolls back the appointment.

## APIs

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/v1/notifications/me` | Paginated inbox (`unreadOnly`, `skip`, `take`) |
| GET | `/api/v1/notifications/me/unread-count` | Unread badge count |
| POST | `/api/v1/notifications/me/{id}/read` | Mark one read |
| POST | `/api/v1/notifications/me/read-all` | Mark all read |
| DELETE | `/api/v1/notifications/me/{id}` | Delete own notification |
| GET/PUT | `/api/v1/account/notification-preferences` | Channel preferences |
| POST/DELETE | `/api/v1/devices/push` | Register / clear Expo push token |

Realtime hub: `/hubs/notifications` (JWT required). Clients join `notify:user:{userId}` on connect.

## Appointment recipient mapping

| Event | Patient | Doctor | Reception (clinic/doctor scoped) | Clinic owner |
|---|---|---|---|---|
| Confirmed / created | Yes | Yes | Assigned receptionists | Yes |
| Cancelled | Yes | Yes | Assigned receptionists | Yes |
| Rescheduled | Yes | Yes | Assigned receptionists | Yes |
| Reminder due | Yes (party fan-out) | Yes | Yes | Yes |
| Checked in | Yes | Yes | Assigned receptionists | Yes |
| Schedule leave / closure | Affected patients only | Yes | Reception staff | — |
| Payment completed/failed | Payer | — | — | — |

Receptionists are filtered by active clinic membership, optional branch coverage, and assigned-doctor lists. Private appointment details are never broadcast to unrelated staff.

## Reminders

Default offsets relative to appointment `startsAtUtc` (clinic timezone Asia/Kolkata for display):

- 24 hours before
- 2 hours before

Reminders are cancelled when an appointment is cancelled or rescheduled, then re-scheduled for the new start. Cancelled / rejected / expired visits never receive reminders. Preference `AppointmentRemindersEnabled` gates reminder delivery.

## Push configuration (remaining)

In-app + SignalR are production-ready. Expo push registration and `DevelopmentPushSender` are wired, but production delivery needs:

1. EAS project id in `app.json` / `extra.eas.projectId`.
2. Expo push credentials for Android (FCM) and iOS (APNs) via EAS credentials.
3. Replace `DevelopmentPushSender` with an Expo Push API sender (or FCM/APNs) registered in DI for non-Development environments.
4. Validate end-to-end on physical devices; lock-screen copy stays sanitized (no diagnoses / Rx / amounts).

Until those credentials are configured, treat push as best-effort and rely on in-app history + SignalR.

## Mobile UI

- Shared `NotificationBell` (unread badge from API) on patient, doctor, reception, clinic, and admin shells.
- Shared `NotificationCenter` with All / Unread / Read filters, Today / Yesterday / Earlier grouping, mark-read, mark-all-read, delete, and lazy load.
- SignalR client: `@microsoft/signalr` via `useNotificationRealtime` — invalidates badge/list without full app refresh.
