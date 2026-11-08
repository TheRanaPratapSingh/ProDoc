import { type ReactNode } from 'react';
import { Modal, Pressable, View, useWindowDimensions } from 'react-native';
import Animated, { FadeIn, FadeOut, SlideInDown, SlideOutDown } from 'react-native-reanimated';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { duration, radius, spacing } from '@/theme/tokens';

/** Lightweight confirmation / action sheet — respects reduceMotion. */
export function BottomSheet({
  visible,
  title,
  onClose,
  children,
  maxHeightRatio = 0.72,
}: {
  visible: boolean;
  title?: string;
  onClose: () => void;
  children: ReactNode;
  /** Fraction of window height for the sheet (default 0.72). */
  maxHeightRatio?: number;
}) {
  const theme = useProdocTheme();
  const insets = useSafeAreaInsets();
  const { height } = useWindowDimensions();
  const reduce = theme.reduceMotion;
  const sheetMax = height * Math.min(0.92, Math.max(0.4, maxHeightRatio));

  return (
    <Modal visible={visible} transparent animationType="none" onRequestClose={onClose}>
      <View style={{ flex: 1, justifyContent: 'flex-end' }}>
        <Animated.View
          entering={reduce ? undefined : FadeIn.duration(duration.modal)}
          exiting={reduce ? undefined : FadeOut.duration(120)}
          style={{ position: 'absolute', top: 0, right: 0, bottom: 0, left: 0, backgroundColor: theme.colors.overlay }}>
          <Pressable style={{ flex: 1 }} onPress={onClose} accessibilityLabel="Close" />
        </Animated.View>
        <Animated.View
          entering={reduce ? undefined : SlideInDown.duration(duration.modal)}
          exiting={reduce ? undefined : SlideOutDown.duration(160)}
          style={{
            maxHeight: sheetMax,
            backgroundColor: theme.colors.surface,
            borderTopLeftRadius: radius.xl,
            borderTopRightRadius: radius.xl,
            paddingHorizontal: spacing.xl,
            paddingTop: spacing.lg,
            paddingBottom: Math.max(insets.bottom, spacing.lg),
            gap: spacing.lg,
            borderWidth: 1,
            borderColor: theme.colors.border,
          }}>
          <View
            style={{
              alignSelf: 'center',
              width: 40,
              height: 4,
              borderRadius: radius.pill,
              backgroundColor: theme.colors.border,
            }}
          />
          {title ? <AppText variant="headline">{title}</AppText> : null}
          {children}
        </Animated.View>
      </View>
    </Modal>
  );
}
