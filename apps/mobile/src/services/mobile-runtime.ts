import { onlineManager, focusManager, type QueryClient } from '@tanstack/react-query';
import { AppState, type AppStateStatus, Platform } from 'react-native';
import * as Notifications from 'expo-notifications';

import { attachQueryPersistence, hydrateOfflineQueryCache } from '@/services/offline/query-cache';
import { listOfflineDrafts } from '@/services/offline/offline-queue';
import { startNetworkMonitoring, useNetworkStore, selectIsEffectivelyOnline } from '@/stores/network-store';
import { registerPushDevice } from '@/services/notifications/push';
import { useSessionStore } from '@/stores/session-store';

export function bindReactQueryToAppState() {
  if (Platform.OS === 'web') {
    return () => undefined;
  }

  const onChange = (status: AppStateStatus) => {
    focusManager.setFocused(status === 'active');
  };
  const sub = AppState.addEventListener('change', onChange);
  return () => sub.remove();
}

export function bindReactQueryToNetwork() {
  onlineManager.setEventListener((setOnline) => {
    const stop = startNetworkMonitoring();
    const unsub = useNetworkStore.subscribe((state) => {
      setOnline(selectIsEffectivelyOnline(state));
    });
    return () => {
      stop();
      unsub();
    };
  });
}

export async function bootstrapMobileRuntime(client: QueryClient) {
  bindReactQueryToNetwork();
  const detachPersist = attachQueryPersistence(client);
  await hydrateOfflineQueryCache(client);

  const accessToken = useSessionStore.getState().accessToken;
  if (accessToken) {
    void registerPushDevice(true);
  }

  const drafts = await listOfflineDrafts();
  if (drafts.length && selectIsEffectivelyOnline(useNetworkStore.getState())) {
    // Drafts remain local until the feature screens flush them intentionally.
  }

  return () => {
    detachPersist();
  };
}

export function attachNotificationResponseLogger() {
  return Notifications.addNotificationResponseReceivedListener(() => {
    // Navigation is handled by the root layout using deep-link helpers.
  });
}
