import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useMemo, useState } from 'react';
import { ScrollView, View } from 'react-native';

import { useSelectedDoctorClinicId } from '@/components/doctor/clinic-switcher';
import { DoctorShell } from '@/components/layout/doctor-shell';
import { Screen } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Segmented } from '@/components/ui/segmented';
import { AppText } from '@/components/ui/text';
import { DailyQueueBoard } from '@/features/appointments-queue/daily-queue-board';
import { WeekAppointmentCalendar } from '@/features/appointments-queue/week-appointment-calendar';
import { addCalendarDays, clinicDateIso } from '@/features/providers/schedule-day';
import { useCopy } from '@/i18n/copy';
import { listDoctorClinics, listPracticeAppointmentsScoped } from '@/services/api/doctor-portal-api';
import { getMyDoctor } from '@/services/api/provider-api';
import { callNext, doctorQueueSummary, listDeskClinics, listDoctorQueue, queueAction } from '@/services/api/queue-api';
import { useDoctorClinicStore } from '@/stores/doctor-clinic-store';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

type HubTab = 'queue' | 'calendar';

export function DoctorQueueScreen() {
  const copy = useCopy();
  const router = useRouter();
  const queryClient = useQueryClient();
  const [tab, setTab] = useState<HubTab>('queue');
  const doctor = useQuery({ queryKey: ['doctor', 'me'], queryFn: getMyDoctor });
  const clinics = useQuery({ queryKey: ['doctor', 'clinics'], queryFn: listDoctorClinics });
  const desk = useQuery({ queryKey: ['clinics', 'desk'], queryFn: listDeskClinics });
  const selectedClinicId = useSelectedDoctorClinicId(clinics.data);
  const setSelectedClinicId = useDoctorClinicStore((state) => state.setSelectedClinicId);
  const [branchId, setBranchId] = useState('');
  const selectedClinic =
    (clinics.data ?? []).find((item) => item.clinicId === selectedClinicId) ??
    (clinics.data ?? []).find((item) => item.isPrimary) ??
    clinics.data?.[0];
  const activeClinicId = selectedClinic?.clinicId ?? '';
  const deskBranches = (desk.data ?? []).find((item) => item.id === activeClinicId)?.branches ?? [];
  const activeBranchId = branchId || selectedClinic?.branchId || deskBranches[0]?.id || '';
  const doctorId = doctor.data?.id ?? '';
  const enabled = Boolean(activeClinicId && activeBranchId && doctorId);

  const summary = useQuery({
    queryKey: ['queue', 'summary', activeClinicId, activeBranchId, doctorId],
    queryFn: () => doctorQueueSummary(activeClinicId, activeBranchId, doctorId),
    enabled: enabled && tab === 'queue',
    refetchInterval: 15_000,
    refetchIntervalInBackground: false,
  });
  const queue = useQuery({
    queryKey: ['queue', 'doctor', activeClinicId, activeBranchId, doctorId],
    queryFn: () => listDoctorQueue(activeClinicId, activeBranchId, doctorId),
    enabled: enabled && tab === 'queue',
    refetchInterval: 15_000,
    refetchIntervalInBackground: false,
  });

  const calendarRange = useMemo(() => {
    const from = clinicDateIso();
    return { from, to: addCalendarDays(from, 6) };
  }, []);
  const visits = useQuery({
    queryKey: ['appointments', 'aq-week', calendarRange.from, calendarRange.to, activeClinicId || 'all'],
    queryFn: () => listPracticeAppointmentsScoped(calendarRange.from, calendarRange.to, activeClinicId || null, true),
    enabled: tab === 'calendar',
    refetchInterval: 60_000,
    refetchIntervalInBackground: false,
  });

  const refresh = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['queue', 'summary', activeClinicId, activeBranchId, doctorId] }),
      queryClient.invalidateQueries({ queryKey: ['queue', 'doctor', activeClinicId, activeBranchId, doctorId] }),
      queryClient.invalidateQueries({
        queryKey: ['appointments', 'aq-week', calendarRange.from, calendarRange.to, activeClinicId || 'all'],
      }),
    ]);
  };
  const next = useMutation({
    mutationFn: () => callNext(activeClinicId, activeBranchId, doctorId),
    onSuccess: refresh,
  });
  const act = useMutation({
    mutationFn: (input: { id: string; action: string }) => queueAction(input.id, input.action),
    onSuccess: refresh,
  });
  const error = summary.error ?? queue.error ?? next.error ?? act.error ?? doctor.error ?? visits.error;

  return (
    <DoctorShell active="appointmentsQueue">
      <Screen>
        <ScrollView contentContainerStyle={{ gap: spacing.md, paddingBottom: spacing.xxl }}>
          <ScreenHeader title={copy.appointmentsAndQueue} />
          <Segmented
            value={tab}
            options={[
              { value: 'queue', label: copy.aqDailyQueue },
              { value: 'calendar', label: copy.aqAppointmentCalendar },
            ]}
            onChange={setTab}
          />

          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
            {(clinics.data ?? []).map((clinic) => (
              <Button
                key={clinic.clinicId}
                label={clinic.name}
                variant={activeClinicId === clinic.clinicId ? 'primary' : 'secondary'}
                onPress={() => {
                  setSelectedClinicId(clinic.clinicId);
                  setBranchId(clinic.branchId ?? '');
                }}
              />
            ))}
          </View>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
            {deskBranches.map((branch) => (
              <Button
                key={branch.id}
                label={branch.name}
                variant={activeBranchId === branch.id ? 'primary' : 'secondary'}
                onPress={() => setBranchId(branch.id)}
              />
            ))}
          </View>

          {tab === 'queue' ? (
            enabled ? (
              <DailyQueueBoard
                summary={summary.data}
                entries={queue.data ?? []}
                busy={next.isPending || act.isPending}
                canManage
                onCallNext={() => next.mutate()}
                onAction={(id, action) => act.mutate({ id, action })}
                onOpenConsultation={(appointmentId) => router.push(`/consultation/${appointmentId}` as Href)}
              />
            ) : (
              <AppText variant="muted">{copy.aqEmptyQueueBody}</AppText>
            )
          ) : (
            <WeekAppointmentCalendar
              visits={visits.data ?? []}
              loading={visits.isLoading}
              error={visits.error ? problemMessage(visits.error, copy.aqAppointmentCalendar) : null}
              onOpenDetails={(id) => router.push(`/consultation/${id}` as Href)}
            />
          )}

          {error && tab === 'queue' ? <AppText variant="muted">{problemMessage(error, copy.appointmentsAndQueue)}</AppText> : null}
        </ScrollView>
      </Screen>
    </DoctorShell>
  );
}
