import 'react-native-gesture-handler';

import {
  PlusJakartaSans_400Regular,
  PlusJakartaSans_500Medium,
  PlusJakartaSans_600SemiBold,
  PlusJakartaSans_700Bold,
} from '@expo-google-fonts/plus-jakarta-sans';
import {
  NotoSansDevanagari_400Regular,
  NotoSansDevanagari_600SemiBold,
  NotoSansDevanagari_700Bold,
} from '@expo-google-fonts/noto-sans-devanagari';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Linking from 'expo-linking';
import * as Notifications from 'expo-notifications';
import { useFonts } from 'expo-font';
import { Stack, useRouter } from 'expo-router';
import * as SplashScreen from 'expo-splash-screen';
import { useEffect, useRef, useState } from 'react';
import { AppState } from 'react-native';

import { installAuthBridge, refreshSession } from '@/services/api/client';
import { bootstrapMobileRuntime, bindReactQueryToAppState } from '@/services/mobile-runtime';
import { navigateFromDeepLink, navigateFromNotification } from '@/services/notifications/deep-links';
import { registerPushDevice } from '@/services/notifications/push';
import { clearSensitiveCaches } from '@/services/offline/secure-cache';
import { clearSession, hydrateSession, persistSession, useSessionStore } from '@/stores/session-store';
import { ProDocThemeProvider, useProdocTheme } from '@/theme/prodoc-theme';

SplashScreen.preventAutoHideAsync();

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      staleTime: 30_000,
      gcTime: 30 * 60_000,
      refetchOnWindowFocus: false,
      networkMode: 'offlineFirst',
    },
    mutations: {
      networkMode: 'online',
      retry: 0,
    },
  },
});

export default function RootLayout() {
  const [fontsLoaded, fontError] = useFonts({
    PlusJakartaSans_400Regular,
    PlusJakartaSans_500Medium,
    PlusJakartaSans_600SemiBold,
    PlusJakartaSans_700Bold,
    NotoSansDevanagari_400Regular,
    NotoSansDevanagari_600SemiBold,
    NotoSansDevanagari_700Bold,
  });
  const hydrated = useSessionStore((state) => state.hydrated);
  const [runtimeReady, setRuntimeReady] = useState(false);

  useEffect(() => {
    installAuthBridge({
      getAccessToken: () => useSessionStore.getState().accessToken,
      getRefreshToken: () => useSessionStore.getState().refreshToken,
      onSession: (session) => {
        void persistSession(session);
      },
      onSignedOut: () => {
        void clearSensitiveCaches();
        void clearSession();
        queryClient.clear();
      },
    });
    void hydrateSession();
  }, []);

  useEffect(() => {
    let cleanupRuntime: (() => void) | undefined;
    const cleanupFocus = bindReactQueryToAppState();
    void bootstrapMobileRuntime(queryClient).then((cleanup) => {
      cleanupRuntime = cleanup;
      setRuntimeReady(true);
    });
    return () => {
      cleanupFocus();
      cleanupRuntime?.();
    };
  }, []);

  useEffect(() => {
    if ((fontsLoaded || fontError) && hydrated && runtimeReady) {
      void SplashScreen.hideAsync();
    }
  }, [fontError, fontsLoaded, hydrated, runtimeReady]);

  if ((!fontsLoaded && !fontError) || !hydrated || !runtimeReady) {
    return null;
  }

  return (
    <QueryClientProvider client={queryClient}>
      <ProDocThemeProvider>
        <RootStack />
      </ProDocThemeProvider>
    </QueryClientProvider>
  );
}

function RootStack() {
  const theme = useProdocTheme();
  const router = useRouter();
  const accessToken = useSessionStore((state) => state.accessToken);
  const handledUrl = useRef<string | null>(null);

  useEffect(() => {
    if (accessToken) {
      void registerPushDevice(true);
    }
  }, [accessToken]);

  useEffect(() => {
    const handleUrl = (url: string | null) => {
      if (!url || handledUrl.current === url) {
        return;
      }
      handledUrl.current = url;
      navigateFromDeepLink(router, url);
    };

    void Linking.getInitialURL().then(handleUrl);
    const sub = Linking.addEventListener('url', (event) => handleUrl(event.url));
    return () => sub.remove();
  }, [router]);

  useEffect(() => {
    const sub = Notifications.addNotificationResponseReceivedListener((response) => {
      const data = response.notification.request.content.data as
        | { kind?: string; subjectId?: string; path?: string }
        | undefined;
      navigateFromNotification(router, data);
    });
    return () => sub.remove();
  }, [router]);

  useEffect(() => {
    const sub = AppState.addEventListener('change', (status) => {
      if (status !== 'active') {
        return;
      }
      const refreshToken = useSessionStore.getState().refreshToken;
      if (!refreshToken) {
        return;
      }
      void refreshSession(refreshToken)
        .then((session) => persistSession(session))
        .catch(() => undefined);
    });
    return () => sub.remove();
  }, []);

  return (
    <Stack
      screenOptions={{
        headerShown: false,
        animation: theme.reduceMotion ? 'none' : 'slide_from_right',
        animationDuration: theme.reduceMotion ? 0 : 220,
        contentStyle: { backgroundColor: theme.colors.background },
        gestureEnabled: true,
      }}
    />
  );
}
