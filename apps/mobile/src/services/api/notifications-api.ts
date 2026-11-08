import axios from 'axios';

import { api, refreshSession } from '@/services/api/client';
import { persistSession, useSessionStore } from '@/stores/session-store';

async function withAuthRetry<T>(run: () => Promise<T>): Promise<T> {
  try {
    return await run();
  } catch (error) {
    if (!axios.isAxiosError(error) || error.response?.status !== 403) {
      throw error;
    }
    const refreshToken = useSessionStore.getState().refreshToken;
    if (!refreshToken) {
      throw error;
    }
    await persistSession(await refreshSession(refreshToken));
    return run();
  }
}

export type AppNotification = {
  id: string;
  kind: string;
  title: string;
  body: string;
  createdAtUtc: string;
  isRead: boolean;
  subjectId: string | null;
  deepLink: string;
};

export type NotificationUnreadCount = {
  count: number;
};

export async function listMyNotifications(params?: {
  unreadOnly?: boolean;
  skip?: number;
  take?: number;
}): Promise<AppNotification[]> {
  return withAuthRetry(async () => {
    const response = await api.get<AppNotification[]>('/api/v1/notifications/me', {
      params: {
        unreadOnly: params?.unreadOnly,
        skip: params?.skip ?? 0,
        take: params?.take ?? 40,
      },
    });
    return response.data;
  });
}

export async function getNotificationUnreadCount(): Promise<number> {
  return withAuthRetry(async () => {
    const response = await api.get<NotificationUnreadCount>('/api/v1/notifications/me/unread-count');
    return response.data.count;
  });
}

export async function markNotificationRead(notificationId: string): Promise<void> {
  await withAuthRetry(async () => {
    await api.post(`/api/v1/notifications/me/${notificationId}/read`);
  });
}

export async function markAllNotificationsRead(): Promise<void> {
  await withAuthRetry(async () => {
    await api.post('/api/v1/notifications/me/read-all');
  });
}

export async function deleteNotification(notificationId: string): Promise<void> {
  await withAuthRetry(async () => {
    await api.delete(`/api/v1/notifications/me/${notificationId}`);
  });
}
