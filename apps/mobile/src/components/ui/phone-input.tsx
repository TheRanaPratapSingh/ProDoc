import { TextInput, View, type TextInputProps } from 'react-native';

import { AppText } from '@/components/ui/text';
import { useActiveLanguage } from '@/i18n/copy';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { control, radius, spacing } from '@/theme/tokens';

export function PhoneInput({
  label,
  error,
  ...props
}: TextInputProps & { label: string; error?: string }) {
  const theme = useProdocTheme();
  const language = useActiveLanguage();

  return (
    <View style={{ gap: spacing.sm }}>
      <AppText variant="label">{label}</AppText>
      <View
        style={{
          minHeight: control.height,
          borderRadius: radius.md,
          borderWidth: 1.5,
          borderColor: error ? theme.colors.error : theme.colors.border,
          backgroundColor: theme.colors.surface,
          flexDirection: 'row',
          alignItems: 'center',
          overflow: 'hidden',
        }}>
        <View
          style={{
            paddingHorizontal: spacing.lg,
            height: '100%',
            justifyContent: 'center',
            backgroundColor: theme.colors.primaryLight,
            borderRightWidth: 1,
            borderRightColor: theme.colors.border,
          }}>
          <AppText variant="label" style={{ color: theme.colors.primary }}>
            +91
          </AppText>
        </View>
        <TextInput
          placeholderTextColor={theme.colors.textMuted}
          accessibilityLabel={label}
          keyboardType="phone-pad"
          autoComplete="tel"
          inputMode="tel"
          textContentType="telephoneNumber"
          style={{
            flex: 1,
            minHeight: control.height,
            paddingHorizontal: spacing.lg,
            color: theme.colors.text,
            fontSize: 16,
            fontFamily: language === 'hi' ? 'NotoSansDevanagari_400Regular' : 'PlusJakartaSans_400Regular',
          }}
          {...props}
        />
      </View>
      {error ? (
        <AppText variant="muted" accessibilityLiveRegion="polite" style={{ color: theme.colors.error }}>
          {error}
        </AppText>
      ) : null}
    </View>
  );
}
