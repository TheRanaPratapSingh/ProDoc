import { TextInput, View, type TextInputProps } from 'react-native';

import { AppText } from '@/components/ui/text';
import { useActiveLanguage } from '@/i18n/copy';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { control, radius, spacing } from '@/theme/tokens';

export function TextField({
  label,
  error,
  required = false,
  style,
  multiline,
  returnKeyType,
  blurOnSubmit,
  ...props
}: TextInputProps & { label: string; error?: string; required?: boolean }) {
  const theme = useProdocTheme();
  const language = useActiveLanguage();

  return (
    <View style={{ gap: spacing.xs }}>
      {label ? (
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 2 }}>
          <AppText variant="label" style={{ fontSize: 13, lineHeight: 18 }}>
            {label}
          </AppText>
          {required ? (
            <AppText variant="label" style={{ fontSize: 13, lineHeight: 18, color: theme.colors.error }}>
              *
            </AppText>
          ) : null}
        </View>
      ) : null}
      <TextInput
        placeholderTextColor={theme.colors.textMuted}
        accessibilityLabel={label}
        multiline={multiline}
        textAlignVertical={multiline ? 'top' : 'center'}
        // Next keeps focus moving via KeyboardToolbar; Done dismisses on single-line submit.
        returnKeyType={returnKeyType ?? (multiline ? 'default' : 'next')}
        blurOnSubmit={blurOnSubmit ?? !multiline}
        style={[
          {
            minHeight: multiline ? 88 : control.heightSm + 8,
            maxHeight: multiline ? 120 : undefined,
            borderRadius: radius.md,
            borderWidth: 1.5,
            borderColor: error ? theme.colors.error : theme.colors.border,
            backgroundColor: theme.colors.surface,
            color: theme.colors.text,
            paddingHorizontal: spacing.md,
            paddingVertical: multiline ? spacing.sm : 0,
            fontSize: 15,
            lineHeight: 20,
            fontFamily: language === 'hi' ? 'NotoSansDevanagari_400Regular' : 'PlusJakartaSans_400Regular',
          },
          style,
        ]}
        {...props}
      />
      {error ? (
        <AppText
          variant="muted"
          accessibilityLiveRegion="polite"
          style={{ color: theme.colors.error, fontSize: 12, lineHeight: 16 }}>
          {error}
        </AppText>
      ) : null}
    </View>
  );
}
