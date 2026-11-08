import { View } from 'react-native';

import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';

export type AdminChartRow = { label: string; value: number };

export function AdminChartCard({
  title,
  rows,
  emptyLabel = '—',
  formatValue,
}: {
  title: string;
  rows: AdminChartRow[];
  emptyLabel?: string;
  formatValue?: (value: number) => string;
}) {
  const theme = useProdocTheme();
  const max = Math.max(1, ...rows.map((row) => row.value));
  const format = formatValue ?? ((value: number) => String(value));

  return (
    <View
      style={{
        backgroundColor: theme.colors.surface,
        borderRadius: radius.lg,
        borderWidth: 1,
        borderColor: theme.colors.border,
        padding: spacing.lg,
        gap: spacing.md,
        flexGrow: 1,
        flexBasis: 280,
        minWidth: 260,
        shadowColor: '#14243D',
        shadowOffset: { width: 0, height: 4 },
        shadowOpacity: theme.scheme === 'light' ? 0.04 : 0,
        shadowRadius: 12,
        elevation: theme.scheme === 'light' ? 1 : 0,
      }}>
      <AppText variant="label" style={{ color: theme.colors.text }}>
        {title}
      </AppText>
      {rows.length === 0 ? <AppText variant="muted">{emptyLabel}</AppText> : null}
      {rows.map((row) => {
        const widthPct = Math.max(8, Math.round((row.value / max) * 100));
        return (
          <View key={row.label} style={{ gap: 6 }}>
            <View style={{ flexDirection: 'row', justifyContent: 'space-between', gap: spacing.sm }}>
              <AppText variant="muted" style={{ flex: 1 }} numberOfLines={1}>
                {row.label}
              </AppText>
              <AppText variant="label" style={{ color: theme.colors.text }}>
                {format(row.value)}
              </AppText>
            </View>
            <View style={{ height: 8, borderRadius: radius.pill, backgroundColor: theme.colors.primaryLight, overflow: 'hidden' }}>
              <View
                style={{
                  width: `${widthPct}%`,
                  height: 8,
                  borderRadius: radius.pill,
                  backgroundColor: theme.colors.primary,
                }}
              />
            </View>
          </View>
        );
      })}
    </View>
  );
}
