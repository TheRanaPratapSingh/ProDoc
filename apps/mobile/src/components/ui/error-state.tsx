import { View } from 'react-native';

import { AppText } from '@/components/ui/text';
import { RetryButton } from '@/components/ui/retry-button';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';

export function ErrorState({
  title,
  body,
  actionLabel,
  onAction,
  loading = false,
}: {
  title: string;
  body?: string;
  actionLabel?: string;
  onAction?: () => void;
  loading?: boolean;
}) {
  const theme = useProdocTheme();

  return (
    <View style={{ gap: spacing.md, paddingVertical: spacing.xl }}>
      <AppText variant="headline" style={{ color: theme.colors.error }}>
        {title}
      </AppText>
      {body ? <AppText variant="muted">{body}</AppText> : null}
      {actionLabel && onAction ? (
        <RetryButton label={actionLabel} loading={loading} onPress={onAction} />
      ) : null}
    </View>
  );
}
