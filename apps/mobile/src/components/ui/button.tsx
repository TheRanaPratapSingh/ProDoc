import { useState } from 'react';
import { Pressable, ActivityIndicator, type PressableProps, type StyleProp, type ViewStyle } from 'react-native';
import Animated, { useAnimatedStyle, useSharedValue, withTiming } from 'react-native-reanimated';

import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { control, duration, radius } from '@/theme/tokens';

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger';

export function Button({
  label,
  loading = false,
  disabled = false,
  variant = 'primary',
  style,
  onPress,
  ...props
}: PressableProps & {
  label: string;
  loading?: boolean;
  variant?: Variant;
  style?: StyleProp<ViewStyle>;
}) {
  const theme = useProdocTheme();
  const scale = useSharedValue(1);
  const [pressed, setPressed] = useState(false);
  const animated = useAnimatedStyle(() => ({ transform: [{ scale: scale.value }] }));
  const inactive = disabled || loading;

  const background = {
    primary: pressed ? theme.colors.primaryPressed : theme.colors.primary,
    secondary: theme.colors.surface,
    ghost: 'transparent',
    danger: theme.colors.error,
  }[variant];

  const foreground = {
    primary: theme.colors.onPrimary,
    secondary: theme.colors.text,
    ghost: theme.colors.primary,
    danger: theme.colors.onPrimary,
  }[variant];

  return (
    <Animated.View style={animated}>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={label}
        accessibilityState={{ disabled: inactive, busy: loading }}
        hitSlop={8}
        disabled={inactive}
        onPress={onPress}
        onPressIn={() => {
          setPressed(true);
          if (!theme.reduceMotion) {
            scale.value = withTiming(0.98, { duration: duration.press });
          }
        }}
        onPressOut={() => {
          setPressed(false);
          scale.value = withTiming(1, { duration: duration.press });
        }}
        style={[
          {
            minHeight: control.height,
            borderRadius: radius.md,
            alignItems: 'center',
            justifyContent: 'center',
            paddingHorizontal: 18,
            backgroundColor: background,
            borderWidth: variant === 'secondary' || variant === 'ghost' ? 1 : 0,
            borderColor: variant === 'ghost' ? 'transparent' : theme.colors.border,
            opacity: inactive ? 0.55 : 1,
          },
          style,
        ]}
        {...props}>
        {loading ? (
          <ActivityIndicator color={foreground} />
        ) : (
          <AppText variant="label" style={{ color: foreground }}>
            {label}
          </AppText>
        )}
      </Pressable>
    </Animated.View>
  );
}
