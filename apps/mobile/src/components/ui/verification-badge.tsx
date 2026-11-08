import { View } from 'react-native';
import Svg, { Path } from 'react-native-svg';

import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';

function CheckIcon({ color, size = 12 }: { color: string; size?: number }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Path
        d="M5 12.5l4.2 4.2L19 7"
        stroke={color}
        strokeWidth={2.2}
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </Svg>
  );
}

/** Compact verified / pending chip used on doctor & clinic cards. */
export function VerificationBadge({
  label,
  verified = true,
}: {
  label: string;
  verified?: boolean;
}) {
  const theme = useProdocTheme();
  const fg = verified ? theme.colors.success : theme.colors.warning;
  const bg =
    theme.scheme === 'light'
      ? verified
        ? '#E6F4EC'
        : '#FBF3E6'
      : verified
        ? '#1A3328'
        : '#332818';

  return (
    <View
      style={{
        alignSelf: 'flex-start',
        flexDirection: 'row',
        alignItems: 'center',
        gap: 4,
        backgroundColor: bg,
        borderRadius: radius.pill,
        paddingHorizontal: spacing.sm,
        paddingVertical: 3,
      }}>
      {verified ? <CheckIcon color={fg} /> : null}
      <AppText variant="caption" style={{ color: fg }}>
        {label}
      </AppText>
    </View>
  );
}
