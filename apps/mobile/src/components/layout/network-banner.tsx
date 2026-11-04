import { View } from 'react-native';

import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { selectIsEffectivelyOnline, useNetworkStore } from '@/stores/network-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';

export function NetworkBanner() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const online = useNetworkStore(selectIsEffectivelyOnline);

  if (online) {
    return null;
  }

  return (
    <View
      accessibilityRole="alert"
      accessibilityLiveRegion="polite"
      style={{
        backgroundColor: theme.colors.warning,
        paddingHorizontal: spacing.lg,
        paddingVertical: spacing.sm,
      }}>
      <AppText variant="label" style={{ color: theme.scheme === 'dark' ? '#0B1220' : '#FFFFFF' }}>
        {copy.offlineBanner}
      </AppText>
      <AppText variant="muted" style={{ color: theme.scheme === 'dark' ? '#0B1220' : '#FFFFFF' }}>
        {copy.offlineBannerBody}
      </AppText>
    </View>
  );
}
