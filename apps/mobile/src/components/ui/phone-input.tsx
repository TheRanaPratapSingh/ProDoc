import { TextInput, View, type TextInputProps } from 'react-native';

import { FormFieldError } from '@/components/ui/form-field-error';
import { AppText } from '@/components/ui/text';
import { useActiveLanguage } from '@/i18n/copy';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { control, radius, spacing } from '@/theme/tokens';
import { nationalMobileDigits } from '@/utils/phone';

export function PhoneInput({
  label,
  error,
  required = false,
  value,
  onChangeText,
  ...props
}: TextInputProps & { label: string; error?: string; required?: boolean }) {
  const theme = useProdocTheme();
  const language = useActiveLanguage();

  return (
    <View style={{ gap: spacing.xs }}>
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
      <View
        style={{
          height: control.heightSm + 8,
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
            paddingHorizontal: spacing.md,
            alignSelf: 'stretch',
            justifyContent: 'center',
            backgroundColor: theme.colors.primaryLight,
            borderRightWidth: 1,
            borderRightColor: theme.colors.border,
          }}>
          <AppText variant="label" style={{ color: theme.colors.primary, fontSize: 13 }}>
            +91
          </AppText>
        </View>
        <TextInput
          returnKeyType="done"
          blurOnSubmit
          {...props}
          placeholderTextColor={theme.colors.textMuted}
          accessibilityLabel={label}
          keyboardType="phone-pad"
          autoComplete="tel"
          inputMode="tel"
          textContentType="telephoneNumber"
          maxLength={10}
          value={typeof value === 'string' ? nationalMobileDigits(value) : value}
          onChangeText={(next) => onChangeText?.(nationalMobileDigits(next))}
          style={[
            {
              flex: 1,
              paddingHorizontal: spacing.md,
              paddingVertical: 0,
              color: theme.colors.text,
              fontSize: 15,
              fontFamily: language === 'hi' ? 'NotoSansDevanagari_400Regular' : 'PlusJakartaSans_400Regular',
            },
            props.style,
          ]}
        />
      </View>
      <FormFieldError message={error} />
    </View>
  );
}
