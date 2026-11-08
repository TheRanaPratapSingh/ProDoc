import { Redirect, useLocalSearchParams } from 'expo-router';
import { useEffect, useState } from 'react';
import { View } from 'react-native';

import { AppText } from '@/components/ui/text';
import { savePendingReferralCode } from '@/services/referral-intake';
import { useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';

export default function InviteCodeScreen() {
  const theme = useProdocTheme();
  const accessToken = useSessionStore((state) => state.accessToken);
  const { code } = useLocalSearchParams<{ code?: string }>();
  const [ready, setReady] = useState(false);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      if (typeof code === 'string' && code.trim()) {
        await savePendingReferralCode(code);
      }
      if (!cancelled) {
        setReady(true);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [code]);

  if (!ready) {
    return (
      <View style={{ flex: 1, justifyContent: 'center', padding: spacing.lg, backgroundColor: theme.colors.background }}>
        <AppText variant="muted">Opening your ProDoc invitation…</AppText>
      </View>
    );
  }

  return <Redirect href={accessToken ? '/rewards' : '/sign-in'} />;
}
