import { View } from 'react-native';

import { Button } from '@/components/ui/button';
import { AppText } from '@/components/ui/text';
import { spacing } from '@/theme/tokens';

export function EmptyState({
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
  return (
    <View style={{ gap: spacing.md, paddingVertical: spacing.xl, alignItems: 'flex-start' }}>
      <AppText variant="headline">{title}</AppText>
      {body ? <AppText variant="muted">{body}</AppText> : null}
      {actionLabel && onAction ? <Button label={actionLabel} onPress={onAction} style={{ alignSelf: 'stretch' }} /> : null}
    </View>
  );
}
