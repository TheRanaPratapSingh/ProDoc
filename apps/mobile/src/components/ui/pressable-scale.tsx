import { type ReactNode } from 'react';
import { Pressable, type PressableProps, type StyleProp, type ViewStyle } from 'react-native';
import Animated, { useAnimatedStyle, useSharedValue, withTiming } from 'react-native-reanimated';

import { useProdocTheme } from '@/theme/prodoc-theme';
import { duration, motion } from '@/theme/tokens';

/** Shared press feedback — respects reduceMotion. */
export function PressableScale({
  children,
  style,
  contentStyle,
  scaleTo = motion.cardPressScale,
  onPress,
  disabled,
  ...props
}: PressableProps & {
  children: ReactNode;
  style?: StyleProp<ViewStyle>;
  contentStyle?: StyleProp<ViewStyle>;
  scaleTo?: number;
}) {
  const theme = useProdocTheme();
  const scale = useSharedValue(1);
  const animated = useAnimatedStyle(() => ({ transform: [{ scale: scale.value }] }));

  return (
    <Pressable
      disabled={disabled}
      onPress={onPress}
      onPressIn={() => {
        if (!theme.reduceMotion && !disabled) {
          scale.value = withTiming(scaleTo, { duration: duration.press });
        }
      }}
      onPressOut={() => {
        scale.value = withTiming(1, { duration: duration.press });
      }}
      style={style}
      {...props}>
      <Animated.View style={[contentStyle, animated]}>{children}</Animated.View>
    </Pressable>
  );
}
