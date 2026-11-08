import { useState } from 'react';
import { ActivityIndicator, Pressable, type PressableProps } from 'react-native';
import Animated, { useAnimatedStyle, useSharedValue, withTiming } from 'react-native-reanimated';

import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { duration, radius } from '@/theme/tokens';

const BUTTON_HEIGHT = 48;

/** Full-width Earth Green CTA for patient login. */
export function LoginPrimaryButton({
  label,
  loading = false,
  disabled = false,
  onPress,
  ...props
}: PressableProps & { label: string; loading?: boolean }) {
  const theme = useProdocTheme();
  const scale = useSharedValue(1);
  const [pressed, setPressed] = useState(false);
  const animated = useAnimatedStyle(() => ({ transform: [{ scale: scale.value }] }));
  const inactive = disabled || loading;

  return (
    <Animated.View style={animated}>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={label}
        accessibilityState={{ disabled: inactive, busy: loading }}
        disabled={inactive}
        onPress={onPress}
        onPressIn={() => {
          setPressed(true);
          if (!theme.reduceMotion) {
            scale.value = withTiming(0.985, { duration: duration.press });
          }
        }}
        onPressOut={() => {
          setPressed(false);
          scale.value = withTiming(1, { duration: duration.press });
        }}
        style={{
          height: BUTTON_HEIGHT,
          borderRadius: radius.md,
          alignItems: 'center',
          justifyContent: 'center',
          backgroundColor: pressed ? '#285849' : '#356F5E',
          opacity: inactive ? 0.55 : 1,
        }}
        {...props}>
        {loading ? (
          <ActivityIndicator color="#FFFFFF" />
        ) : (
          <AppText variant="label" style={{ color: '#FFFFFF', fontSize: 15, lineHeight: 20 }}>
            {label}
          </AppText>
        )}
      </Pressable>
    </Animated.View>
  );
}
