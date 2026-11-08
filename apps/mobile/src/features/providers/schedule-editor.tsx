import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';
import { Alert, Pressable, ScrollView, Switch, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { ClinicSwitcher, useSelectedDoctorClinicId } from '@/components/doctor/clinic-switcher';
import { ReceptionClinicSwitcher, useReceptionDeskSelection } from '@/components/reception/clinic-switcher';
import { DoctorShell } from '@/components/layout/doctor-shell';
import { ReceptionShell } from '@/components/layout/reception-shell';
import { AuthScaffold, Screen } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { listDoctorClinics } from '@/services/api/doctor-portal-api';
import { listDeskDoctors } from '@/services/api/queue-api';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Segmented } from '@/components/ui/segmented';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import {
  isValidCalendarDate,
  isValidClockRange,
  normalizeScheduleDay,
  sliceClock,
  todayIsoLocal,
  weekdayName,
} from '@/features/providers/schedule-day';
import { useFormFieldErrors } from '@/hooks/use-form-field-errors';
import { useCopy } from '@/i18n/copy';
import { scheduleCreateSchema } from '@/schemas/common';
import {
  addScheduleClosure,
  createSchedule,
  getClosureImpact,
  getMyClinics,
  listMySchedules,
  removeScheduleClosure,
  saveScheduleHours,
  saveScheduleTerms,
  setSchedulePublished,
  type ScheduleDetails,
} from '@/services/api/provider-api';
import { notifyWarning } from '@/stores/feedback-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { classifyApiError, problemMessage } from '@/utils/problem';

type AvailabilityTab = 'availability' | 'working' | 'leave';

function PressableBookingModeCard({
  title,
  body,
  active,
  activeLabel,
  disabled,
  onPress,
}: {
  title: string;
  body: string;
  active: boolean;
  activeLabel: string;
  disabled?: boolean;
  onPress: () => void;
}) {
  const theme = useProdocTheme();
  return (
    <Pressable
      accessibilityRole="radio"
      accessibilityState={{ selected: active, disabled }}
      disabled={disabled}
      onPress={onPress}
      style={{
        borderWidth: 1.5,
        borderColor: active ? theme.colors.primary : theme.colors.border,
        backgroundColor: active ? theme.colors.primaryLight : theme.colors.surface,
        borderRadius: 12,
        padding: spacing.md,
        gap: spacing.xs,
        opacity: disabled ? 0.6 : 1,
      }}>
      <AppText variant="label">{title}</AppText>
      <AppText variant="muted">{body}</AppText>
      {active ? (
        <AppText variant="muted" style={{ color: theme.colors.primary }}>
          {activeLabel}
        </AppText>
      ) : null}
    </Pressable>
  );
}

const DAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'] as const;
const DEFAULT_START = '11:00';
const DEFAULT_END = '17:00';

type DayDraft = {
  day: number;
  enabled: boolean;
  startsAt: string;
  endsAt: string;
};

type ScheduleSurface = 'doctor' | 'reception';

function buildDayDrafts(schedule: ScheduleDetails | undefined): DayDraft[] {
  return DAYS.map((_, day) => {
    const windows = (schedule?.hours ?? [])
      .filter((item) => normalizeScheduleDay(item.day) === day)
      .map((item) => ({ startsAt: sliceClock(item.startsAt), endsAt: sliceClock(item.endsAt) }))
      .sort((a, b) => a.startsAt.localeCompare(b.startsAt));
    const weekday = day >= 1 && day <= 5;
    return {
      day,
      enabled: windows.length > 0 ? true : false,
      startsAt: windows[0]?.startsAt ?? (weekday ? DEFAULT_START : DEFAULT_START),
      endsAt: windows[0]?.endsAt ?? (weekday ? DEFAULT_END : DEFAULT_END),
    };
  });
}

function confirmScheduleConflict(copy: ReturnType<typeof useCopy>, onReload: () => void) {
  Alert.alert(copy.scheduleConflictTitle, copy.scheduleConflictBody, [
    { text: copy.cancel, style: 'cancel' },
    { text: copy.scheduleReload, onPress: onReload },
  ]);
}

export function ScheduleEditor({
  surface = 'doctor',
  initialTab = 'availability',
  embedded = false,
  lockTab = false,
}: {
  surface?: ScheduleSurface;
  initialTab?: AvailabilityTab;
  embedded?: boolean;
  lockTab?: boolean;
}) {
  const copy = useCopy();
  const theme = useProdocTheme();
  const insets = useSafeAreaInsets();
  const queryClient = useQueryClient();
  const [tab, setTab] = useState<AvailabilityTab>(initialTab);
  const schedules = useQuery({ queryKey: ['schedules', 'mine'], queryFn: listMySchedules });
  const clinics = useQuery({
    queryKey: ['clinics', 'mine'],
    queryFn: getMyClinics,
    enabled: surface === 'doctor',
  });
  const doctorClinics = useQuery({
    queryKey: ['doctor', 'clinics'],
    queryFn: listDoctorClinics,
    enabled: surface === 'doctor',
  });
  const doctorClinicId = useSelectedDoctorClinicId(doctorClinics.data);
  const receptionDesk = useReceptionDeskSelection();
  const clinicId = surface === 'reception' ? receptionDesk.clinicId : doctorClinicId;
  const branchId = surface === 'reception' ? receptionDesk.branchId : null;
  const deskDoctors = useQuery({
    queryKey: ['desk', 'doctors', clinicId, branchId],
    queryFn: () => listDeskDoctors(clinicId!, branchId!),
    enabled: surface === 'reception' && Boolean(clinicId && branchId),
  });

  const [fee, setFee] = useState('500');
  const [duration, setDuration] = useState('10');
  const [buffer, setBuffer] = useState('0');
  const scheduleFieldErrors = useFormFieldErrors<'fee' | 'duration' | 'buffer' | 'closedDate'>();
  const [place, setPlace] = useState('independent');
  const [closedDate, setClosedDate] = useState('');
  const [reason, setReason] = useState('Personal Leave');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [selectedDoctorId, setSelectedDoctorId] = useState<string | null>(null);
  const [dayDrafts, setDayDrafts] = useState<DayDraft[]>(() => buildDayDrafts(undefined));
  const [copyFromDay, setCopyFromDay] = useState(1);

  const scopedSchedules = useMemo(() => {
    const rows = schedules.data ?? [];
    if (surface === 'reception') {
      return rows.filter((item) => {
        if (clinicId && item.clinicId !== clinicId) return false;
        if (branchId && item.branchId && item.branchId !== branchId) return false;
        if (selectedDoctorId && item.doctorId !== selectedDoctorId) return false;
        return Boolean(item.clinicId);
      });
    }
    return rows.filter((item) => !clinicId || item.clinicId === clinicId);
  }, [branchId, clinicId, schedules.data, selectedDoctorId, surface]);

  const selected = scopedSchedules.find((item) => item.id === selectedId) ?? scopedSchedules[0];
  const activeDoctorId = selectedDoctorId ?? deskDoctors.data?.[0]?.id ?? null;
  const selectedClinic = doctorClinics.data?.find((item) => item.clinicId === clinicId) ?? doctorClinics.data?.[0];

  useEffect(() => {
    setDayDrafts(buildDayDrafts(selected));
    if (selected) {
      setFee(String(selected.consultationFee));
      setDuration(String(selected.slotDurationMinutes));
      setBuffer(String(selected.bufferMinutes));
    }
  }, [selected?.id, selected?.rowVersion]);

  const refresh = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['schedules'] }),
      queryClient.invalidateQueries({ queryKey: ['doctor'] }),
      queryClient.invalidateQueries({ queryKey: ['appointments'] }),
    ]);
  };

  const onMutationError = (error: unknown) => {
    if (classifyApiError(error) === 'conflict') {
      confirmScheduleConflict(copy, () => {
        void refresh();
      });
    }
  };

  const create = useMutation({
    mutationFn: () => {
      const validated = scheduleFieldErrors.validateObject(scheduleCreateSchema, { fee, duration, buffer });
      if (!validated.ok) {
        notifyWarning(validated.message);
        throw new Error(validated.message);
      }
      const consultationFee = Number(validated.data.fee);
      const slotDurationMinutes = Number(validated.data.duration);
      const bufferMinutes = Number(validated.data.buffer || '0');
      if (surface === 'reception') {
        if (!clinicId || !branchId || !activeDoctorId) {
          throw new Error(copy.receptionNoClinics);
        }
        return createSchedule({
          doctorId: activeDoctorId,
          clinicId,
          branchId,
          consultationFee,
          slotDurationMinutes,
          bufferMinutes,
        });
      }
      if (place === 'independent' && clinicId && selectedClinic) {
        return createSchedule({
          clinicId: selectedClinic.clinicId,
          branchId: selectedClinic.branchId ?? undefined,
          consultationFee,
          slotDurationMinutes,
          bufferMinutes,
        });
      }
      const [nextClinicId, nextBranchId] = place === 'independent' ? [undefined, undefined] : place.split(':');
      return createSchedule({
        clinicId: nextClinicId,
        branchId: nextBranchId,
        consultationFee,
        slotDurationMinutes,
        bufferMinutes,
      });
    },
    onSuccess: async (created) => {
      scheduleFieldErrors.clearAll();
      setSelectedId(created.id);
      await refresh();
    },
    onError: onMutationError,
  });

  const withVersion = (schedule: ScheduleDetails | undefined) => schedule?.rowVersion || undefined;

  const hours = useMutation({
    mutationFn: async () => {
      for (const day of dayDrafts.filter((item) => item.enabled)) {
        if (!isValidClockRange(day.startsAt, day.endsAt)) {
          throw new Error(copy.doctorTimingInvalidRange);
        }
      }
      return saveScheduleHours(
        selected!.id,
        dayDrafts
          .filter((item) => item.enabled)
          .map((item) => ({
            day: weekdayName(item.day),
            startsAt: item.startsAt,
            endsAt: item.endsAt,
          })),
        withVersion(selected),
      );
    },
    onSuccess: async () => {
      await refresh();
      Alert.alert(copy.save, copy.doctorTimingSaved);
    },
    onError: (error) => {
      onMutationError(error);
      if (error instanceof Error && !String(error.message).includes('axios')) {
        Alert.alert(copy.save, error.message);
      }
    },
  });

  const closure = useMutation({
    mutationFn: async () => {
      const date = closedDate || todayIsoLocal();
      if (!isValidCalendarDate(date)) {
        throw new Error(copy.closedDate);
      }
      const impact = await getClosureImpact(selected!.id, date);
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
        if (!confirmed) throw new Error('cancelled');
      }
      return addScheduleClosure(selected!.id, date, reason || 'Leave', withVersion(selected));
    },
    onSuccess: async () => {
      setClosedDate('');
      await refresh();
    },
    onError: (error) => {
      if (error instanceof Error && error.message === 'cancelled') return;
      onMutationError(error);
    },
  });
  const reopen = useMutation({
    mutationFn: (closureId: string) => removeScheduleClosure(selected!.id, closureId, withVersion(selected)),
    onSuccess: refresh,
    onError: onMutationError,
  });
  const publication = useMutation({
    mutationFn: (isPublished: boolean) => setSchedulePublished(selected!.id, isPublished, withVersion(selected)),
    onSuccess: refresh,
    onError: onMutationError,
  });
  const approval = useMutation({
    mutationFn: () => saveScheduleTerms(selected!, !selected!.requiresApproval),
    onSuccess: refresh,
    onError: onMutationError,
  });
  const bookingMode = useMutation({
    mutationFn: async (next: 'TimedSlots' | 'HourlyQueue') => {
      if (!selected) {
        throw new Error('No schedule selected');
      }
      if ((selected.bookingMode ?? 'TimedSlots') === next) {
        return selected;
      }
      try {
        return await saveScheduleTerms(selected, selected.requiresApproval, {
          bookingMode: next,
          confirmBookingModeChange: false,
        });
      } catch (error) {
        const code = problemMessage(error, '');
        const needsConfirm =
          code.toLowerCase().includes('confirm') ||
          code.toLowerCase().includes('future booking') ||
          classifyApiError(error) === 'conflict';
        if (!needsConfirm) {
          throw error;
        }
        const confirmed = await new Promise<boolean>((resolve) => {
          Alert.alert(copy.bookingModeChangeConfirmTitle, copy.bookingModeChangeConfirmBody, [
            { text: copy.cancel, style: 'cancel', onPress: () => resolve(false) },
            { text: copy.save, style: 'destructive', onPress: () => resolve(true) },
          ]);
        });
        if (!confirmed) {
          throw new Error('cancelled');
        }
        return saveScheduleTerms(selected, selected.requiresApproval, {
          bookingMode: next,
          confirmBookingModeChange: true,
        });
      }
    },
    onSuccess: async () => {
      await refresh();
      notifyWarning(copy.bookingModeSaved);
    },
    onError: (error) => {
      if (error instanceof Error && error.message === 'cancelled') return;
      onMutationError(error);
    },
  });

  const error =
    create.error ?? hours.error ?? closure.error ?? reopen.error ?? publication.error ?? approval.error ?? bookingMode.error;
  const todayClosure = selected?.closures.find((item) => item.date.startsWith(todayIsoLocal()));
  const upcomingLeave = (selected?.closures ?? [])
    .slice()
    .sort((a, b) => a.date.localeCompare(b.date));

  const editorBody = (
    <>
      {embedded ? null : (
        <ScreenHeader title={surface === 'reception' ? copy.receptionAvailabilityTitle : copy.doctorNavAvailability} />
      )}
      {surface === 'doctor' ? <ClinicSwitcher clinics={doctorClinics.data ?? []} /> : <ReceptionClinicSwitcher />}
      {surface === 'reception' ? (
        <Card>
          <AppText variant="label">{copy.receptionAvailableDoctors}</AppText>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
            {(deskDoctors.data ?? []).map((doctor) => (
              <Button
                key={doctor.id}
                label={doctor.displayName}
                variant={(selectedDoctorId ?? activeDoctorId) === doctor.id ? 'primary' : 'secondary'}
                onPress={() => {
                  setSelectedDoctorId(doctor.id);
                  setSelectedId(null);
                }}
              />
            ))}
          </View>
          {(deskDoctors.data ?? []).length === 0 ? <AppText variant="muted">{copy.receptionNoClinics}</AppText> : null}
        </Card>
      ) : null}

      {lockTab ? null : (
        <Segmented
          value={tab}
          onChange={setTab}
          options={[
            { value: 'availability', label: copy.doctorAvailableToday },
            { value: 'working', label: copy.doctorWorkingDays },
            { value: 'leave', label: copy.doctorLeaveTab },
          ]}
        />
      )}

      <Card>
        {scopedSchedules.map((item) => (
          <Button
            key={item.id}
            label={`${item.isPublished ? '● ' : ''}${item.currency} ${item.consultationFee} · ${item.slotDurationMinutes}m`}
            variant={selected?.id === item.id ? 'primary' : 'secondary'}
            onPress={() => setSelectedId(item.id)}
          />
        ))}
        {scopedSchedules.length === 0 ? <AppText variant="muted">{copy.noSchedules}</AppText> : null}
        {tab === 'working' ? (
          <>
            <TextField
              label={copy.consultationFee}
              required
              value={fee}
              error={scheduleFieldErrors.errors.fee}
              onChangeText={(value) => {
                scheduleFieldErrors.clearField('fee');
                setFee(value);
              }}
              keyboardType="decimal-pad"
            />
            <TextField
              label={copy.slotLength}
              required
              value={duration}
              error={scheduleFieldErrors.errors.duration}
              onChangeText={(value) => {
                scheduleFieldErrors.clearField('duration');
                setDuration(value);
              }}
              keyboardType="number-pad"
            />
            <TextField
              label={copy.buffer}
              value={buffer}
              error={scheduleFieldErrors.errors.buffer}
              onChangeText={(value) => {
                scheduleFieldErrors.clearField('buffer');
                setBuffer(value);
              }}
              keyboardType="number-pad"
            />
            {surface === 'doctor' ? (
              <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
                <Button
                  label={copy.independent}
                  variant={place === 'independent' ? 'primary' : 'secondary'}
                  onPress={() => setPlace('independent')}
                />
                {(clinics.data ?? []).flatMap((clinic) =>
                  clinic.branches
                    .filter((branch) => branch.isActive)
                    .map((branch) => {
                      const key = `${clinic.id}:${branch.id}`;
                      return (
                        <Button
                          key={key}
                          label={`${clinic.name ?? copy.clinicName} · ${branch.name}`}
                          variant={place === key ? 'primary' : 'secondary'}
                          onPress={() => setPlace(key)}
                        />
                      );
                    }),
                )}
              </View>
            ) : (
              <AppText variant="muted">{copy.receptionAvailabilityHint}</AppText>
            )}
            <Button label={copy.addSchedule} variant="secondary" loading={create.isPending} onPress={() => create.mutate()} />
          </>
        ) : null}
      </Card>

      {selected && tab === 'availability' ? (
        <Card style={{ gap: spacing.md }}>
          <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
            <AppText variant="label">{copy.doctorAvailableToday}</AppText>
            <Switch
              value={selected.isPublished && !todayClosure}
              onValueChange={(value) => {
                if (!value) {
                  setClosedDate(todayIsoLocal());
                  setReason('Marked unavailable for today');
                  closure.mutate();
                  return;
                }
                if (todayClosure) {
                  reopen.mutate(todayClosure.id);
                  return;
                }
                publication.mutate(true);
              }}
            />
          </View>
          <AppText variant="muted">{copy.doctorWorkingDayHint}</AppText>
          <Button
            label={selected.isPublished ? copy.hideSchedule : copy.publishSchedule}
            variant="secondary"
            loading={publication.isPending}
            onPress={() => publication.mutate(!selected.isPublished)}
          />
        </Card>
      ) : null}

      {selected && tab === 'working' ? (
        <Card style={{ gap: spacing.md }}>
          <AppText variant="muted">{copy.doctorWorkingDayHint}</AppText>
          {dayDrafts.map((draft) => (
            <View
              key={draft.day}
              style={{
                gap: spacing.sm,
                borderTopWidth: 1,
                borderTopColor: theme.colors.border,
                paddingTop: spacing.sm,
              }}>
              <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
                <AppText variant="label">{DAYS[draft.day]}</AppText>
                <Switch
                  value={draft.enabled}
                  onValueChange={(enabled) =>
                    setDayDrafts((rows) => rows.map((row) => (row.day === draft.day ? { ...row, enabled } : row)))
                  }
                />
              </View>
              <View style={{ flexDirection: 'row', gap: spacing.sm }}>
                <View style={{ flex: 1 }}>
                  <TextField
                    label={copy.hoursStart}
                    value={draft.startsAt}
                    onChangeText={(startsAt) =>
                      setDayDrafts((rows) => rows.map((row) => (row.day === draft.day ? { ...row, startsAt } : row)))
                    }
                    editable={draft.enabled}
                  />
                </View>
                <View style={{ flex: 1 }}>
                  <TextField
                    label={copy.hoursEnd}
                    value={draft.endsAt}
                    onChangeText={(endsAt) =>
                      setDayDrafts((rows) => rows.map((row) => (row.day === draft.day ? { ...row, endsAt } : row)))
                    }
                    editable={draft.enabled}
                  />
                </View>
              </View>
            </View>
          ))}

          <AppText variant="label">{copy.doctorCopyDayToDay}</AppText>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
            {DAYS.map((label, day) => (
              <Button
                key={label}
                label={label.slice(0, 3)}
                variant={copyFromDay === day ? 'primary' : 'secondary'}
                onPress={() => setCopyFromDay(day)}
              />
            ))}
          </View>
          <Button
            label={copy.doctorCopySchedule}
            variant="secondary"
            onPress={() => {
              const source = dayDrafts.find((item) => item.day === copyFromDay);
              if (!source) return;
              setDayDrafts((rows) =>
                rows.map((row) =>
                  row.day === copyFromDay
                    ? row
                    : { ...row, enabled: source.enabled, startsAt: source.startsAt, endsAt: source.endsAt },
                ),
              );
            }}
          />

          <Button label={copy.doctorSaveWorkingDays} loading={hours.isPending} onPress={() => hours.mutate()} />
          <Button
            label={selected.requiresApproval ? copy.approvalRequired : copy.instantBooking}
            variant="secondary"
            loading={approval.isPending}
            onPress={() => approval.mutate()}
          />

          <AppText variant="label">{copy.appointmentBookingMode}</AppText>
          <PressableBookingModeCard
            title={copy.bookingModeTimedTitle}
            body={copy.bookingModeTimedBody}
            active={(selected.bookingMode ?? 'TimedSlots') === 'TimedSlots'}
            activeLabel={copy.bookingModeActive}
            disabled={bookingMode.isPending}
            onPress={() => bookingMode.mutate('TimedSlots')}
          />
          <PressableBookingModeCard
            title={copy.bookingModeHourlyTitle}
            body={copy.bookingModeHourlyBody}
            active={(selected.bookingMode ?? 'TimedSlots') === 'HourlyQueue'}
            activeLabel={copy.bookingModeActive}
            disabled={bookingMode.isPending}
            onPress={() => bookingMode.mutate('HourlyQueue')}
          />
        </Card>
      ) : null}

      {selected && tab === 'leave' ? (
        <Card style={{ gap: spacing.md }}>
          <TextField
            label={copy.closedDate}
            value={closedDate}
            onChangeText={setClosedDate}
            placeholder="2026-11-09, 2026-11-10"
          />
          <AppText variant="muted">Enter one date, or comma-separated dates.</AppText>
          <TextField label={copy.reason} value={reason} onChangeText={setReason} />
          <Button
            label={copy.addClosure}
            loading={closure.isPending}
            onPress={() => {
              const dates = (closedDate || todayIsoLocal())
                .split(/[\s,]+/)
                .map((item) => item.trim())
                .filter(isValidCalendarDate);
              if (dates.length === 0) {
                setClosedDate(todayIsoLocal());
                closure.mutate();
                return;
              }
              void (async () => {
                let current = selected;
                for (const date of dates) {
                  setClosedDate(date);
                  const impact = await getClosureImpact(current.id, date);
                  if (impact.confirmedAppointments > 0) {
                    const ok = await new Promise<boolean>((resolve) => {
                      Alert.alert(
                        copy.doctorLeaveImpactTitle,
                        `${date}: ` +
                          copy.doctorLeaveImpactBody.replace('{count}', String(impact.confirmedAppointments)),
                        [
                          { text: copy.cancel, style: 'cancel', onPress: () => resolve(false) },
                          { text: copy.save, style: 'destructive', onPress: () => resolve(true) },
                        ],
                      );
                    });
                    if (!ok) break;
                  }
                  current = await addScheduleClosure(current.id, date, reason || 'Leave', current.rowVersion);
                }
                await refresh();
                setClosedDate('');
              })();
            }}
          />
          {upcomingLeave.map((item) => (
            <View
              key={item.id}
              style={{
                gap: spacing.sm,
                borderTopWidth: 1,
                borderTopColor: theme.colors.border,
                paddingTop: spacing.sm,
              }}>
              <AppText variant="label">
                {item.date}
                {item.reason ? ` · ${item.reason}` : ''}
              </AppText>
              <Button label={copy.cancel} variant="secondary" loading={reopen.isPending} onPress={() => reopen.mutate(item.id)} />
            </View>
          ))}
        </Card>
      ) : null}

      {error && classifyApiError(error) !== 'conflict' && !(error instanceof Error && error.message === 'cancelled') ? (
        <AppText variant="muted">{problemMessage(error, copy.save)}</AppText>
      ) : null}
    </>
  );

  const body =
    surface === 'doctor' ? (
      <Screen>
        <ScrollView
          contentContainerStyle={{
            gap: spacing.md,
            paddingBottom: spacing.xxl,
            paddingTop: embedded ? spacing.sm : Math.max(insets.top - 8, 0),
          }}>
          {editorBody}
        </ScrollView>
      </Screen>
    ) : (
      <AuthScaffold>{editorBody}</AuthScaffold>
    );

  if (embedded) {
    return body;
  }

  if (surface === 'reception') {
    return <ReceptionShell active="availability">{body}</ReceptionShell>;
  }

  return <DoctorShell active="availability">{body}</DoctorShell>;
}
