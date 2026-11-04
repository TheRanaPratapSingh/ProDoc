import { useEffect } from 'react';
import { View, type ViewStyle } from 'react-native';
import Animated, {
  useAnimatedStyle,
  useSharedValue,
  withRepeat,
  withTiming,
} from 'react-native-reanimated';

import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius } from '@/theme/tokens';

export function LoadingSkeleton({
  height = 16,
  width = '100%',
  style,
}: {
  height?: number;
  width?: number | `${number}%`;
  style?: ViewStyle;
}) {
  const theme = useProdocTheme();
  const opacity = useSharedValue(0.45);

  useEffect(() => {
    if (theme.reduceMotion) {
      return;
    }
    opacity.value = withRepeat(withTiming(1, { duration: 700 }), -1, true);
  }, [opacity, theme.reduceMotion]);

  const animated = useAnimatedStyle(() => ({ opacity: theme.reduceMotion ? 0.55 : opacity.value }));

  return (
    <Animated.View
      style={[
        {
          height,
          width,
          borderRadius: radius.sm,
          backgroundColor: theme.colors.border,
        },
        animated,
        style,
      ]}
    />
  );
}

export function CardSkeleton() {
  return (
    <View style={{ gap: 12 }}>
      <LoadingSkeleton height={56} />
      <LoadingSkeleton height={14} width="70%" />
      <LoadingSkeleton height={14} width="45%" />
      <LoadingSkeleton height={48} />
    </View>
  );
}
