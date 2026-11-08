import { useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';

import {
  connectNotificationHub,
  disconnectNotificationHub,
} from '@/services/notifications/notification-hub';
import { useSessionStore } from '@/stores/session-store';

/** Keeps unread badge + notification lists fresh via SignalR without full-screen refresh. */
export function useNotificationRealtime(enabled = true) {
  const accessToken = useSessionStore((state) => state.accessToken);
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!enabled || !accessToken) {
      void disconnectNotificationHub();
      return;
    }

    void connectNotificationHub(accessToken, {
      onCreated: () => {
        void queryClient.invalidateQueries({ queryKey: ['notifications'] });
      },
      onUnread: (payload) => {
        if (typeof payload?.unreadCount === 'number') {
          queryClient.setQueryData(['notifications', 'unread-count'], payload.unreadCount);
        } else {
          void queryClient.invalidateQueries({ queryKey: ['notifications', 'unread-count'] });
        }
        void queryClient.invalidateQueries({ queryKey: ['notifications', 'list'] });
      },
    });

    return () => {
      void disconnectNotificationHub();
    };
  }, [accessToken, enabled, queryClient]);
}
