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
import * as SystemUI from 'expo-system-ui';
import { useCallback, useEffect, useRef, useState } from 'react';
import { AppState, Platform, StyleSheet, View } from 'react-native';
import { GestureHandlerRootView } from 'react-native-gesture-handler';
import { KeyboardProvider } from 'react-native-keyboard-controller';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import { PremiumSplashScreen } from '@/components/branding/premium-splash-screen';
import { GlobalErrorBoundary } from '@/components/errors/global-error-boundary';
import { ToastHost } from '@/components/feedback/toast-host';
import { installAuthBridge, refreshSession } from '@/services/api/client';
import { bootstrapMobileRuntime, bindReactQueryToAppState } from '@/services/mobile-runtime';
import { useNotificationRealtime } from '@/hooks/use-notification-realtime';
import { navigateFromDeepLink, navigateFromNotification } from '@/services/notifications/deep-links';
import { registerPushDevice } from '@/services/notifications/push';
import { clearSensitiveCaches } from '@/services/offline/secure-cache';
import { notifyError } from '@/stores/feedback-store';
import { clearSession, hydrateSession, persistSession, useSessionStore } from '@/stores/session-store';
import { ProDocThemeProvider, useProdocTheme } from '@/theme/prodoc-theme';
import { splashPalette } from '@/theme/tokens';
import { useUiStore } from '@/stores/ui-store';
import { installUnhandledRejectionGuard } from '@/utils/unhandled-rejection';
import { classifyApiError, problemMessage } from '@/utils/problem';

/** Hold branded splash briefly for a polished launch; never exceed the max wait. */
const MIN_BRANDED_SPLASH_MS = 5500;
const MAX_BRANDED_SPLASH_MS = 10000;

SplashScreen.preventAutoHideAsync().catch(() => undefined);
void SystemUI.setBackgroundColorAsync(splashPalette.deep);
try {
  SplashScreen.setOptions({ duration: 320, fade: true });
} catch {
  // Older runtimes may not support setOptions — native hide still works.
}

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      // Prefer cache + event-driven invalidation over automatic remount/focus refetch.
      staleTime: 60_000,
      gcTime: 30 * 60_000,
      refetchOnWindowFocus: false,
      // NetInfo flaps must not mass-refetch every query (causes global blink).
      refetchOnReconnect: false,
      // Remount only refetch when data is past staleTime (not on every navigation).
      refetchOnMount: true,
      // Keep previous result visible while a background refetch runs.
      placeholderData: (previousData: unknown) => previousData,
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
  const language = useUiStore((state) => state.language);
  const [runtimeReady, setRuntimeReady] = useState(false);
  const [showBrandedSplash, setShowBrandedSplash] = useState(true);
  const nativeHidden = useRef(false);
  const splashStartedAt = useRef(Date.now());

  const initReady = (fontsLoaded || Boolean(fontError)) && hydrated && runtimeReady;

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
    const uninstallRejectionGuard = installUnhandledRejectionGuard((reason) => {
      // Surface unexpected promise failures without toasting cancelled requests.
      if (classifyApiError(reason) === 'cancelled') {
        return;
      }
      notifyError(problemMessage(reason, 'Something went wrong. Please try again.'));
    });
    void hydrateSession();
    return () => uninstallRejectionGuard();
  }, []);

  useEffect(() => {
    let cleanupRuntime: (() => void) | undefined;
    const cleanupFocus = bindReactQueryToAppState();
    void bootstrapMobileRuntime(queryClient)
      .then((cleanup) => {
        cleanupRuntime = cleanup;
        setRuntimeReady(true);
      })
      .catch(() => {
        // Enter the app so slow/offline networks are not stuck on splash forever.
        setRuntimeReady(true);
      });
    return () => {
      cleanupFocus();
      cleanupRuntime?.();
    };
  }, []);

  // Enforce min branded duration; force continue by max even if init is slow.
  useEffect(() => {
    let cancelled = false;

    const finishIfAllowed = () => {
      if (cancelled || !showBrandedSplash) {
        return;
      }
      const elapsed = Date.now() - splashStartedAt.current;
      if (initReady && elapsed >= MIN_BRANDED_SPLASH_MS) {
        setShowBrandedSplash(false);
        return;
      }
      if (elapsed >= MAX_BRANDED_SPLASH_MS) {
        setShowBrandedSplash(false);
      }
    };

    finishIfAllowed();
    const poll = setInterval(finishIfAllowed, 200);
    const maxTimer = setTimeout(() => {
      setRuntimeReady(true);
      finishIfAllowed();
    }, MAX_BRANDED_SPLASH_MS);

    return () => {
      cancelled = true;
      clearInterval(poll);
      clearTimeout(maxTimer);
    };
  }, [initReady, showBrandedSplash]);

  const hideNativeSplash = useCallback(() => {
    if (nativeHidden.current) {
      return;
    }
    nativeHidden.current = true;
    void SplashScreen.hideAsync().catch(() => undefined);
  }, []);

  const splashCopy =
    language === 'hi'
      ? {
          tagline: 'आपका स्वास्थ्य, हमारी प्राथमिकता',
          message: 'आपका स्वास्थ्य अनुभव तैयार हो रहा है...',
        }
      : {
          tagline: 'Your Health, Our Priority',
          message: 'Preparing your healthcare experience...',
        };

  return (
    <GestureHandlerRootView style={styles.fill}>
      <SafeAreaProvider>
        <KeyboardProvider
          statusBarTranslucent={false}
          // Web uses JS stubs; keep provider mounted for a single tree shape.
          enabled={Platform.OS !== 'web'}>
          <QueryClientProvider client={queryClient}>
            <ProDocThemeProvider>
              <GlobalErrorBoundary
                onReset={() => {
                  queryClient.clear();
                }}>
                <View style={styles.fill}>
                  {showBrandedSplash ? (
                    <PremiumSplashScreen
                      onFirstFrame={hideNativeSplash}
                      tagline={splashCopy.tagline}
                      message={splashCopy.message}
                    />
                  ) : (
                    <RootStack />
                  )}
                  <ToastHost />
                </View>
              </GlobalErrorBoundary>
            </ProDocThemeProvider>
          </QueryClientProvider>
        </KeyboardProvider>
      </SafeAreaProvider>
    </GestureHandlerRootView>
  );
}

function RootStack() {
  const theme = useProdocTheme();
  const router = useRouter();
  const accessToken = useSessionStore((state) => state.accessToken);
  const handledUrl = useRef<string | null>(null);
  useNotificationRealtime(Boolean(accessToken));

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
    let lastRefreshAt = 0;
    const sub = AppState.addEventListener('change', (status) => {
      if (status !== 'active') {
        return;
      }
      const refreshToken = useSessionStore.getState().refreshToken;
      if (!refreshToken) {
        return;
      }
      // Throttle foreground session refresh — avoid store churn / tree re-renders every resume.
      const now = Date.now();
      if (now - lastRefreshAt < 5 * 60_000) {
        return;
      }
      lastRefreshAt = now;
      void refreshSession(refreshToken)
        .then((session) => {
          const current = useSessionStore.getState();
          if (
            current.accessToken === session.accessToken &&
            current.refreshToken === session.refreshToken
          ) {
            return;
          }
          return persistSession(session);
        })
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
        fullScreenGestureEnabled: !theme.reduceMotion,
        animationTypeForReplace: 'push',
      }}
    />
  );
}

const styles = StyleSheet.create({
  fill: {
    flex: 1,
    backgroundColor: splashPalette.deep,
  },
});
