import { Pressable, View } from 'react-native';

import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';

export function Segmented<T extends string>({
  value,
  options,
  onChange,
}: {
  value: T;
  options: { value: T; label: string }[];
  onChange: (value: T) => void;
}) {
  const theme = useProdocTheme();

  return (
    <View
      style={{
        flexDirection: 'row',
        backgroundColor: theme.colors.primaryLight,
        borderRadius: radius.md,
        padding: 4,
        gap: 4,
      }}>
      {options.map((option) => {
        const selected = option.value === value;
        return (
          <Pressable
            key={option.value}
            accessibilityRole="button"
            accessibilityState={{ selected }}
            onPress={() => onChange(option.value)}
            style={{
              flex: 1,
              minHeight: 40,
              borderRadius: radius.sm,
              alignItems: 'center',
              justifyContent: 'center',
              paddingHorizontal: spacing.sm,
              backgroundColor: selected ? theme.colors.primary : 'transparent',
            }}>
            <AppText variant="label" style={{ color: selected ? theme.colors.onPrimary : theme.colors.text }}>
              {option.label}
            </AppText>
          </Pressable>
        );
      })}
    </View>
  );
}
