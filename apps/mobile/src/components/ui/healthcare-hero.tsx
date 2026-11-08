import { Image } from 'expo-image';
import { useWindowDimensions, View } from 'react-native';
import Svg, { Circle, Ellipse, Path, Rect } from 'react-native-svg';

import { useProdocTheme } from '@/theme/prodoc-theme';

const loginHero = require('../../../assets/images/login-healthcare-hero.jpg');

type HealthcareHeroProps = {
  height?: number;
  /** Prefer the photographic/illustration asset on auth screens. */
  variant?: 'illustration' | 'vector';
};

/** Healthcare visual for auth and marketing surfaces. */
export function HealthcareHero({ height = 148, variant = 'illustration' }: HealthcareHeroProps) {
  const theme = useProdocTheme();
  const { width: screenWidth } = useWindowDimensions();
  const maxWidth = Math.min(screenWidth - 48, 320);
  const imageHeight = Math.min(height, 160);

  if (variant === 'illustration') {
    return (
      <View
        accessible={false}
        style={{
          width: '100%',
          maxWidth,
          height: imageHeight,
          alignSelf: 'center',
          borderRadius: 20,
          overflow: 'hidden',
          backgroundColor: theme.colors.primaryLight,
        }}>
        <Image
          source={loginHero}
          style={{ width: '100%', height: '100%' }}
          contentFit="contain"
          transition={180}
          accessibilityIgnoresInvertColors
        />
      </View>
    );
  }

  const width = Math.round(height * 1.35);

  return (
    <View style={{ alignItems: 'center', justifyContent: 'center' }} accessible={false}>
      <Svg width={width} height={height} viewBox="0 0 270 200">
        <Ellipse cx="135" cy="168" rx="88" ry="14" fill={theme.colors.primaryLight} />
        <Circle cx="135" cy="78" r="62" fill={theme.colors.softBlue} />
        <Path d="M88 148c8-34 28-48 47-48s39 14 47 48" fill={theme.colors.primary} />
        <Circle cx="135" cy="72" r="28" fill="#F5D0B5" />
        <Path d="M110 58c8-18 42-18 50 0-6 10-14 14-25 14s-19-4-25-14z" fill={theme.colors.brandEnd} />
        <Rect x="122" y="96" width="26" height="34" rx="6" fill="#FFFFFF" opacity={0.92} />
        <Path d="M135 104v18M126 113h18" stroke={theme.colors.primary} strokeWidth={3} strokeLinecap="round" />
        <Circle cx="198" cy="54" r="18" fill={theme.colors.primaryLight} />
        <Path d="M198 46v16M190 54h16" stroke={theme.colors.primary} strokeWidth={2.5} strokeLinecap="round" />
        <Circle cx="72" cy="62" r="10" fill={theme.colors.softBlue} />
        <Circle cx="214" cy="118" r="7" fill={theme.colors.primaryLight} />
      </Svg>
    </View>
  );
}
