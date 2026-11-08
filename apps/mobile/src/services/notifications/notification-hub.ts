import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

import { resolveApiBaseUrl } from '@/services/api/client';

let connection: HubConnection | null = null;
let startedForToken: string | null = null;

export type NotificationRealtimeHandlers = {
  onCreated?: (payload: unknown) => void;
  onUnread?: (payload: { unreadCount?: number }) => void;
};

export async function connectNotificationHub(
  accessToken: string,
  handlers: NotificationRealtimeHandlers,
): Promise<void> {
  if (!accessToken) {
    return;
  }

  if (connection && startedForToken === accessToken) {
    return;
  }

  await disconnectNotificationHub();

  const hub = new HubConnectionBuilder()
    .withUrl(`${resolveApiBaseUrl()}/hubs/notifications`, {
      accessTokenFactory: () => accessToken,
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(LogLevel.Warning)
    .build();

  if (handlers.onCreated) {
    hub.on('notification.created', handlers.onCreated);
  }
  if (handlers.onUnread) {
    hub.on('notification.unread', handlers.onUnread);
  }

  connection = hub;
  startedForToken = accessToken;
  try {
    await hub.start();
  } catch {
    connection = null;
    startedForToken = null;
  }
}

export async function disconnectNotificationHub(): Promise<void> {
  const current = connection;
  connection = null;
  startedForToken = null;
  if (!current) {
    return;
  }
  try {
    await current.stop();
  } catch {
    // Best-effort disconnect.
  }
}
