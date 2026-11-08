import { View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { StatusToast, ToastStack } from '@/components/ui/status-toast';
import { useFeedbackStore } from '@/stores/feedback-store';
import { spacing } from '@/theme/tokens';

/** Global overlay for transient success/error/warning/info feedback. */
export function ToastHost() {
  const insets = useSafeAreaInsets();
  const toasts = useFeedbackStore((state) => state.toasts);
  const dismiss = useFeedbackStore((state) => state.dismiss);

  if (toasts.length === 0) {
    return null;
  }

  return (
    <View
      pointerEvents="box-none"
      style={{
        position: 'absolute',
        top: Math.max(insets.top, spacing.md) + spacing.xs,
        left: spacing.lg,
        right: spacing.lg,
        zIndex: 1000,
      }}>
      <ToastStack>
        {toasts.map((toast) => (
          <StatusToast
            key={toast.id}
            message={toast.message}
            tone={toast.tone}
            onDismiss={() => dismiss(toast.id)}
          />
        ))}
      </ToastStack>
    </View>
  );
}
