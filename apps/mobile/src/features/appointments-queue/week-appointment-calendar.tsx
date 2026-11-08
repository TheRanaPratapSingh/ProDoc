import { useMemo, useState } from 'react';
import { Pressable, ScrollView, View } from 'react-native';

import { BottomSheet } from '@/components/ui/bottom-sheet';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { EmptyState } from '@/components/ui/empty-state';
import { AppText } from '@/components/ui/text';
import { addCalendarDays, clinicDateIso, parseCalendarDate } from '@/features/providers/schedule-day';
import { useCopy } from '@/i18n/copy';
import type { AppointmentDetails } from '@/services/api/provider-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';
import { formatVisitDate, formatVisitTime, paymentStatusLabel } from '@/utils/visit-format';

function appointmentDayKey(visit: AppointmentDetails) {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: visit.timeZoneId || 'Asia/Kolkata',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date(visit.startsAtUtc));
}

/** Compact today + next 6 days calendar with day detail sheet. */
export function WeekAppointmentCalendar({
  visits,
  loading,
  error,
  onOpenDetails,
}: {
  visits: AppointmentDetails[];
  loading?: boolean;
  error?: string | null;
  onOpenDetails?: (appointmentId: string) => void;
}) {
  const copy = useCopy();
  const theme = useProdocTheme();
  const today = clinicDateIso();
  const [selectedDate, setSelectedDate] = useState(today);
  const [sheetOpen, setSheetOpen] = useState(false);

  const days = useMemo(
    () => Array.from({ length: 7 }, (_, index) => addCalendarDays(today, index)),
    [today],
  );

  const byDay = useMemo(() => {
    const map = new Map<string, AppointmentDetails[]>();
    for (const visit of visits) {
      const key = appointmentDayKey(visit);
      const rows = map.get(key) ?? [];
      rows.push(visit);
      map.set(key, rows);
    }
    for (const [key, rows] of map) {
      rows.sort((a, b) => new Date(a.startsAtUtc).getTime() - new Date(b.startsAtUtc).getTime());
      map.set(key, rows);
    }
    return map;
  }, [visits]);

  const selectedVisits = byDay.get(selectedDate) ?? [];
  const selectedLabel = formatVisitDate(`${selectedDate}T12:00:00+05:30`, 'Asia/Kolkata');
  const confirmed = selectedVisits.filter((item) =>
    ['Confirmed', 'CheckedIn', 'Requested', 'Held'].includes(item.status),
  ).length;
  const completed = selectedVisits.filter((item) => item.status === 'Completed').length;
  const cancelled = selectedVisits.filter((item) =>
    ['Cancelled', 'Rejected', 'Expired'].includes(item.status),
  ).length;

  const openDay = (ymd: string) => {
    setSelectedDate(ymd);
    setSheetOpen(true);
  };

  return (
    <View style={{ gap: spacing.lg }}>
      <AppText variant="muted">{copy.aqCalendarHint}</AppText>
      {loading ? <AppText variant="muted">{copy.loading}</AppText> : null}
      {error ? <AppText variant="muted">{error}</AppText> : null}

      <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: spacing.sm }}>
        {days.map((ymd) => {
          const count = (byDay.get(ymd) ?? []).filter(
            (item) => !['Cancelled', 'Rejected', 'Expired'].includes(item.status),
          ).length;
          const selected = selectedDate === ymd;
          const isToday = ymd === today;
          const day = parseCalendarDate(ymd);
          return (
            <Pressable
              key={ymd}
              onPress={() => openDay(ymd)}
              style={{
                width: 72,
                height: 92,
                borderRadius: radius.lg,
                borderWidth: selected || isToday ? 1.5 : 1,
                borderColor: selected || isToday ? theme.colors.primary : theme.colors.border,
                backgroundColor: selected ? theme.colors.primary : theme.colors.surface,
                alignItems: 'center',
                justifyContent: 'center',
                gap: 2,
                padding: spacing.sm,
              }}>
              <AppText
                variant="caption"
                style={{ color: selected ? theme.colors.onPrimary : theme.colors.textMuted, fontSize: 11 }}>
                {day.toLocaleDateString(undefined, { weekday: 'short' })}
              </AppText>
              <AppText variant="headline" style={{ color: selected ? theme.colors.onPrimary : theme.colors.text }}>
                {day.getDate()}
              </AppText>
              {count > 0 ? (
                <View
                  style={{
                    marginTop: 2,
                    minWidth: 22,
                    paddingHorizontal: 6,
                    paddingVertical: 2,
                    borderRadius: radius.pill,
                    backgroundColor: selected ? 'rgba(255,255,255,0.22)' : theme.colors.primaryLight,
                  }}>
                  <AppText
                    variant="caption"
                    style={{
                      color: selected ? theme.colors.onPrimary : theme.colors.primaryPressed,
                      textAlign: 'center',
                      fontSize: 10,
                    }}>
                    {count}
                  </AppText>
                </View>
              ) : (
                <AppText
                  variant="caption"
                  style={{ color: selected ? theme.colors.onPrimary : theme.colors.textMuted, fontSize: 9 }}>
                  {copy.aqNoBookings}
                </AppText>
              )}
            </Pressable>
          );
        })}
      </ScrollView>

      <Card elevated style={{ gap: spacing.sm }}>
        <AppText variant="label">{selectedLabel}</AppText>
        <AppText variant="muted">
          {copy.aqTotalAppointments}: {selectedVisits.length}
        </AppText>
        <Button label={copy.aqViewDayAppointments} onPress={() => setSheetOpen(true)} />
      </Card>

      <BottomSheet visible={sheetOpen} title={selectedLabel} onClose={() => setSheetOpen(false)}>
        <ScrollView style={{ maxHeight: 420 }} contentContainerStyle={{ gap: spacing.md, paddingBottom: spacing.lg }}>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md }}>
            <AppText variant="muted">
              {copy.aqTotalAppointments}: {selectedVisits.length}
            </AppText>
            <AppText variant="muted">
              {copy.aqConfirmed}: {confirmed}
            </AppText>
            <AppText variant="muted">
              {copy.aqCompleted}: {completed}
            </AppText>
            <AppText variant="muted">
              {copy.aqCancelled}: {cancelled}
            </AppText>
          </View>

          {selectedVisits.length === 0 ? <EmptyState title={copy.aqNoAppointmentsOnDate} /> : null}

          {selectedVisits.map((visit) => (
            <Card key={visit.id} style={{ gap: spacing.xs }}>
              <View style={{ flexDirection: 'row', justifyContent: 'space-between', gap: spacing.sm }}>
                <AppText variant="label">{visit.tokenLabel ?? '—'}</AppText>
                <AppText variant="caption">{visit.status}</AppText>
              </View>
              <AppText>{visit.patientName}</AppText>
              <AppText variant="muted">
                {formatVisitTime(visit.startsAtUtc, visit.timeZoneId)} · {visit.reference ?? visit.id.slice(0, 8)}
              </AppText>
              <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                {paymentStatusLabel(visit.paymentStatus, visit.paymentMethod, {
                  paid: copy.paid,
                  payAtClinic: copy.payAtClinic,
                  paymentPending: copy.paymentPending,
                })}
              </AppText>
              {onOpenDetails ? (
                <Button label={copy.aqViewDetails} variant="secondary" onPress={() => onOpenDetails(visit.id)} />
              ) : null}
            </Card>
          ))}

          <Button label={copy.closeQr} variant="secondary" onPress={() => setSheetOpen(false)} />
        </ScrollView>
      </BottomSheet>
    </View>
  );
}
