import type { ImageSource } from 'expo-image';
import { type ReactNode } from 'react';
import { View, type StyleProp, type ViewStyle } from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';

import { OptimizedImage } from '@/components/ui/optimized-image';
import { PressableScale } from '@/components/ui/pressable-scale';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { motion, radius } from '@/theme/tokens';

/** Soft marketing / CTA banner with optional photo — Earth Green tint by default. */
export function ImageBanner({
  children,
  image,
  onPress,
  colors,
  style,
  imageWidth = 128,
  minHeight = 132,
}: {
  children: ReactNode;
  image?: ImageSource;
  onPress?: () => void;
  colors?: [string, string];
  style?: StyleProp<ViewStyle>;
  imageWidth?: number;
  minHeight?: number;
}) {
  const theme = useProdocTheme();
  const gradient = colors ?? [theme.colors.primaryLight, theme.scheme === 'light' ? '#E3F0F6' : theme.colors.softBlue];

  const content = (
    <View
      style={[
        {
          borderRadius: radius.lg,
          overflow: 'hidden',
          minHeight,
          backgroundColor: theme.colors.primaryLight,
        },
        style,
      ]}>
      <LinearGradient
        colors={gradient}
        start={{ x: 0, y: 0.5 }}
        end={{ x: 1, y: 0.5 }}
        style={{
          minHeight,
          paddingLeft: 18,
          paddingVertical: 18,
          flexDirection: 'row',
          alignItems: 'center',
        }}>
        <View style={{ flex: 1, gap: 6, paddingRight: 8, zIndex: 2 }}>{children}</View>
        {image ? (
          <OptimizedImage
            source={image}
            style={{
              position: 'absolute',
              right: 0,
              bottom: 0,
              width: imageWidth,
              height: minHeight,
              borderRadius: 0,
            }}
            contentFit="cover"
            contentPosition={{ top: 0, right: 0 }}
          />
        ) : null}
      </LinearGradient>
    </View>
  );

  if (!onPress) {
    return content;
  }

  return (
    <PressableScale onPress={onPress} scaleTo={motion.pressScale} contentStyle={{ borderRadius: radius.lg }}>
      {content}
    </PressableScale>
  );
}
