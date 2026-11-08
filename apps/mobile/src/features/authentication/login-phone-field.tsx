import { TextInput, View, type TextInputProps } from 'react-native';

import { FormFieldError } from '@/components/ui/form-field-error';
import { AppText } from '@/components/ui/text';
import { useActiveLanguage } from '@/i18n/copy';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';
import { nationalMobileDigits } from '@/utils/phone';

const FIELD_HEIGHT = 52;

/** Compact +91 phone field sized for auth login cards (52px). */
export function LoginPhoneField({
  label,
  error,
  editable = true,
  style,
  value,
  onChangeText,
  ...props
}: TextInputProps & { label: string; error?: string }) {
  const theme = useProdocTheme();
  const language = useActiveLanguage();
  const readOnly = editable === false;

  return (
    <View style={{ gap: spacing.xs + 2 }}>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 2 }}>
        <AppText variant="label" style={{ color: '#14243D', fontSize: 13, lineHeight: 18 }}>
          {label}
        </AppText>
        <AppText variant="label" style={{ color: theme.colors.error, fontSize: 13, lineHeight: 18 }}>
          *
        </AppText>
      </View>
      <View
        style={{
          height: FIELD_HEIGHT,
          borderRadius: radius.md,
          borderWidth: 1,
          borderColor: error ? theme.colors.error : '#E2E8F0',
          backgroundColor: readOnly ? '#F7FAFC' : '#FFFFFF',
          flexDirection: 'row',
          alignItems: 'center',
          overflow: 'hidden',
        }}>
        <View
          style={{
            height: '100%',
            paddingHorizontal: 12,
            justifyContent: 'center',
            alignItems: 'center',
            backgroundColor: '#EAF3EF',
            borderRightWidth: 1,
            borderRightColor: '#E2E8F0',
            minWidth: 54,
          }}>
          <AppText variant="label" style={{ color: '#356F5E', fontSize: 14, lineHeight: 18 }}>
            +91
          </AppText>
        </View>
        <TextInput
          returnKeyType="done"
          blurOnSubmit
          {...props}
          placeholderTextColor="#64748B"
          accessibilityLabel={label}
          keyboardType="phone-pad"
          autoComplete="tel"
          inputMode="tel"
          textContentType="telephoneNumber"
          editable={editable}
          maxLength={10}
          value={typeof value === 'string' ? (readOnly ? value : nationalMobileDigits(value)) : value}
          onChangeText={(next) => onChangeText?.(nationalMobileDigits(next))}
          style={[
            {
              flex: 1,
              height: FIELD_HEIGHT,
              paddingHorizontal: 12,
              color: '#14243D',
              fontSize: 16,
              lineHeight: 22,
              fontFamily: language === 'hi' ? 'NotoSansDevanagari_400Regular' : 'PlusJakartaSans_400Regular',
            },
            style,
          ]}
        />
      </View>
      <FormFieldError message={error} />
    </View>
  );
}
