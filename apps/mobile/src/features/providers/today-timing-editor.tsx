import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useEffect, useMemo, useState } from 'react';
import { Alert, Pressable, ScrollView, Switch, View } from 'react-native';
import { ClinicSwitcher, useSelectedDoctorClinicId } from '@/components/doctor/clinic-switcher';
import { DoctorShell } from '@/components/layout/doctor-shell';
import { Screen } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import {
  addCalendarDays,
  clocksOverlap,
  isValidCalendarDate,
  isValidClockRange,
  normalizeScheduleDay,
  parseCalendarDate,
  sliceClock,
  todayIsoLocal,
  weekdayName,
} from '@/features/providers/schedule-day';
import { useCopy } from '@/i18n/copy';
import { listDoctorClinics } from '@/services/api/doctor-portal-api';
import {
  copyDaySchedule,
  createSchedule,
  getClosureImpact,
  listMySchedules,
  replaceDayOverride,
  resetDayOverride,
  type ScheduleDetails,
} from '@/services/api/provider-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { classifyApiError, problemMessage } from '@/utils/problem';

type SessionDraft = {
  id: string;
  label: string;
  enabled: boolean;
  startsAt: string;
  endsAt: string;
};

function dateParam(value: string | string[] | undefined) {
  const raw = Array.isArray(value) ? value[0] : value;
  if (raw && isValidCalendarDate(raw)) return raw;
  return todayIsoLocal();
}

function effectiveWindows(schedule: ScheduleDetails | undefined, isoDate: string) {
  if (!schedule) return [] as { startsAt: string; endsAt: string }[];
  const extras = (schedule.extras ?? [])
    .filter((item) => item.date.startsWith(isoDate))
    .map((item) => ({ startsAt: sliceClock(item.startsAt), endsAt: sliceClock(item.endsAt) }))
    .sort((a, b) => a.startsAt.localeCompare(b.startsAt));
  if (extras.length > 0) return extras;

  const day = parseCalendarDate(isoDate).getDay();
  return (schedule.hours ?? [])
    .filter((item) => normalizeScheduleDay(item.day) === day)
    .map((item) => ({ startsAt: sliceClock(item.startsAt), endsAt: sliceClock(item.endsAt) }))
    .sort((a, b) => a.startsAt.localeCompare(b.startsAt));
}

function buildSessions(schedule: ScheduleDetails | undefined, isoDate: string): SessionDraft[] {
  const windows = effectiveWindows(schedule, isoDate);
  if (windows.length === 0) {
    return [{ id: 'session-0', label: 'Consultation', enabled: true, startsAt: '11:00', endsAt: '17:00' }];
  }
  return windows.map((item, index) => ({
    id: `session-${index}`,
    label: index === 0 ? 'Consultation' : `Session ${index + 1}`,
    enabled: true,
    startsAt: item.startsAt,
    endsAt: item.endsAt,
  }));
}

export function TodayTimingEditorScreen() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const queryClient = useQueryClient();
  const params = useLocalSearchParams<{ date?: string }>();
  const selectedDate = dateParam(params.date);
  const day = parseCalendarDate(selectedDate).getDay();
  const dayLabel = weekdayName(day);
  const isToday = selectedDate === todayIsoLocal();

  const clinics = useQuery({ queryKey: ['doctor', 'clinics'], queryFn: listDoctorClinics });
  const clinicId = useSelectedDoctorClinicId(clinics.data);
  const schedules = useQuery({ queryKey: ['schedules', 'mine'], queryFn: listMySchedules });

  const selectedClinic = clinics.data?.find((item) => item.clinicId === clinicId) ?? clinics.data?.[0];
  const schedule = useMemo(() => {
    const rows = schedules.data ?? [];
    const scoped = rows.filter((item) => {
      if (!clinicId) return true;
      return item.clinicId === clinicId;
    });
    return scoped.find((item) => item.clinicId === clinicId) ?? scoped[0] ?? rows[0];
  }, [clinicId, schedules.data]);

  const [sessions, setSessions] = useState<SessionDraft[]>(() => buildSessions(schedule, selectedDate));
  const [available, setAvailable] = useState(true);
  const [leaveReason, setLeaveReason] = useState('Personal Leave');
  const [copyTargets, setCopyTargets] = useState('');
  const [formError, setFormError] = useState<string | null>(null);

  useEffect(() => {
    if (!schedule) return;
    setSessions(buildSessions(schedule, selectedDate));
    const closed = schedule.closures.some((item) => item.date.startsWith(selectedDate));
    const windows = effectiveWindows(schedule, selectedDate);
    setAvailable(schedule.isPublished && !closed && windows.length > 0);
    const reason = schedule.closures.find((item) => item.date.startsWith(selectedDate))?.reason;
    if (reason) setLeaveReason(reason);
  }, [schedule, selectedDate]);

  const refreshAll = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['schedules'] }),
      queryClient.invalidateQueries({ queryKey: ['doctor'] }),
      queryClient.invalidateQueries({ queryKey: ['appointments'] }),
    ]);
  };

  const ensureSchedule = async () =>
    schedule ??
    (await createSchedule({
      clinicId: selectedClinic?.clinicId,
      branchId: selectedClinic?.branchId ?? undefined,
      consultationFee: 300,
      slotDurationMinutes: 10,
      bufferMinutes: 0,
    }));

  const validateSessions = (activeSessions: SessionDraft[]) => {
    if (available && activeSessions.length === 0) {
      throw new Error(copy.doctorTimingNeedSession);
    }
    for (const session of activeSessions) {
      if (!isValidClockRange(session.startsAt, session.endsAt)) {
        throw new Error(copy.doctorTimingInvalidRange);
      }
    }
    for (let i = 0; i < activeSessions.length; i += 1) {
      for (let j = i + 1; j < activeSessions.length; j += 1) {
        if (
          clocksOverlap(
            activeSessions[i].startsAt,
            activeSessions[i].endsAt,
            activeSessions[j].startsAt,
            activeSessions[j].endsAt,
          )
        ) {
          throw new Error(copy.doctorTimingOverlap);
        }
      }
    }
  };

  const save = useMutation({
    mutationFn: async () => {
      setFormError(null);
      const activeSessions = sessions.filter((item) => item.enabled);
      validateSessions(activeSessions);
      let current = await ensureSchedule();

      if (!available) {
        const impact = await getClosureImpact(current.id, selectedDate);
        if (impact.confirmedAppointments > 0) {
          const confirmed = await new Promise<boolean>((resolve) => {
            Alert.alert(
              copy.doctorLeaveImpactTitle,
              copy.doctorLeaveImpactBody.replace('{count}', String(impact.confirmedAppointments)),
              [
                { text: copy.cancel, style: 'cancel', onPress: () => resolve(false) },
                { text: copy.save, style: 'destructive', onPress: () => resolve(true) },
              ],
            );
          });
          if (!confirmed) throw new Error(copy.cancel);
        }
      }

      current = await replaceDayOverride(current.id, {
        date: selectedDate,
        available,
        unavailableReason: available ? undefined : leaveReason || 'Unavailable',
        windows: available
          ? activeSessions.map((item) => ({ startsAt: item.startsAt, endsAt: item.endsAt }))
          : [],
        expectedRowVersion: current.rowVersion,
      });
      return current;
    },
    onSuccess: async () => {
      await refreshAll();
      Alert.alert(copy.save, copy.doctorTimingSaved);
      router.back();
    },
    onError: (error) => {
      if (error instanceof Error && error.message === copy.cancel) return;
      if (classifyApiError(error) === 'conflict') {
        setFormError(copy.scheduleConflictBody);
        void refreshAll();
        return;
      }
      setFormError(
        error instanceof Error && error.message && !String(error.message).includes('axios')
          ? error.message
          : problemMessage(error, copy.doctorAvailabilityUpdate),
      );
    },
  });

  const reset = useMutation({
    mutationFn: async () => {
      const current = await ensureSchedule();
      return resetDayOverride(current.id, selectedDate, current.rowVersion);
    },
    onSuccess: async () => {
      await refreshAll();
      Alert.alert(copy.save, copy.doctorResetToDefaultSaved);
    },
    onError: (error) => setFormError(problemMessage(error, copy.doctorResetToDefault)),
  });

  const copySchedule = useMutation({
    mutationFn: async (overwriteExisting: boolean) => {
      const current = await ensureSchedule();
      const targets = copyTargets
        .split(/[\s,]+/)
        .map((item) => item.trim())
        .filter(isValidCalendarDate);
      if (targets.length === 0) {
        throw new Error(copy.doctorCopyScheduleNeedDates);
      }
      return copyDaySchedule(current.id, {
        sourceDate: selectedDate,
        targetDates: targets,
        overwriteExisting,
        expectedRowVersion: current.rowVersion,
      });
    },
    onSuccess: async () => {
      await refreshAll();
      Alert.alert(copy.save, copy.doctorCopyScheduleSaved);
    },
    onError: (error) =>
      setFormError(
        error instanceof Error && !String(error.message).includes('axios')
          ? error.message
          : problemMessage(error, copy.doctorCopySchedule),
      ),
  });

  const runCopy = () => {
    Alert.alert(copy.doctorCopySchedule, copy.doctorCopyScheduleConfirm, [
      { text: copy.cancel, style: 'cancel' },
      {
        text: copy.doctorCopySkipExisting,
        onPress: () => copySchedule.mutate(false),
      },
      {
        text: copy.doctorCopyOverwrite,
        style: 'destructive',
        onPress: () => copySchedule.mutate(true),
      },
    ]);
  };

  const title = isToday ? copy.doctorAvailabilityUpdate : copy.doctorEditSelectedDate;
  const availableLabel = isToday
    ? available
      ? copy.doctorAvailableToday
      : copy.doctorUnavailableToday
    : available
      ? copy.doctorAvailableSelectedDate
      : copy.doctorUnavailableSelectedDate;

  return (
    <DoctorShell active="availability">
      <Screen>
        <ScrollView
          keyboardShouldPersistTaps="handled"
          contentContainerStyle={{
            gap: spacing.md,
            paddingBottom: spacing.xxl,
            // Screen already applies top SafeArea; avoid stacking a second inset.
            paddingTop: spacing.sm,
          }}>
          <ScreenHeader title={title} showBack onBack={() => router.back()} />
          <ClinicSwitcher clinics={clinics.data ?? []} onChanged={() => void refreshAll()} />
          <AppText variant="muted">
            {dayLabel} · {selectedDate}
            {selectedClinic ? ` · ${selectedClinic.name}` : ''}
          </AppText>

          <View
            style={{
              backgroundColor: theme.colors.surface,
              borderRadius: 16,
              borderWidth: 1,
              borderColor: theme.colors.border,
              padding: spacing.lg,
              gap: spacing.md,
            }}>
            <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
              <AppText variant="label">{availableLabel}</AppText>
              <Switch value={available} onValueChange={setAvailable} />
            </View>

            {!available ? (
              <TextField label={copy.reason} value={leaveReason} onChangeText={setLeaveReason} />
            ) : null}

            {sessions.map((session) => (
              <View
                key={session.id}
                style={{
                  gap: spacing.sm,
                  paddingTop: spacing.sm,
                  borderTopWidth: 1,
                  borderTopColor: theme.colors.border,
                }}>
                <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
                  <AppText variant="label">{session.label}</AppText>
                  <Switch
                    value={session.enabled}
                    onValueChange={(enabled) =>
                      setSessions((rows) => rows.map((row) => (row.id === session.id ? { ...row, enabled } : row)))
                    }
                    disabled={!available}
                  />
                </View>
                <View style={{ flexDirection: 'row', gap: spacing.sm }}>
                  <View style={{ flex: 1 }}>
                    <TextField
                      label={copy.hoursStart}
                      value={session.startsAt}
                      onChangeText={(startsAt) =>
                        setSessions((rows) => rows.map((row) => (row.id === session.id ? { ...row, startsAt } : row)))
                      }
                      editable={available && session.enabled}
                    />
                  </View>
                  <View style={{ flex: 1 }}>
                    <TextField
                      label={copy.hoursEnd}
                      value={session.endsAt}
                      onChangeText={(endsAt) =>
                        setSessions((rows) => rows.map((row) => (row.id === session.id ? { ...row, endsAt } : row)))
                      }
                      editable={available && session.enabled}
                    />
                  </View>
                </View>
              </View>
            ))}

            <Pressable
              onPress={() =>
                setSessions((rows) => [
                  ...rows,
                  {
                    id: `session-${rows.length + 1}`,
                    label: `Session ${rows.length + 1}`,
                    enabled: true,
                    startsAt: '15:00',
                    endsAt: '16:00',
                  },
                ])
              }
              disabled={!available}>
              <AppText variant="label" style={{ color: theme.colors.primary }}>
                {copy.doctorAddSession}
              </AppText>
            </Pressable>
          </View>

          <View
            style={{
              backgroundColor: theme.colors.surface,
              borderRadius: 16,
              borderWidth: 1,
              borderColor: theme.colors.border,
              padding: spacing.lg,
              gap: spacing.sm,
            }}>
            <AppText variant="label">{copy.doctorCopySchedule}</AppText>
            <AppText variant="muted">{copy.doctorCopyScheduleHint}</AppText>
            <TextField
              label={copy.doctorCopyTargetDates}
              value={copyTargets}
              onChangeText={setCopyTargets}
              placeholder={`${addCalendarDays(selectedDate, 1)}, ${addCalendarDays(selectedDate, 2)}`}
            />
            <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
              <Button
                label={copy.doctorCopyRestOfWeek}
                variant="secondary"
                onPress={() => {
                  const start = parseCalendarDate(selectedDate);
                  const mondayOffset = start.getDay() === 0 ? -6 : 1 - start.getDay();
                  const monday = new Date(start);
                  monday.setDate(start.getDate() + mondayOffset);
                  const dates: string[] = [];
                  for (let i = 0; i < 7; i += 1) {
                    const dayDate = new Date(monday);
                    dayDate.setDate(monday.getDate() + i);
                    const iso = `${dayDate.getFullYear()}-${`${dayDate.getMonth() + 1}`.padStart(2, '0')}-${`${dayDate.getDate()}`.padStart(2, '0')}`;
                    if (iso !== selectedDate) dates.push(iso);
                  }
                  setCopyTargets(dates.join(', '));
                }}
              />
              <Button label={copy.doctorCopySchedule} variant="secondary" loading={copySchedule.isPending} onPress={runCopy} />
            </View>
          </View>

          {formError ? <AppText variant="muted">{formError}</AppText> : null}
          <Button label={copy.save} loading={save.isPending} onPress={() => save.mutate()} />
          <Button
            label={copy.doctorResetToDefault}
            variant="secondary"
            loading={reset.isPending}
            onPress={() => reset.mutate()}
          />
          <Button label={copy.cancel} variant="secondary" onPress={() => router.back()} />
        </ScrollView>
      </Screen>
    </DoctorShell>
  );
}
