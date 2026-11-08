import { type ReactNode } from 'react';
import { View } from 'react-native';

import { Button } from '@/components/ui/button';
import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';

export function EmptyState({
  title,
  body,
  actionLabel,
  onAction,
  icon,
  compact = false,
}: {
  title: string;
  body?: string;
  actionLabel?: string;
  onAction?: () => void;
  icon?: ReactNode;
  compact?: boolean;
}) {
  const theme = useProdocTheme();

  return (
    <View
      style={{
        gap: spacing.md,
        paddingVertical: compact ? spacing.md : spacing.xl,
        alignItems: 'center',
        alignSelf: 'stretch',
      }}>
      {icon ? (
        <View
          style={{
            width: 64,
            height: 64,
            borderRadius: radius.xl,
            backgroundColor: theme.colors.primaryLight,
            alignItems: 'center',
            justifyContent: 'center',
            marginBottom: spacing.xs,
          }}>
          {icon}
        </View>
      ) : null}
      <AppText variant="headline" style={{ textAlign: 'center' }}>
        {title}
      </AppText>
      {body ? (
        <AppText variant="muted" style={{ textAlign: 'center' }}>
          {body}
        </AppText>
      ) : null}
      {actionLabel && onAction ? (
        <Button label={actionLabel} onPress={onAction} style={{ alignSelf: 'stretch', marginTop: spacing.sm }} />
      ) : null}
    </View>
  );
}
