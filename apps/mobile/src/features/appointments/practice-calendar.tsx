import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { useState } from 'react';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import { bookWalkIn, decideAppointment, listMySchedules, listPracticeAppointments } from '@/services/api/provider-api';
import { useSessionStore } from '@/stores/session-store';
import { problemMessage } from '@/utils/problem';

function localDate(offset = 0) {
  const date = new Date();
  date.setDate(date.getDate() + offset);
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

export function PracticeCalendar() {
  const copy = useCopy();
  const router = useRouter();
  const queryClient = useQueryClient();
  const canWalkIn = useSessionStore((state) => state.account?.permissions.includes('appointments.book.walkin') ?? false);
  const from = localDate(0);
  const to = localDate(14);
  const visits = useQuery({ queryKey: ['appointments', 'practice', from], queryFn: () => listPracticeAppointments(from, to) });
  const schedules = useQuery({ queryKey: ['schedules', 'mine'], queryFn: listMySchedules, enabled: canWalkIn });
  const [scheduleId, setScheduleId] = useState<string | null>(null);
  const [date, setDate] = useState(localDate(1));
  const [time, setTime] = useState('09:00');
  const [patientName, setPatientName] = useState('');
  const [reason, setReason] = useState('');
  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['appointments', 'practice'] });
  };
  const decision = useMutation({
    mutationFn: (input: { id: string; decision: 'Approve' | 'Reject' }) => decideAppointment(input.id, input.decision, reason),
    onSuccess: refresh,
  });
  const walkIn = useMutation({
    mutationFn: () =>
      bookWalkIn({
        scheduleId: scheduleId!,
        date,
        start: time.length === 5 ? `${time}:00` : time,
        patientName: patientName.trim(),
      }),
    onSuccess: async () => {
      setPatientName('');
      await refresh();
    },
  });
  const clinicSchedules = (schedules.data ?? []).filter((item) => item.clinicId && item.branchId);
  const error = visits.error ?? decision.error ?? walkIn.error;

  return (
    <AuthScaffold>
      <Button label={copy.back} variant="ghost" onPress={() => router.back()} />
      <AppText variant="title">{copy.practiceVisits}</AppText>
      {(visits.data ?? []).map((visit) => (
        <Card key={visit.id}>
          <AppText>{visit.reference ?? visit.status}</AppText>
          <AppText>{visit.patientName || copy.patientName}</AppText>
          <AppText variant="muted">{visit.status === 'Requested' ? copy.requested : visit.status === 'Confirmed' ? copy.confirmed : visit.status}</AppText>
          <AppText>{[visit.doctorName, visit.clinicName, visit.branchName].filter(Boolean).join(' · ')}</AppText>
          <AppText>{new Date(visit.startsAtUtc).toLocaleString()}</AppText>
          {visit.reason ? <AppText variant="muted">{visit.reason}</AppText> : null}
          {visit.status === 'Requested' ? (
            <>
              <TextField label={copy.reason} value={reason} onChangeText={setReason} />
              <Button label={copy.approve} loading={decision.isPending} onPress={() => decision.mutate({ id: visit.id, decision: 'Approve' })} />
              <Button
                label={copy.reject}
                variant="danger"
                loading={decision.isPending}
                onPress={() => decision.mutate({ id: visit.id, decision: 'Reject' })}
              />
            </>
          ) : null}
        </Card>
      ))}
      {visits.data?.length === 0 ? <AppText variant="muted">{copy.noVisits}</AppText> : null}
      {canWalkIn ? (
        <Card>
          <AppText variant="label">{copy.walkIn}</AppText>
          {clinicSchedules.map((item) => (
            <Button
              key={item.id}
              label={`${item.currency} ${item.consultationFee}`}
              variant={scheduleId === item.id ? 'primary' : 'secondary'}
              onPress={() => setScheduleId(item.id)}
            />
          ))}
          <TextField label={copy.newDate} value={date} onChangeText={setDate} />
          <TextField label={copy.newTime} value={time} onChangeText={setTime} />
          <TextField label={copy.patientName} value={patientName} onChangeText={setPatientName} />
          <Button label={copy.walkIn} loading={walkIn.isPending} disabled={!scheduleId} onPress={() => walkIn.mutate()} />
        </Card>
      ) : null}
      {error ? <AppText variant="muted">{problemMessage(error, copy.practiceVisits)}</AppText> : null}
    </AuthScaffold>
  );
}
