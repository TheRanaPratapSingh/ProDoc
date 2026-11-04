import { Pressable } from 'react-native';
import Animated, { useAnimatedStyle, useSharedValue, withTiming } from 'react-native-reanimated';

import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { control, duration, radius, spacing } from '@/theme/tokens';

export function TimeSlot({
  label,
  selected = false,
  disabled = false,
  onPress,
}: {
  label: string;
  selected?: boolean;
  disabled?: boolean;
  onPress: () => void;
}) {
  const theme = useProdocTheme();
  const scale = useSharedValue(1);
  const animated = useAnimatedStyle(() => ({ transform: [{ scale: scale.value }] }));

  return (
    <Animated.View style={[{ flexGrow: 1, flexBasis: '30%', minWidth: 96 }, animated]}>
      <Pressable
        accessibilityRole="button"
        accessibilityState={{ selected, disabled }}
        disabled={disabled}
        onPress={onPress}
        onPressIn={() => {
          if (!theme.reduceMotion) {
            scale.value = withTiming(0.97, { duration: duration.press });
          }
        }}
        onPressOut={() => {
          scale.value = withTiming(1, { duration: duration.press });
        }}
        style={{
          minHeight: control.heightSm + 8,
          borderRadius: radius.md,
          borderWidth: 1.5,
          borderColor: selected ? theme.colors.primary : theme.colors.border,
          backgroundColor: selected ? theme.colors.primary : theme.colors.surface,
          alignItems: 'center',
          justifyContent: 'center',
          paddingHorizontal: spacing.md,
          opacity: disabled ? 0.4 : 1,
        }}>
        <AppText variant="label" style={{ color: selected ? theme.colors.onPrimary : theme.colors.text }}>
          {label}
        </AppText>
      </Pressable>
    </Animated.View>
  );
}
