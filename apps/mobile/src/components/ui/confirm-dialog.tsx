import { Modal, Pressable, View } from 'react-native';
import Animated, { FadeIn, FadeOut } from 'react-native-reanimated';

import { Button } from '@/components/ui/button';
import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';

export function ConfirmDialog({
  visible,
  title,
  body,
  confirmLabel,
  cancelLabel,
  danger = false,
  loading = false,
  onConfirm,
  onCancel,
}: {
  visible: boolean;
  title: string;
  body?: string;
  confirmLabel: string;
  cancelLabel: string;
  danger?: boolean;
  loading?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  const theme = useProdocTheme();

  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onCancel}>
      <Pressable
        onPress={onCancel}
        style={{
          flex: 1,
          backgroundColor: theme.colors.overlay,
          justifyContent: 'center',
          padding: spacing.xl,
        }}>
        <Animated.View
          entering={theme.reduceMotion ? undefined : FadeIn.duration(180)}
          exiting={theme.reduceMotion ? undefined : FadeOut.duration(120)}
          style={{
            backgroundColor: theme.colors.surface,
            borderRadius: radius.lg,
            padding: spacing.xl,
            gap: spacing.lg,
            borderWidth: 1,
            borderColor: theme.colors.border,
          }}>
          <Pressable onPress={(event) => event.stopPropagation()}>
            <View style={{ gap: spacing.lg }}>
              <AppText variant="headline">{title}</AppText>
              {body ? <AppText variant="muted">{body}</AppText> : null}
              <Button
                label={confirmLabel}
                variant={danger ? 'danger' : 'primary'}
                loading={loading}
                onPress={onConfirm}
              />
              <Button label={cancelLabel} variant="secondary" onPress={onCancel} />
            </View>
          </Pressable>
        </Animated.View>
      </Pressable>
    </Modal>
  );
}
