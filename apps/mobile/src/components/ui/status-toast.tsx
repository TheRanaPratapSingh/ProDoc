import type { ReactNode } from 'react';
import { Pressable, View } from 'react-native';
import Animated, { FadeInDown, FadeOutUp } from 'react-native-reanimated';

import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';

export type ToastTone = 'success' | 'error' | 'warning' | 'info';

type ToastProps = {
  message?: string | null;
  tone?: ToastTone;
  onDismiss?: () => void;
};

const toneStyles = {
  success: { background: '#EAF3EF', border: '#2F855A55', color: '#2F855A' },
  error: { background: '#DC444418', border: '#DC444444', color: '#DC4444' },
  warning: { background: '#D99A3518', border: '#D99A3544', color: '#D99A35' },
  info: { background: '#EEF2F6', border: '#E2E8F0', color: '#14243D' },
} as const;

export function StatusToast({ message, tone = 'info', onDismiss }: ToastProps) {
  const theme = useProdocTheme();
  if (!message) {
    return null;
  }

  const colors = toneStyles[tone];

  return (
    <Animated.View
      entering={theme.reduceMotion ? undefined : FadeInDown.duration(180)}
      exiting={theme.reduceMotion ? undefined : FadeOutUp.duration(120)}>
      <Pressable
        onPress={onDismiss}
        accessibilityRole="alert"
        style={{
          backgroundColor: colors.background,
          borderRadius: radius.md,
          borderWidth: 1,
          borderColor: colors.border,
          paddingHorizontal: spacing.md,
          paddingVertical: spacing.sm,
        }}>
        <AppText variant="label" style={{ color: colors.color, fontSize: 13, lineHeight: 18 }}>
          {message}
        </AppText>
      </Pressable>
    </Animated.View>
  );
}

export function SuccessToast(props: Omit<ToastProps, 'tone'>) {
  return <StatusToast {...props} tone="success" />;
}

export function ErrorToast(props: Omit<ToastProps, 'tone'>) {
  return <StatusToast {...props} tone="error" />;
}

export function WarningToast(props: Omit<ToastProps, 'tone'>) {
  return <StatusToast {...props} tone="warning" />;
}

/** Stacking helper for screens that show transient feedback above content. */
export function ToastStack({ children }: { children: ReactNode }) {
  return <View style={{ gap: spacing.sm }}>{children}</View>;
}
