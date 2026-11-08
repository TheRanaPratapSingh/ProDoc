import { View } from 'react-native';
import Svg, { Circle, Path } from 'react-native-svg';

import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';

/** Compact ProDoc brand mark — sized for auth headers without clipping. */
export function ProdocLogo({ size = 40, showWordmark = true }: { size?: number; showWordmark?: boolean }) {
  const theme = useProdocTheme();
  const copy = useCopy();
  const mark = Math.round(size);

  return (
    <View
      accessible
      accessibilityRole="header"
      accessibilityLabel={copy.brand}
      style={{
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'center',
        gap: spacing.sm,
        paddingVertical: 2,
      }}>
      <Svg width={mark} height={mark} viewBox="0 0 48 48">
        <Circle cx="24" cy="24" r="24" fill={theme.colors.primary} />
        <Circle cx="24" cy="24" r="18" fill={theme.colors.primaryLight} />
        <Path
          d="M24 12c-1.1 0-2 .9-2 2v8h-8c-1.1 0-2 .9-2 2s.9 2 2 2h8v8c0 1.1.9 2 2 2s2-.9 2-2v-8h8c1.1 0 2-.9 2-2s-.9-2-2-2h-8v-8c0-1.1-.9-2-2-2z"
          fill={theme.colors.primary}
        />
      </Svg>
      {showWordmark ? (
        <AppText
          variant="headline"
          style={{
            color: theme.colors.primary,
            fontSize: Math.max(22, Math.round(size * 0.58)),
            lineHeight: Math.max(28, Math.round(size * 0.72)),
            letterSpacing: -0.3,
          }}>
          {copy.brand}
        </AppText>
      ) : null}
    </View>
  );
}
