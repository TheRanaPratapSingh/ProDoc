import { View } from 'react-native';

import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';

export function DashboardStatCard({
  label,
  value,
  hint,
}: {
  label: string;
  value: string | number;
  hint?: string;
}) {
  const theme = useProdocTheme();

  return (
    <Card
      style={{
        flexGrow: 1,
        flexBasis: 140,
        minWidth: 140,
        gap: spacing.sm,
        borderLeftWidth: 3,
        borderLeftColor: theme.colors.primary,
      }}>
      <AppText variant="caption">{label}</AppText>
      <AppText variant="title" style={{ color: theme.colors.primary }}>
        {value}
      </AppText>
      {hint ? <AppText variant="muted">{hint}</AppText> : null}
    </Card>
  );
}
