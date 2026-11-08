import { useMemo, useState } from 'react';
import { Pressable, ScrollView, View } from 'react-native';
import Animated, { FadeIn } from 'react-native-reanimated';

import { AppText } from '@/components/ui/text';
import {
  addCalendarDays,
  clinicDateIso,
  parseCalendarDate,
  patientBookingWindow,
  startOfClinicWeekMonday,
} from '@/features/providers/schedule-day';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { duration, radius, spacing } from '@/theme/tokens';

export type CalendarDay = {
  date: string;
  status: string;
  label?: string;
  weekday?: string;
  dayNum?: string;
  slotCount?: number;
};

function parseYmd(isoDate: string) {
  return parseCalendarDate(isoDate);
}

function toYmd(date: Date) {
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

function startOfMonth(date: Date) {
  return new Date(date.getFullYear(), date.getMonth(), 1);
}

function addMonths(date: Date, delta: number) {
  return new Date(date.getFullYear(), date.getMonth() + delta, 1);
}

const WEEKDAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

function statusColors(
  theme: ReturnType<typeof useProdocTheme>,
  status: string | undefined,
  available: boolean,
  selected: boolean,
) {
  if (selected) {
    return { background: theme.colors.primary, border: theme.colors.primary, text: theme.colors.onPrimary };
  }
  if (status === 'full') {
    return { background: '#FCE8E8', border: '#E07A7A', text: theme.colors.text };
  }
  if (status === 'leave' || status === 'holiday') {
    return { background: '#FEF3C7', border: '#D97706', text: theme.colors.textMuted };
  }
  if (status === 'unavailable' || status === 'unconfigured') {
    return { background: '#F1F5F9', border: '#94A3B8', text: theme.colors.textMuted };
  }
  if (status === 'partial') {
    return { background: theme.colors.primaryLight, border: theme.colors.primary, text: theme.colors.text };
  }
  if (available) {
    return { background: theme.colors.primaryLight, border: theme.colors.primary, text: theme.colors.text };
  }
  return { background: theme.colors.surface, border: theme.colors.border, text: theme.colors.textMuted };
}

/** Month calendar (staff) or compact current/next-week booking calendar (patients). */
export function CalendarView({
  days,
  selectedDate,
  onSelect,
  statusLabels,
  availableStatus = 'available',
  monthCursor,
  onMonthChange,
  mode = 'month',
  weekCursor,
  onWeekChange,
}: {
  days: CalendarDay[];
  selectedDate: string;
  onSelect: (date: string) => void;
  statusLabels?: Record<string, string>;
  availableStatus?: string;
  /** Controlled month (YYYY-MM-01). When omitted, derived from selectedDate. */
  monthCursor?: string;
  onMonthChange?: (monthStartYmd: string) => void;
  /** `bookingWeeks` = current week + next week only. */
  mode?: 'month' | 'bookingWeeks';
  /** Monday of the displayed week (YYYY-MM-DD). */
  weekCursor?: string;
  onWeekChange?: (weekStartYmd: string) => void;
}) {
  const theme = useProdocTheme();
  const todayYmd = clinicDateIso();
  const bookingWindow = useMemo(() => patientBookingWindow(todayYmd), [todayYmd]);
  const bookingMode = mode === 'bookingWeeks';

  const [internalMonth, setInternalMonth] = useState(() => toYmd(startOfMonth(parseYmd(selectedDate || todayYmd))));
  const [internalWeek, setInternalWeek] = useState(() => startOfClinicWeekMonday(selectedDate || todayYmd));

  const monthStartYmd = monthCursor ?? internalMonth;
  const monthStart = startOfMonth(parseYmd(monthStartYmd));
  const weekStartYmd = weekCursor ?? internalWeek;

  const byDate = useMemo(() => {
    const map = new Map<string, CalendarDay>();
    for (const day of days) map.set(day.date.slice(0, 10), day);
    return map;
  }, [days]);

  const weekDays = useMemo(() => {
    const weekStart = parseYmd(weekStartYmd);
    return Array.from({ length: 7 }, (_, index) => {
      const date = new Date(weekStart);
      date.setDate(weekStart.getDate() + index);
      return toYmd(date);
    });
  }, [weekStartYmd]);

  const monthCells = useMemo(() => {
    const firstWeekday = monthStart.getDay();
    const daysInMonth = new Date(monthStart.getFullYear(), monthStart.getMonth() + 1, 0).getDate();
    const leading = Array.from({ length: firstWeekday }, () => null as string | null);
    const monthDayList = Array.from({ length: daysInMonth }, (_, index) => {
      const date = new Date(monthStart.getFullYear(), monthStart.getMonth(), index + 1);
      return toYmd(date);
    });
    const total = leading.length + monthDayList.length;
    const trailing = Array.from({ length: (7 - (total % 7)) % 7 }, () => null as string | null);
    return [...leading, ...monthDayList, ...trailing];
  }, [monthStart]);

  if (bookingMode) {
    const isCurrentWeek = weekStartYmd === bookingWindow.currentWeekStart;
    const isNextWeek = weekStartYmd === bookingWindow.nextWeekStart;
    const titleStart = parseYmd(weekDays[0]);
    const titleEnd = parseYmd(weekDays[6]);
    const title = `${titleStart.toLocaleDateString(undefined, { day: 'numeric', month: 'short' })} – ${titleEnd.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' })}`;

    const shiftWeek = (delta: -1 | 1) => {
      if (delta < 0 && isCurrentWeek) return;
      if (delta > 0 && isNextWeek) return;
      const next = addCalendarDays(weekStartYmd, delta * 7);
      if (next < bookingWindow.currentWeekStart || next > bookingWindow.nextWeekStart) return;
      if (onWeekChange) onWeekChange(next);
      else setInternalWeek(next);
    };

    return (
      <View style={{ gap: spacing.md, width: '100%' }}>
        <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
          <Pressable
            accessibilityRole="button"
            accessibilityState={{ disabled: isCurrentWeek }}
            disabled={isCurrentWeek}
            onPress={() => shiftWeek(-1)}
            style={{
              paddingHorizontal: spacing.md,
              paddingVertical: spacing.sm,
              borderRadius: radius.md,
              backgroundColor: theme.colors.primaryLight,
              opacity: isCurrentWeek ? 0.4 : 1,
            }}>
            <AppText style={{ color: theme.colors.primaryPressed }}>‹</AppText>
          </Pressable>
          <AppText variant="headline" style={{ flex: 1, textAlign: 'center' }}>
            {title}
          </AppText>
          <Pressable
            accessibilityRole="button"
            accessibilityState={{ disabled: isNextWeek }}
            disabled={isNextWeek}
            onPress={() => shiftWeek(1)}
            style={{
              paddingHorizontal: spacing.md,
              paddingVertical: spacing.sm,
              borderRadius: radius.md,
              backgroundColor: theme.colors.primaryLight,
              opacity: isNextWeek ? 0.4 : 1,
            }}>
            <AppText style={{ color: theme.colors.primaryPressed }}>›</AppText>
          </Pressable>
        </View>

        <ScrollView
          horizontal
          showsHorizontalScrollIndicator={false}
          contentContainerStyle={{ gap: spacing.sm, paddingVertical: 2 }}>
          {weekDays.map((ymd) => {
            const meta = byDate.get(ymd);
            const isPast = ymd < todayYmd;
            const available =
              !isPast && (meta?.status === availableStatus || meta?.status === 'partial');
            const selected = selectedDate === ymd;
            const isToday = ymd === todayYmd;
            const colors = statusColors(theme, meta?.status, available, selected);
            const dayDate = parseYmd(ymd);
            const statusText =
              isPast || !meta
                ? statusLabels?.unavailable ?? 'Unavailable'
                : statusLabels?.[meta.status] ?? meta.status;
            return (
              <Pressable
                key={ymd}
                disabled={!available}
                accessibilityRole="button"
                accessibilityState={{ selected, disabled: !available }}
                onPress={() => onSelect(ymd)}
                style={{ width: 64 }}>
                <View
                  style={{
                    height: 84,
                    borderRadius: radius.md,
                    alignItems: 'center',
                    justifyContent: 'center',
                    paddingHorizontal: 4,
                    paddingVertical: spacing.sm,
                    gap: 2,
                    borderWidth: selected || isToday ? 1.5 : 1,
                    borderColor: selected || isToday ? theme.colors.primary : colors.border,
                    backgroundColor: colors.background,
                    opacity: available || selected ? 1 : 0.65,
                  }}>
                  <AppText
                    variant="caption"
                    numberOfLines={1}
                    style={{ color: selected ? theme.colors.onPrimary : theme.colors.textMuted, fontSize: 11 }}>
                    {dayDate.toLocaleDateString(undefined, { weekday: 'short' })}
                  </AppText>
                  <AppText variant="label" style={{ color: colors.text, fontSize: 16 }}>
                    {dayDate.getDate()}
                  </AppText>
                  {available && !selected ? (
                    <View
                      style={{
                        width: 5,
                        height: 5,
                        borderRadius: 3,
                        backgroundColor: theme.colors.success,
                      }}
                    />
                  ) : (
                    <View style={{ height: 5 }} />
                  )}
                  <AppText
                    variant="caption"
                    numberOfLines={1}
                    style={{
                      fontSize: 9,
                      color: selected ? theme.colors.onPrimary : theme.colors.textMuted,
                      textAlign: 'center',
                    }}>
                    {statusText}
                  </AppText>
                </View>
              </Pressable>
            );
          })}
        </ScrollView>

        {theme.reduceMotion ? null : <Animated.View entering={FadeIn.duration(duration.enter)} />}
      </View>
    );
  }

  const title = monthStart.toLocaleDateString(undefined, { month: 'long', year: 'numeric' });

  const shiftMonth = (delta: number) => {
    const next = toYmd(addMonths(monthStart, delta));
    if (onMonthChange) onMonthChange(next);
    else setInternalMonth(next);
  };

  return (
    <View style={{ gap: spacing.md, width: '100%' }}>
      <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
        <Pressable
          accessibilityRole="button"
          onPress={() => shiftMonth(-1)}
          style={{
            paddingHorizontal: spacing.md,
            paddingVertical: spacing.sm,
            borderRadius: radius.md,
            backgroundColor: theme.colors.primaryLight,
          }}>
          <AppText style={{ color: theme.colors.primaryPressed }}>‹</AppText>
        </Pressable>
        <AppText variant="headline">{title}</AppText>
        <Pressable
          accessibilityRole="button"
          onPress={() => shiftMonth(1)}
          style={{
            paddingHorizontal: spacing.md,
            paddingVertical: spacing.sm,
            borderRadius: radius.md,
            backgroundColor: theme.colors.primaryLight,
          }}>
          <AppText style={{ color: theme.colors.primaryPressed }}>›</AppText>
        </Pressable>
      </View>

      <View style={{ flexDirection: 'row' }}>
        {WEEKDAYS.map((label) => (
          <View key={label} style={{ flex: 1, alignItems: 'center', paddingVertical: spacing.xs }}>
            <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
              {label}
            </AppText>
          </View>
        ))}
      </View>

      <View style={{ flexDirection: 'row', flexWrap: 'wrap' }}>
        {monthCells.map((ymd, index) => {
          if (!ymd) {
            return <View key={`empty-${index}`} style={{ width: `${100 / 7}%`, aspectRatio: 1 }} />;
          }

          const meta = byDate.get(ymd);
          const isPast = ymd < todayYmd;
          const available =
            !isPast && (meta?.status === availableStatus || meta?.status === 'partial');
          const selected = selectedDate === ymd;
          const isToday = ymd === todayYmd;
          const dayNum = String(parseYmd(ymd).getDate());
          const colors = statusColors(theme, meta?.status, available, selected);

          return (
            <Pressable
              key={ymd}
              disabled={!available}
              accessibilityRole="button"
              accessibilityState={{ selected, disabled: !available }}
              onPress={() => onSelect(ymd)}
              style={{
                width: `${100 / 7}%`,
                aspectRatio: 1,
                padding: 2,
              }}>
              <View
                style={{
                  flex: 1,
                  borderRadius: radius.md,
                  alignItems: 'center',
                  justifyContent: 'center',
                  borderWidth: selected || isToday ? 1.5 : 1,
                  borderColor: selected || isToday ? theme.colors.primary : colors.border,
                  backgroundColor: colors.background,
                  opacity: available || selected ? 1 : 0.45,
                }}>
                <AppText
                  variant="label"
                  style={{
                    color: colors.text,
                    fontSize: 14,
                  }}>
                  {dayNum}
                </AppText>
                {available && !selected ? (
                  <View
                    style={{
                      width: 5,
                      height: 5,
                      borderRadius: 3,
                      marginTop: 2,
                      backgroundColor: theme.colors.success,
                    }}
                  />
                ) : null}
                {statusLabels?.[meta?.status ?? ''] && !selected ? (
                  <AppText variant="caption" style={{ fontSize: 8, color: theme.colors.textMuted }} numberOfLines={1}>
                    {statusLabels[meta!.status]}
                  </AppText>
                ) : null}
              </View>
            </Pressable>
          );
        })}
      </View>

      {theme.reduceMotion ? null : <Animated.View entering={FadeIn.duration(duration.enter)} />}
    </View>
  );
}
