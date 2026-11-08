import { View } from 'react-native';
import Svg, { Circle, Path } from 'react-native-svg';

import { useProdocTheme } from '@/theme/prodoc-theme';

/** Green shield-check mark used on OTP verification (matches reference). */
export function OtpShield({ size = 88 }: { size?: number }) {
  const theme = useProdocTheme();

  return (
    <View style={{ alignItems: 'center', justifyContent: 'center' }} accessible={false}>
      <Svg width={size} height={size} viewBox="0 0 96 96">
        <Circle cx="48" cy="48" r="46" fill={theme.colors.primaryLight} />
        <Path
          d="M48 14c12 6 24 8 30 9v22c0 18-12 30-30 37-18-7-30-19-30-37V23c6-1 18-3 30-9z"
          fill={theme.colors.primary}
        />
        <Path
          d="M34 48l10 10 20-22"
          stroke={theme.colors.onPrimary}
          strokeWidth={5}
          strokeLinecap="round"
          strokeLinejoin="round"
          fill="none"
        />
      </Svg>
    </View>
  );
}
