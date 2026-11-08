import { View } from 'react-native';
import Svg, { Circle, Path, Rect } from 'react-native-svg';

import { useProdocTheme } from '@/theme/prodoc-theme';

/** Calendar + check used on booking confirmation. */
export function BookingSuccessMark({ size = 96 }: { size?: number }) {
  const theme = useProdocTheme();

  return (
    <View style={{ alignItems: 'center' }} accessible={false}>
      <Svg width={size} height={size} viewBox="0 0 100 100">
        <Circle cx="50" cy="50" r="48" fill={theme.colors.primaryLight} />
        <Rect x="28" y="30" width="44" height="42" rx="6" fill={theme.colors.primary} />
        <Rect x="28" y="30" width="44" height="12" rx="6" fill={theme.colors.primaryPressed} />
        <Path
          d="M40 56l7 7 14-16"
          stroke={theme.colors.onPrimary}
          strokeWidth={4}
          strokeLinecap="round"
          strokeLinejoin="round"
          fill="none"
        />
      </Svg>
    </View>
  );
}
