import { Pressable, View } from 'react-native';

import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';

export type AdminFeedbackTone = 'success' | 'error' | 'info';

export function AdminFeedbackBanner({
  message,
  tone = 'info',
  onDismiss,
}: {
  message: string | null | undefined;
  tone?: AdminFeedbackTone;
  onDismiss?: () => void;
}) {
  const theme = useProdocTheme();
  if (!message) {
    return null;
  }

  const background =
    tone === 'success' ? theme.colors.primaryLight : tone === 'error' ? `${theme.colors.error}18` : '#EEF2F6';
  const color = tone === 'success' ? theme.colors.primary : tone === 'error' ? theme.colors.error : theme.colors.text;

  return (
    <Pressable
      onPress={onDismiss}
      style={{
        backgroundColor: background,
        borderRadius: 14,
        borderWidth: 1,
        borderColor: tone === 'error' ? `${theme.colors.error}44` : theme.colors.border,
        paddingHorizontal: spacing.md,
        paddingVertical: spacing.sm,
      }}>
      <AppText variant="label" style={{ color, fontSize: 13, lineHeight: 18 }}>
        {message}
      </AppText>
    </Pressable>
  );
}
