import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useMemo, useState } from 'react';
import { ScrollView, View } from 'react-native';

import { ReceptionClinicSwitcher, useReceptionDeskSelection } from '@/components/reception/clinic-switcher';
import { ReceptionShell } from '@/components/layout/reception-shell';
import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Segmented } from '@/components/ui/segmented';
import { AppText } from '@/components/ui/text';
import { DailyQueueBoard } from '@/features/appointments-queue/daily-queue-board';
import { WeekAppointmentCalendar } from '@/features/appointments-queue/week-appointment-calendar';
import { addCalendarDays, clinicDateIso } from '@/features/providers/schedule-day';
import { useCopy } from '@/i18n/copy';
import { listPracticeAppointments } from '@/services/api/provider-api';
import {
  callNext,
  doctorQueueSummary,
  listDeskDoctors,
  listDoctorQueue,
  queueAction,
} from '@/services/api/queue-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

type HubTab = 'queue' | 'calendar';

export function ReceptionQueueScreen() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { clinicId, branchId } = useReceptionDeskSelection();
  const [tab, setTab] = useState<HubTab>('queue');
  const [doctorId, setDoctorId] = useState('');
  const doctors = useQuery({
    queryKey: ['desk', 'doctors', clinicId, branchId],
    queryFn: () => listDeskDoctors(clinicId, branchId),
    enabled: Boolean(clinicId && branchId),
    staleTime: 60_000,
    refetchInterval: 60_000,
    refetchIntervalInBackground: false,
  });
  const activeDoctorId = doctorId || doctors.data?.[0]?.id || '';
  const ready = Boolean(clinicId && branchId && activeDoctorId);

  const summary = useQuery({
    queryKey: ['queue', 'summary', clinicId, branchId, activeDoctorId],
    queryFn: () => doctorQueueSummary(clinicId, branchId, activeDoctorId),
    enabled: ready && tab === 'queue',
    refetchInterval: 15_000,
    refetchIntervalInBackground: false,
  });
  const queue = useQuery({
    queryKey: ['queue', 'doctor', clinicId, branchId, activeDoctorId],
    queryFn: () => listDoctorQueue(clinicId, branchId, activeDoctorId),
    enabled: ready && tab === 'queue',
    refetchInterval: 15_000,
    refetchIntervalInBackground: false,
  });

  const calendarRange = useMemo(() => {
    const from = clinicDateIso();
    return { from, to: addCalendarDays(from, 6) };
  }, []);
  const visits = useQuery({
    queryKey: ['appointments', 'practice', 'aq-week', clinicId, calendarRange.from, calendarRange.to],
    queryFn: () => listPracticeAppointments(calendarRange.from, calendarRange.to, { clinicId }),
    enabled: Boolean(clinicId) && tab === 'calendar',
    refetchInterval: 60_000,
    refetchIntervalInBackground: false,
  });

  const filteredVisits = useMemo(() => {
    const rows = visits.data ?? [];
    if (!activeDoctorId) return rows;
    return rows.filter((item) => item.doctorId === activeDoctorId);
  }, [activeDoctorId, visits.data]);

  const refresh = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['queue', 'summary', clinicId, branchId, activeDoctorId] }),
      queryClient.invalidateQueries({ queryKey: ['queue', 'doctor', clinicId, branchId, activeDoctorId] }),
      queryClient.invalidateQueries({
        queryKey: ['appointments', 'practice', 'aq-week', clinicId, calendarRange.from, calendarRange.to],
      }),
      queryClient.invalidateQueries({ queryKey: ['reception', 'dashboard', clinicId, branchId] }),
    ]);
  };
  const next = useMutation({
    mutationFn: () => callNext(clinicId, branchId, activeDoctorId),
    onSuccess: refresh,
  });
  const act = useMutation({
    mutationFn: (input: { id: string; action: string }) => queueAction(input.id, input.action),
    onSuccess: refresh,
  });
  const error = summary.error ?? queue.error ?? next.error ?? act.error ?? visits.error;

  return (
    <ReceptionShell active="appointmentsQueue">
      <AuthScaffold>
        <ScreenHeader title={copy.appointmentsAndQueue} />
        <ScrollView contentContainerStyle={{ gap: spacing.lg, paddingBottom: spacing.xxl }}>
          <Segmented
            value={tab}
            options={[
              { value: 'queue', label: copy.aqDailyQueue },
              { value: 'calendar', label: copy.aqAppointmentCalendar },
            ]}
            onChange={setTab}
          />
          <ReceptionClinicSwitcher />
          <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: spacing.sm }}>
            {(doctors.data ?? []).map((doctor) => (
              <Button
                key={doctor.id}
                label={doctor.displayName}
                variant={activeDoctorId === doctor.id ? 'primary' : 'secondary'}
                onPress={() => setDoctorId(doctor.id)}
              />
            ))}
          </ScrollView>

          {tab === 'queue' ? (
            ready ? (
              <DailyQueueBoard
                summary={summary.data}
                entries={queue.data ?? []}
                busy={next.isPending || act.isPending}
                canManage
                onCallNext={() => next.mutate()}
                onAction={(id, action) => act.mutate({ id, action })}
              />
            ) : (
              <AppText variant="muted">{copy.aqEmptyQueueBody}</AppText>
            )
          ) : (
            <WeekAppointmentCalendar
              visits={filteredVisits}
              loading={visits.isLoading}
              error={visits.error ? problemMessage(visits.error, copy.aqAppointmentCalendar) : null}
              onOpenDetails={(id) => router.push(`/reception-appointments?appointmentId=${id}` as Href)}
            />
          )}

          {error && tab === 'queue' ? (
            <AppText style={{ color: theme.colors.error }}>{problemMessage(error, copy.appointmentsAndQueue)}</AppText>
          ) : null}
        </ScrollView>
      </AuthScaffold>
    </ReceptionShell>
  );
}
