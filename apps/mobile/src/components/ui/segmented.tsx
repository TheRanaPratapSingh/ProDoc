import { Pressable, ScrollView, View } from 'react-native';

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
  const scrollable = options.length > 3;

  const chips = options.map((option) => {
    const selected = option.value === value;
    return (
      <Pressable
        key={option.value}
        accessibilityRole="button"
        accessibilityState={{ selected }}
        onPress={() => onChange(option.value)}
        style={{
          flex: scrollable ? undefined : 1,
          flexShrink: scrollable ? 0 : 1,
          minWidth: scrollable ? 88 : 0,
          minHeight: 40,
          borderRadius: radius.sm,
          alignItems: 'center',
          justifyContent: 'center',
          paddingHorizontal: scrollable ? spacing.md : spacing.sm,
          backgroundColor: selected ? theme.colors.primary : 'transparent',
        }}>
        <AppText
          variant="label"
          numberOfLines={1}
          adjustsFontSizeToFit
          minimumFontScale={0.85}
          style={{
            color: selected ? theme.colors.onPrimary : theme.colors.text,
            textAlign: 'center',
            width: '100%',
          }}>
          {option.label}
        </AppText>
      </Pressable>
    );
  });

  if (scrollable) {
    return (
      <View
        style={{
          backgroundColor: theme.colors.primaryLight,
          borderRadius: radius.md,
          padding: 4,
          width: '100%',
          maxWidth: '100%',
        }}>
        <ScrollView
          horizontal
          showsHorizontalScrollIndicator={false}
          nestedScrollEnabled
          contentContainerStyle={{ gap: 4, flexGrow: 1 }}>
          {chips}
        </ScrollView>
      </View>
    );
  }

  return (
    <View
      style={{
        flexDirection: 'row',
        backgroundColor: theme.colors.primaryLight,
        borderRadius: radius.md,
        padding: 4,
        gap: 4,
        width: '100%',
        maxWidth: '100%',
      }}>
      {chips}
    </View>
  );
}
