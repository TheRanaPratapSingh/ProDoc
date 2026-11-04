import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useState } from 'react';
import { View } from 'react-native';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { DashboardStatCard } from '@/components/ui/stat-card';
import { StatusBadge } from '@/components/ui/status-badge';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { getMyDoctor } from '@/services/api/provider-api';
import { callNext, doctorQueueSummary, listDeskClinics, listDoctorQueue, queueAction } from '@/services/api/queue-api';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

export function DoctorQueueScreen() {
  const copy = useCopy();
  const router = useRouter();
  const queryClient = useQueryClient();
  const doctor = useQuery({ queryKey: ['doctor', 'me'], queryFn: getMyDoctor });
  const clinics = useQuery({ queryKey: ['clinics', 'desk'], queryFn: listDeskClinics });
  const [clinicId, setClinicId] = useState('');
  const [branchId, setBranchId] = useState('');
  const selectedClinic = (clinics.data ?? []).find((item) => item.id === clinicId) ?? clinics.data?.[0];
  const selectedBranch = selectedClinic?.branches.find((item) => item.id === branchId) ?? selectedClinic?.branches[0];
  const activeClinicId = selectedClinic?.id ?? '';
  const activeBranchId = selectedBranch?.id ?? '';
  const doctorId = doctor.data?.id ?? '';
  const enabled = Boolean(activeClinicId && activeBranchId && doctorId);
  const summary = useQuery({
    queryKey: ['queue', 'summary', activeClinicId, activeBranchId, doctorId],
    queryFn: () => doctorQueueSummary(activeClinicId, activeBranchId, doctorId),
    enabled,
    refetchInterval: 10000,
  });
  const queue = useQuery({
    queryKey: ['queue', 'doctor', activeClinicId, activeBranchId, doctorId],
    queryFn: () => listDoctorQueue(activeClinicId, activeBranchId, doctorId),
    enabled,
    refetchInterval: 10000,
  });
  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['queue'] });
  };
  const next = useMutation({
    mutationFn: () => callNext(activeClinicId, activeBranchId, doctorId),
    onSuccess: refresh,
  });
  const act = useMutation({
    mutationFn: (input: { id: string; action: string }) => queueAction(input.id, input.action),
    onSuccess: refresh,
  });
  const error = summary.error ?? queue.error ?? next.error ?? act.error ?? doctor.error;

  return (
    <AuthScaffold>
      <Button label={copy.back} variant="ghost" onPress={() => router.back()} />
      <AppText variant="title">{copy.doctorQueue}</AppText>
      {(clinics.data ?? []).map((clinic) => (
        <Button
          key={clinic.id}
          label={clinic.name}
          variant={activeClinicId === clinic.id ? 'primary' : 'secondary'}
          onPress={() => {
            setClinicId(clinic.id);
            setBranchId(clinic.branches[0]?.id ?? '');
          }}
        />
      ))}
      {(selectedClinic?.branches ?? []).map((branch) => (
        <Button
          key={branch.id}
          label={branch.name}
          variant={activeBranchId === branch.id ? 'primary' : 'secondary'}
          onPress={() => setBranchId(branch.id)}
        />
      ))}
      {summary.data ? (
        <Card elevated style={{ gap: spacing.md }}>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md }}>
            <DashboardStatCard label={copy.myVisits} value={summary.data.totalToday} />
            <DashboardStatCard label={copy.waiting} value={summary.data.waiting} />
            <DashboardStatCard label={copy.completedVisit} value={summary.data.completed} />
            <DashboardStatCard label={copy.walkIn} value={summary.data.walkIns} />
          </View>
          {summary.data.current ? (
            <AppText>
              {copy.nowConsulting}: {summary.data.current.tokenLabel} · {summary.data.current.displayName}
            </AppText>
          ) : null}
          <Button label={copy.callNext} loading={next.isPending} onPress={() => next.mutate()} />
        </Card>
      ) : null}
      {(queue.data ?? []).map((entry) => (
        <Card key={entry.id} elevated>
          <AppText variant="headline">{entry.tokenLabel}</AppText>
          <AppText>{entry.displayName}</AppText>
          <StatusBadge label={entry.status} tone="primary" />
          <AppText variant="muted">{new Date(entry.checkInAtUtc).toLocaleTimeString()}</AppText>
          <Button label={copy.recall} variant="secondary" onPress={() => act.mutate({ id: entry.id, action: 'recall' })} />
          <Button label={copy.startConsult} onPress={() => act.mutate({ id: entry.id, action: 'start-consultation' })} />
          <Button label={copy.completeConsult} onPress={() => act.mutate({ id: entry.id, action: 'complete-consultation' })} />
          {entry.appointmentId ? (
            <Button
              label={copy.openConsultation}
              variant="secondary"
              onPress={() => router.push(`/consultation/${entry.appointmentId}` as Href)}
            />
          ) : null}
          <Button label={copy.skipPatient} variant="secondary" onPress={() => act.mutate({ id: entry.id, action: 'skip' })} />
          <Button label={copy.holdPatient} variant="secondary" onPress={() => act.mutate({ id: entry.id, action: 'hold' })} />
          <Button label={copy.resumePatient} variant="secondary" onPress={() => act.mutate({ id: entry.id, action: 'resume' })} />
        </Card>
      ))}
      {queue.data?.length === 0 ? <AppText variant="muted">{copy.noQueue}</AppText> : null}
      {error ? <AppText variant="muted">{problemMessage(error, copy.doctorQueue)}</AppText> : null}
    </AuthScaffold>
  );
}
