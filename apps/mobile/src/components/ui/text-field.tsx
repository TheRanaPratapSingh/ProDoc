import { TextInput, View, type TextInputProps } from 'react-native';

import { AppText } from '@/components/ui/text';
import { useActiveLanguage } from '@/i18n/copy';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { control, radius, spacing } from '@/theme/tokens';

export function TextField({
  label,
  error,
  ...props
}: TextInputProps & { label: string; error?: string }) {
  const theme = useProdocTheme();
  const language = useActiveLanguage();

  return (
    <View style={{ gap: spacing.sm }}>
      {label ? <AppText variant="label">{label}</AppText> : null}
      <TextInput
        placeholderTextColor={theme.colors.textMuted}
        accessibilityLabel={label}
        style={{
          minHeight: control.height,
          borderRadius: radius.md,
          borderWidth: 1.5,
          borderColor: error ? theme.colors.error : theme.colors.border,
          backgroundColor: theme.colors.surface,
          color: theme.colors.text,
          paddingHorizontal: spacing.lg,
          fontSize: 16,
          fontFamily: language === 'hi' ? 'NotoSansDevanagari_400Regular' : 'PlusJakartaSans_400Regular',
        }}
        {...props}
      />
      {error ? (
        <AppText variant="muted" accessibilityLiveRegion="polite" style={{ color: theme.colors.error }}>
          {error}
        </AppText>
      ) : null}
    </View>
  );
}
