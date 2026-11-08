import { useQuery } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useMemo, useState } from 'react';
import { Pressable, ScrollView, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { ClinicSwitcher, useSelectedDoctorClinicId } from '@/components/doctor/clinic-switcher';
import { DoctorShell } from '@/components/layout/doctor-shell';
import { Screen } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Segmented } from '@/components/ui/segmented';
import { AppText } from '@/components/ui/text';
import {
  clinicDateIso,
  normalizeScheduleDay,
  parseCalendarDate,
  sliceClock,
} from '@/features/providers/schedule-day';
import { useCopy } from '@/i18n/copy';
import { listDoctorClinics, listPracticeAppointmentsScoped } from '@/services/api/doctor-portal-api';
import { listMySchedules } from '@/services/api/provider-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

type ViewMode = 'day' | 'week' | 'month';

/** Local calendar components of a Date built via parseCalendarDate / setDate (no UTC shift). */
function isoDate(date: Date) {
  return `${date.getFullYear()}-${`${date.getMonth() + 1}`.padStart(2, '0')}-${`${date.getDate()}`.padStart(2, '0')}`;
}

function startOfWeek(date: Date) {
  const next = new Date(date);
  const day = next.getDay();
  const mondayOffset = day === 0 ? -6 : 1 - day;
  next.setDate(next.getDate() + mondayOffset);
  next.setHours(0, 0, 0, 0);
  return next;
}

function parseTime(value: string) {
  const [h, m] = sliceClock(value).split(':').map(Number);
  return (h || 0) * 60 + (m || 0);
}

export function DoctorCalendarScreen({ embedded = false }: { embedded?: boolean }) {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const [mode, setMode] = useState<ViewMode>('week');
  const [anchor, setAnchor] = useState(() => parseCalendarDate(clinicDateIso()));
  const clinics = useQuery({ queryKey: ['doctor', 'clinics'], queryFn: listDoctorClinics });
  const clinicId = useSelectedDoctorClinicId(clinics.data);
  const schedules = useQuery({ queryKey: ['schedules', 'mine'], queryFn: listMySchedules });

  const range = useMemo(() => {
    if (mode === 'day') return { from: isoDate(anchor), to: isoDate(anchor) };
    if (mode === 'week') {
      const start = startOfWeek(anchor);
      const end = new Date(start);
      end.setDate(start.getDate() + 6);
      return { from: isoDate(start), to: isoDate(end) };
    }
    const start = new Date(anchor.getFullYear(), anchor.getMonth(), 1);
    const end = new Date(anchor.getFullYear(), anchor.getMonth() + 1, 0);
    return { from: isoDate(start), to: isoDate(end) };
  }, [anchor, mode]);

  const visits = useQuery({
    queryKey: ['appointments', 'calendar', range.from, range.to, clinicId ?? 'all'],
    queryFn: () => listPracticeAppointmentsScoped(range.from, range.to, clinicId, true),
  });

  const schedule = useMemo(() => {
    const rows = schedules.data ?? [];
    return rows.find((item) => !clinicId || item.clinicId === clinicId) ?? rows[0];
  }, [clinicId, schedules.data]);

  const byDay = useMemo(() => {
    const map = new Map<string, NonNullable<typeof visits.data>>();
    for (const visit of visits.data ?? []) {
      const key = isoDate(new Date(visit.startsAtUtc));
      const rows = map.get(key) ?? [];
      rows.push(visit);
      map.set(key, rows);
    }
    return map;
  }, [visits.data]);

  const weekDays = useMemo(() => {
    const start = startOfWeek(anchor);
    return Array.from({ length: 7 }, (_, i) => {
      const day = new Date(start);
      day.setDate(start.getDate() + i);
      return day;
    });
  }, [anchor]);

  const selectedKey = isoDate(anchor);
  const selectedDayHours = useMemo(() => {
    if (!schedule) return [] as { start: number; end: number }[];
    const extras = (schedule.extras ?? [])
      .filter((item) => item.date.startsWith(selectedKey))
      .map((item) => ({ start: parseTime(item.startsAt), end: parseTime(item.endsAt) }))
      .sort((a, b) => a.start - b.start);
    if (extras.length > 0) return extras;
    const day = anchor.getDay();
    return schedule.hours
      .filter((item) => normalizeScheduleDay(item.day) === day)
      .map((item) => ({ start: parseTime(item.startsAt), end: parseTime(item.endsAt) }))
      .sort((a, b) => a.start - b.start);
  }, [anchor, schedule, selectedKey]);

  const slotMinutes = schedule?.slotDurationMinutes || 10;
  const bufferMinutes = schedule?.bufferMinutes || 0;
  const daySlots = useMemo(() => {
    if (selectedDayHours.length === 0) return [] as number[];
    const step = slotMinutes + bufferMinutes;
    const slots: number[] = [];
    for (const window of selectedDayHours) {
      for (let t = window.start; t + slotMinutes <= window.end; t += step) {
        slots.push(t);
        if (step <= 0) break;
      }
    }
    return slots;
  }, [bufferMinutes, selectedDayHours, slotMinutes]);

  const selectedVisits = byDay.get(selectedKey) ?? [];
  const isClosed = Boolean(schedule?.closures.some((item) => item.date.startsWith(selectedKey)));

  const shift = (delta: number) => {
    const next = new Date(anchor);
    if (mode === 'day') next.setDate(next.getDate() + delta);
    else if (mode === 'week') next.setDate(next.getDate() + delta * 7);
    else next.setMonth(next.getMonth() + delta);
    setAnchor(next);
  };

  const formatMinute = (minute: number) => {
    const h = Math.floor(minute / 60);
    const m = minute % 60;
    return `${`${h}`.padStart(2, '0')}:${`${m}`.padStart(2, '0')}`;
  };

  const content = (
      <Screen>
        <ScrollView
          contentContainerStyle={{
            gap: spacing.md,
            paddingBottom: spacing.xxl,
            paddingTop: embedded ? spacing.sm : Math.max(insets.top - 8, 0),
          }}>
          {embedded ? null : <ScreenHeader title={copy.doctorCalendarTitle} />}
          <ClinicSwitcher
            clinics={clinics.data ?? []}
          />
          <Segmented
            value={mode}
            onChange={setMode}
            options={[
              { value: 'day', label: copy.doctorCalendarDay },
              { value: 'week', label: copy.doctorCalendarWeek },
              { value: 'month', label: copy.doctorCalendarMonth },
            ]}
          />
          <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
            <Button label="‹" variant="secondary" onPress={() => shift(-1)} />
            <AppText variant="label" style={{ flex: 1, textAlign: 'center' }}>
              {mode === 'month'
                ? anchor.toLocaleDateString(undefined, { month: 'long', year: 'numeric' })
                : mode === 'week'
                  ? `${weekDays[0].toLocaleDateString(undefined, { day: 'numeric', month: 'short' })} – ${weekDays[6].toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' })}`
                  : anchor.toLocaleDateString(undefined, { weekday: 'short', day: 'numeric', month: 'short' })}
            </AppText>
            <Button label="›" variant="secondary" onPress={() => shift(1)} />
          </View>

          {(mode === 'week' || mode === 'day') && (
            <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: spacing.sm }}>
              {weekDays.map((day) => {
                const selected = isoDate(day) === selectedKey;
                return (
                  <Pressable
                    key={isoDate(day)}
                    onPress={() => setAnchor(day)}
                    style={{
                      width: 52,
                      borderRadius: 14,
                      paddingVertical: spacing.sm,
                      alignItems: 'center',
                      backgroundColor: selected ? theme.colors.primary : theme.colors.surface,
                      borderWidth: 1,
                      borderColor: selected ? theme.colors.primary : theme.colors.border,
                    }}>
                    <AppText
                      variant="caption"
                      style={{ color: selected ? '#FFFFFF' : theme.colors.textMuted }}>
                      {day.toLocaleDateString(undefined, { weekday: 'short' })}
                    </AppText>
                    <AppText variant="label" style={{ color: selected ? '#FFFFFF' : theme.colors.text }}>
                      {day.getDate()}
                    </AppText>
                  </Pressable>
                );
              })}
            </ScrollView>
          )}

          {mode === 'month' ? (
            <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
              {Array.from(
                { length: new Date(anchor.getFullYear(), anchor.getMonth() + 1, 0).getDate() },
                (_, i) => new Date(anchor.getFullYear(), anchor.getMonth(), i + 1),
              ).map((day) => {
                const key = isoDate(day);
                const count = byDay.get(key)?.length ?? 0;
                return (
                  <Pressable
                    key={key}
                    onPress={() => {
                      setAnchor(day);
                      setMode('day');
                    }}
                    style={{
                      width: '13%',
                      minWidth: 40,
                      aspectRatio: 1,
                      borderRadius: radius.md,
                      borderWidth: 1,
                      borderColor: theme.colors.border,
                      backgroundColor: count > 0 ? '#FCE8E8' : theme.colors.surface,
                      alignItems: 'center',
                      justifyContent: 'center',
                    }}>
                    <AppText variant="caption">{day.getDate()}</AppText>
                    {count > 0 ? (
                      <AppText variant="caption" style={{ color: theme.colors.error }}>
                        {count}
                      </AppText>
                    ) : null}
                  </Pressable>
                );
              })}
            </View>
          ) : null}

          {(mode === 'day' || mode === 'week') && (
            <View style={{ gap: spacing.sm }}>
              <View style={{ flexDirection: 'row', gap: spacing.sm, flexWrap: 'wrap' }}>
                <Legend color="#EAF3EF" label={copy.doctorSlotAvailable} border={theme.colors.primary} />
                <Legend color="#FCE8E8" label={copy.doctorSlotBooked} border="#E07A7A" />
                <Legend color="#F1F5F9" label={copy.doctorUnavailableToday} border="#94A3B8" />
              </View>
              {isClosed ? (
                <AppText variant="muted">{copy.doctorUnavailableToday}</AppText>
              ) : daySlots.length === 0 ? (
                <AppText variant="muted">{copy.doctorCalendarEmpty}</AppText>
              ) : (
                daySlots.map((minute) => {
                  const booked = selectedVisits.find((visit) => {
                    const start = new Date(visit.startsAtUtc);
                    return start.getHours() * 60 + start.getMinutes() === minute;
                  });
                  const bg = booked ? '#FCE8E8' : '#EAF3EF';
                  const label = booked
                    ? `${copy.doctorSlotBooked} · ${booked.patientName}`
                    : copy.doctorSlotAvailable;
                  return (
                    <Pressable
                      key={minute}
                      disabled={!booked}
                      onPress={() => booked && router.push(`/doctor-appointment/${booked.id}` as Href)}
                      style={{
                        borderRadius: 12,
                        borderWidth: 1,
                        borderColor: theme.colors.border,
                        backgroundColor: bg,
                        paddingHorizontal: spacing.md,
                        paddingVertical: spacing.sm + 2,
                        flexDirection: 'row',
                        justifyContent: 'space-between',
                        gap: spacing.md,
                      }}>
                      <AppText variant="caption" style={{ color: theme.colors.textMuted, minWidth: 48 }}>
                        {formatMinute(minute)}
                      </AppText>
                      <AppText variant="label" style={{ flex: 1 }} numberOfLines={1}>
                        {label}
                      </AppText>
                    </Pressable>
                  );
                })
              )}
            </View>
          )}

          <Button
            label={selectedKey === isoDate(new Date()) ? copy.doctorAvailabilityUpdate : copy.doctorEditSelectedDate}
            variant="secondary"
            onPress={() => router.push(`/doctor-today-timing?date=${selectedKey}` as Href)}
          />
          {visits.error ? <AppText variant="muted">{problemMessage(visits.error, copy.doctorCalendarTitle)}</AppText> : null}
        </ScrollView>
      </Screen>
  );

  if (embedded) {
    return content;
  }

  return <DoctorShell active="schedule">{content}</DoctorShell>;
}

function Legend({ color, label, border }: { color: string; label: string; border: string }) {
  const theme = useProdocTheme();
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
      <View
        style={{
          width: 12,
          height: 12,
          borderRadius: 4,
          backgroundColor: color,
          borderWidth: 1,
          borderColor: border,
        }}
      />
      <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
        {label}
      </AppText>
    </View>
  );
}
