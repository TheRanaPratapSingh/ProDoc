import type { ImageSource } from 'expo-image';
import { type ReactNode } from 'react';
import { View } from 'react-native';

import { OptimizedImage } from '@/components/ui/optimized-image';
import { PressableScale } from '@/components/ui/pressable-scale';
import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { motion, radius, spacing } from '@/theme/tokens';

const CARD_WIDTH = 112;
const IMAGE_HEIGHT = 128;

export function SpecialtyCard({
  label,
  image,
  icon,
  recyclingKey,
  onPress,
  width = CARD_WIDTH,
}: {
  label: string;
  image?: ImageSource;
  icon?: ReactNode;
  recyclingKey?: string;
  onPress: () => void;
  width?: number;
}) {
  const theme = useProdocTheme();

  return (
    <PressableScale
      onPress={onPress}
      scaleTo={motion.pressScale}
      contentStyle={{
        width,
        borderRadius: radius.lg,
        overflow: 'hidden',
        backgroundColor: theme.colors.surface,
        borderWidth: 1,
        borderColor: theme.colors.border,
      }}>
      {image ? (
        <OptimizedImage
          source={image}
          recyclingKey={recyclingKey}
          style={{ width, height: IMAGE_HEIGHT, borderRadius: 0 }}
          contentFit="cover"
        />
      ) : (
        <View
          style={{
            width,
            height: IMAGE_HEIGHT,
            alignItems: 'center',
            justifyContent: 'center',
            backgroundColor: theme.colors.primaryLight,
          }}>
          {icon}
        </View>
      )}
      <View
        style={{
          minHeight: 44,
          paddingHorizontal: spacing.sm,
          paddingVertical: spacing.sm,
          alignItems: 'center',
          justifyContent: 'center',
        }}>
        <AppText
          variant="label"
          style={{ fontSize: 12, lineHeight: 15, textAlign: 'center' }}
          numberOfLines={2}>
          {label}
        </AppText>
      </View>
    </PressableScale>
  );
}

export const specialtyCardMetrics = {
  width: CARD_WIDTH,
  imageHeight: IMAGE_HEIGHT,
  gap: 12,
} as const;
