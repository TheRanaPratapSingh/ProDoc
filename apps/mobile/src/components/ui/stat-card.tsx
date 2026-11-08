import { View } from 'react-native';

import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';

export function DashboardStatCard({
  label,
  value,
  hint,
  variant = 'card',
}: {
  label: string;
  value: string | number;
  hint?: string;
  /** `ring` matches reference circular KPI tiles on doctor dashboards. */
  variant?: 'card' | 'ring';
}) {
  const theme = useProdocTheme();

  if (variant === 'ring') {
    return (
      <View style={{ alignItems: 'center', gap: spacing.sm, minWidth: 88, flexGrow: 1, flexBasis: 88 }}>
        <View
          style={{
            width: 72,
            height: 72,
            borderRadius: 36,
            borderWidth: 3,
            borderColor: theme.colors.primary,
            backgroundColor: theme.colors.primaryLight,
            alignItems: 'center',
            justifyContent: 'center',
          }}>
          <AppText variant="headline" style={{ color: theme.colors.primary }}>
            {value}
          </AppText>
        </View>
        <AppText variant="caption" style={{ textAlign: 'center' }}>
          {label}
        </AppText>
        {hint ? (
          <AppText variant="muted" style={{ textAlign: 'center' }}>
            {hint}
          </AppText>
        ) : null}
      </View>
    );
  }

  return (
    <Card
      elevated
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
