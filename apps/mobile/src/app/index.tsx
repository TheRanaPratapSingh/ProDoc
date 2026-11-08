import { useRouter, type Href } from 'expo-router';
import { useEffect, useRef } from 'react';
import { ActivityIndicator, View } from 'react-native';

import { useSessionStore } from '@/stores/session-store';
import { splashPalette } from '@/theme/tokens';
import { homeHrefForAccount } from '@/utils/home-surface';

/** Silent role-based redirect — Earth Green only, no white loading chrome. */
export default function IndexScreen() {
  const router = useRouter();
  const hydrated = useSessionStore((state) => state.hydrated);
  const accessToken = useSessionStore((state) => state.accessToken);
  const account = useSessionStore((state) => state.account);
  const activeSurface = useSessionStore((state) => state.activeSurface);
  const redirected = useRef<string | null>(null);

  useEffect(() => {
    if (!hydrated) {
      return;
    }

    const target = (accessToken ? homeHrefForAccount(account, activeSurface) : '/sign-in') as Href;
    const key = String(target);
    if (redirected.current === key) {
      return;
    }
    redirected.current = key;
    router.replace(target);
  }, [accessToken, account, activeSurface, hydrated, router]);

  return (
    <View
      style={{
        flex: 1,
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: splashPalette.deep,
      }}>
      <ActivityIndicator color={splashPalette.mint} />
    </View>
  );
}
