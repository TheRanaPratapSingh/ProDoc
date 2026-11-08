import { Pressable, View } from 'react-native';

import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';

export type HourlyRangeOption = {
  start: string;
  end: string;
  label: string;
  bookedCount?: number | null;
  capacity?: number | null;
  remaining?: number | null;
  available?: boolean;
  tokenPrefix?: string | null;
};

function formatClock(value: string) {
  const parts = value.split(':');
  const hour = Number(parts[0] ?? 0);
  const minute = Number(parts[1] ?? 0);
  const suffix = hour >= 12 ? 'PM' : 'AM';
  const twelve = hour % 12 === 0 ? 12 : hour % 12;
  if (minute === 0) {
    return `${twelve} ${suffix}`;
  }
  return `${twelve}:${String(minute).padStart(2, '0')} ${suffix}`;
}

export function formatHourlyRangeLabel(start: string, end: string) {
  return `${formatClock(start)} – ${formatClock(end)}`;
}

/** Full-width hourly range cards for queue booking. */
export function HourlyRangeSelector({
  ranges,
  selectedStart,
  onSelect,
  emptyLabel,
  busy = false,
  bookedLabel,
  availableLabel,
  fullLabel,
  remainingLabel,
}: {
  ranges: HourlyRangeOption[];
  selectedStart?: string | null;
  onSelect: (start: string) => void;
  emptyLabel?: string;
  busy?: boolean;
  bookedLabel: string;
  availableLabel: string;
  fullLabel: string;
  remainingLabel: string;
}) {
  const theme = useProdocTheme();

  if (ranges.length === 0) {
    return emptyLabel ? <AppText variant="muted">{emptyLabel}</AppText> : null;
  }

  return (
    <View style={{ gap: spacing.sm, width: '100%' }}>
      {ranges.map((range) => {
        const selected = selectedStart === range.start;
        const disabled = busy || range.available === false;
        const status =
          range.available === false
            ? fullLabel
            : range.remaining != null
              ? `${remainingLabel}: ${range.remaining}`
              : availableLabel;
        return (
          <Pressable
            key={range.start}
            accessibilityRole="button"
            accessibilityState={{ selected, disabled }}
            disabled={disabled}
            onPress={() => onSelect(range.start)}
            style={{
              borderWidth: 1.5,
              borderColor: selected ? theme.colors.primary : theme.colors.border,
              backgroundColor: selected ? theme.colors.primaryLight : theme.colors.surface,
              borderRadius: radius.md,
              padding: spacing.md,
              opacity: disabled ? 0.55 : 1,
              gap: spacing.xs,
            }}>
            <AppText variant="label" style={{ color: theme.colors.text }}>
              {range.label}
              {range.tokenPrefix ? ` · ${range.tokenPrefix}` : ''}
            </AppText>
            <AppText variant="muted">
              {bookedLabel}: {range.bookedCount ?? 0}
              {range.capacity != null ? ` / ${range.capacity}` : ''}
            </AppText>
            <AppText variant="muted" style={{ color: range.available === false ? theme.colors.error : theme.colors.primary }}>
              {status}
            </AppText>
          </Pressable>
        );
      })}
    </View>
  );
}
