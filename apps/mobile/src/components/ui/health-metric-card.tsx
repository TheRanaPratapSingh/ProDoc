import { type ReactNode } from 'react';
import { Pressable, View } from 'react-native';

import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';

/**
 * Visual summary tile for real clinical counts (allergies, Rx, visits).
 * Never invents numeric vitals — pass only API-backed values.
 */
export function HealthMetricCard({
  label,
  value,
  hint,
  icon,
  onPress,
}: {
  label: string;
  value: string | number;
  hint?: string;
  icon?: ReactNode;
  onPress?: () => void;
}) {
  const theme = useProdocTheme();
  const body = (
    <Card
      elevated
      style={{
        flexGrow: 1,
        flexBasis: 140,
        minWidth: 140,
        gap: spacing.sm,
        padding: spacing.lg,
      }}>
      <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
        <AppText variant="caption" style={{ color: theme.colors.textMuted, flex: 1 }} numberOfLines={2}>
          {label}
        </AppText>
        {icon ? (
          <View
            style={{
              width: 36,
              height: 36,
              borderRadius: radius.md,
              backgroundColor: theme.colors.primaryLight,
              alignItems: 'center',
              justifyContent: 'center',
            }}>
            {icon}
          </View>
        ) : null}
      </View>
      <AppText variant="title" style={{ color: theme.colors.primary, fontSize: 28, lineHeight: 34 }}>
        {value}
      </AppText>
      {hint ? <AppText variant="muted">{hint}</AppText> : null}
    </Card>
  );

  if (!onPress) {
    return body;
  }

  return (
    <Pressable onPress={onPress} style={{ flexGrow: 1, flexBasis: 140, minWidth: 140 }}>
      {body}
    </Pressable>
  );
}

/** Simple horizontal bar chart for named counts (admin / health summaries). */
export function ChartCard({
  title,
  rows,
  formatValue,
}: {
  title: string;
  rows: { label: string; value: number }[];
  formatValue?: (value: number) => string;
}) {
  const theme = useProdocTheme();
  const max = Math.max(1, ...rows.map((row) => row.value));

  return (
    <Card elevated style={{ gap: spacing.md }}>
      <AppText variant="label">{title}</AppText>
      {rows.length === 0 ? (
        <AppText variant="muted">—</AppText>
      ) : (
        rows.map((row) => (
          <View key={row.label} style={{ gap: spacing.xs }}>
            <View style={{ flexDirection: 'row', justifyContent: 'space-between', gap: spacing.sm }}>
              <AppText variant="muted" style={{ flex: 1 }} numberOfLines={1}>
                {row.label}
              </AppText>
              <AppText variant="label">{formatValue ? formatValue(row.value) : row.value}</AppText>
            </View>
            <View
              style={{
                height: 8,
                borderRadius: radius.pill,
                backgroundColor: theme.colors.border,
                overflow: 'hidden',
              }}>
              <View
                style={{
                  width: `${Math.round((row.value / max) * 100)}%`,
                  height: '100%',
                  backgroundColor: theme.colors.primary,
                  borderRadius: radius.pill,
                }}
              />
            </View>
          </View>
        ))
      )}
    </Card>
  );
}
