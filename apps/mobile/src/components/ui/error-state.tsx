import { View } from 'react-native';

import { Button } from '@/components/ui/button';
import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';

export function ErrorState({
  title,
  body,
  actionLabel,
  onAction,
}: {
  title: string;
  body?: string;
  actionLabel?: string;
  onAction?: () => void;
}) {
  const theme = useProdocTheme();

  return (
    <View style={{ gap: spacing.md, paddingVertical: spacing.xl }}>
      <AppText variant="headline" style={{ color: theme.colors.error }}>
        {title}
      </AppText>
      {body ? <AppText variant="muted">{body}</AppText> : null}
      {actionLabel && onAction ? <Button label={actionLabel} onPress={onAction} /> : null}
    </View>
  );
}
