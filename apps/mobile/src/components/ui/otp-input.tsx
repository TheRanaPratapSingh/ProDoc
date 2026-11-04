import { useRef, type ElementRef } from 'react';
import { Pressable, TextInput, View } from 'react-native';
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
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  error?: string;
  length?: number;
}) {
  const theme = useProdocTheme();
  const inputRef = useRef<TextInputRef>(null);
  const digits = value.padEnd(length, ' ').slice(0, length).split('');

  return (
    <View style={{ gap: spacing.sm }}>
      <AppText variant="label">{label}</AppText>
      <Pressable onPress={() => inputRef.current?.focus()} accessibilityLabel={label}>
        <View style={{ flexDirection: 'row', gap: spacing.sm, justifyContent: 'space-between' }}>
          {digits.map((digit, index) => {
            const filled = digit.trim().length > 0;
            const active = value.length === index;
            return (
              <Animated.View
                key={index}
                entering={theme.reduceMotion ? undefined : FadeIn.duration(120)}
                style={{
                  flex: 1,
                  minHeight: control.height,
                  borderRadius: radius.md,
                  borderWidth: 1.5,
                  borderColor: error ? theme.colors.error : active || filled ? theme.colors.primary : theme.colors.border,
                  backgroundColor: filled ? theme.colors.primaryLight : theme.colors.surface,
                  alignItems: 'center',
                  justifyContent: 'center',
                }}>
                <AppText variant="headline" style={{ color: theme.colors.text }}>
                  {filled ? digit : ''}
                </AppText>
              </Animated.View>
            );
          })}
        </View>
      </Pressable>
      <TextInput
        ref={inputRef}
        value={value}
        onChangeText={(next) => onChange(next.replace(/\D/g, '').slice(0, length))}
        keyboardType="number-pad"
        textContentType="oneTimeCode"
        autoComplete="sms-otp"
        maxLength={length}
        style={{ position: 'absolute', opacity: 0, height: 1, width: 1 }}
        accessibilityLabel={label}
      />
      {error ? (
        <AppText variant="muted" accessibilityLiveRegion="polite" style={{ color: theme.colors.error }}>
          {error}
        </AppText>
      ) : null}
    </View>
  );
}
