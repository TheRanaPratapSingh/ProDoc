import { useRouter } from 'expo-router';
import { useEffect } from 'react';
import { ActivityIndicator, View } from 'react-native';

import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';

export default function IndexScreen() {
  const router = useRouter();
  const theme = useProdocTheme();
  const copy = useCopy();
  const hydrated = useSessionStore((state) => state.hydrated);
  const accessToken = useSessionStore((state) => state.accessToken);

  useEffect(() => {
    if (!hydrated) {
      return;
    }

    router.replace(accessToken ? '/home' : '/sign-in');
  }, [accessToken, hydrated, router]);

  return (
    <View
      style={{
        flex: 1,
        alignItems: 'center',
        justifyContent: 'center',
        gap: 12,
        backgroundColor: theme.colors.background,
      }}>
      <ActivityIndicator color={theme.colors.primary} />
      <AppText variant="muted">{copy.loading}</AppText>
    </View>
  );
}
