import { View } from 'react-native';

import { Button } from '@/components/ui/button';
import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';

export function ResponsiveHeader({
  title,
  subtitle,
  onBack,
  backLabel,
  actionLabel,
  onAction,
}: {
  title: string;
  subtitle?: string;
  onBack?: () => void;
  backLabel?: string;
  actionLabel?: string;
  onAction?: () => void;
}) {
  const theme = useProdocTheme();

  return (
    <View style={{ gap: spacing.md }}>
      {onBack && backLabel ? <Button label={backLabel} variant="ghost" onPress={onBack} /> : null}
      <View style={{ gap: spacing.xs }}>
        <AppText variant="caption" style={{ color: theme.colors.primary }}>
          ProDoc
        </AppText>
        <AppText variant="title">{title}</AppText>
        {subtitle ? <AppText variant="muted">{subtitle}</AppText> : null}
      </View>
      {actionLabel && onAction ? <Button label={actionLabel} onPress={onAction} /> : null}
    </View>
  );
}
