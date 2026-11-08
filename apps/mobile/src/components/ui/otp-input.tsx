import { useEffect, useRef, type ElementRef } from 'react';
import { Platform, TextInput, View } from 'react-native';
import Animated, { FadeIn } from 'react-native-reanimated';

import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { control, radius, spacing } from '@/theme/tokens';

type TextInputRef = ElementRef<typeof TextInput>;

export function OtpInput({
  label,
  value,
  onChange,
  error,
  length = 6,
  autoFocus = true,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  error?: string;
  length?: number;
  autoFocus?: boolean;
}) {
  const theme = useProdocTheme();
  const refs = useRef<Array<TextInputRef | null>>([]);
  const digits = value.padEnd(length, ' ').slice(0, length).split('');

  useEffect(() => {
    if (autoFocus) {
      refs.current[0]?.focus();
    }
  }, [autoFocus]);

  const setDigit = (index: number, raw: string) => {
    const cleaned = raw.replace(/\D/g, '');
    if (cleaned.length > 1) {
      // Paste of full or partial OTP into any box.
      const next = `${value.slice(0, index)}${cleaned}`.replace(/\D/g, '').slice(0, length);
      onChange(next);
      const focusAt = Math.min(next.length, length - 1);
      refs.current[focusAt]?.focus();
      return;
    }

    const chars = value.padEnd(length, ' ').slice(0, length).split('');
    chars[index] = cleaned.slice(-1) || ' ';
    const next = chars.join('').replace(/\s/g, '').slice(0, length);
    onChange(next);
    if (cleaned && index < length - 1) {
      refs.current[index + 1]?.focus();
    }
  };

  const onKeyPress = (index: number, key: string) => {
    if (key !== 'Backspace') {
      return;
    }
    if (value[index]) {
      const chars = value.split('');
      chars[index] = '';
      onChange(chars.join('').replace(/\s/g, ''));
      return;
    }
    if (index > 0) {
      const chars = value.split('');
      chars[index - 1] = '';
      onChange(chars.join('').replace(/\s/g, ''));
      refs.current[index - 1]?.focus();
    }
  };

  return (
    <View style={{ gap: spacing.sm }}>
      <AppText variant="label">{label}</AppText>
      <View style={{ flexDirection: 'row', gap: spacing.sm, justifyContent: 'space-between' }}>
        {digits.map((digit, index) => {
          const filled = digit.trim().length > 0;
          const active = value.length === index || (value.length === length && index === length - 1);
          return (
            <Animated.View
              key={index}
              entering={theme.reduceMotion ? undefined : FadeIn.duration(120)}
              style={{ flex: 1 }}>
              <TextInput
                ref={(node) => {
                  refs.current[index] = node;
                }}
                value={filled ? digit : ''}
                onChangeText={(next) => setDigit(index, next)}
                onKeyPress={({ nativeEvent }) => onKeyPress(index, nativeEvent.key)}
                keyboardType="number-pad"
                textContentType={index === 0 ? 'oneTimeCode' : 'none'}
                autoComplete={index === 0 ? 'sms-otp' : 'off'}
                maxLength={length}
                selectTextOnFocus
                caretHidden={Platform.OS !== 'web'}
                accessibilityLabel={`${label} digit ${index + 1}`}
                style={{
                  minHeight: control.height,
                  borderRadius: radius.md,
                  borderWidth: 1.5,
                  borderColor: error
                    ? theme.colors.error
                    : active || filled
                      ? theme.colors.primary
                      : theme.colors.border,
                  backgroundColor: filled ? theme.colors.primaryLight : theme.colors.surface,
                  textAlign: 'center',
                  fontSize: 22,
                  fontWeight: '700',
                  color: theme.colors.text,
                }}
              />
            </Animated.View>
          );
        })}
      </View>
      {error ? (
        <AppText variant="muted" accessibilityLiveRegion="polite" style={{ color: theme.colors.error }}>
          {error}
        </AppText>
      ) : null}
    </View>
  );
}
