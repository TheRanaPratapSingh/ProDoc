import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { View } from 'react-native';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import {
  addScheduleClosure,
  createSchedule,
  getMyClinics,
  listMySchedules,
  saveScheduleBreaks,
  saveScheduleHours,
  saveScheduleTerms,
  setSchedulePublished,
} from '@/services/api/provider-api';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

const days = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'] as const;

export function ScheduleEditor() {
  const copy = useCopy();
  const router = useRouter();
  const queryClient = useQueryClient();
  const schedules = useQuery({ queryKey: ['schedules', 'mine'], queryFn: listMySchedules });
  const clinics = useQuery({ queryKey: ['clinics', 'mine'], queryFn: getMyClinics });
  const [fee, setFee] = useState('500');
  const [duration, setDuration] = useState('15');
  const [buffer, setBuffer] = useState('0');
  const [place, setPlace] = useState('independent');
  const [startsAt, setStartsAt] = useState('09:00');
  const [endsAt, setEndsAt] = useState('17:00');
  const [breakStart, setBreakStart] = useState('13:00');
  const [breakEnd, setBreakEnd] = useState('14:00');
  const [closedDate, setClosedDate] = useState('');
  const [reason, setReason] = useState('');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const selected = schedules.data?.find((item) => item.id === selectedId) ?? schedules.data?.[0];

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['schedules', 'mine'] });
  };
  const create = useMutation({
    mutationFn: () => {
      const [clinicId, branchId] = place === 'independent' ? [undefined, undefined] : place.split(':');
      return createSchedule({
        clinicId,
        branchId,
        consultationFee: Number(fee),
        slotDurationMinutes: Number(duration),
        bufferMinutes: Number(buffer),
      });
    },
    onSuccess: async (created) => {
      setSelectedId(created.id);
      await refresh();
    },
  });
  const hours = useMutation({
    mutationFn: () =>
      saveScheduleHours(
        selected!.id,
        days.map((day) => ({ day, startsAt, endsAt })),
      ),
    onSuccess: refresh,
  });
  const breaks = useMutation({
    mutationFn: () =>
      saveScheduleBreaks(
        selected!.id,
        days.map((day) => ({ day, startsAt: breakStart, endsAt: breakEnd })),
      ),
    onSuccess: refresh,
  });
  const closure = useMutation({
    mutationFn: () => addScheduleClosure(selected!.id, closedDate, reason),
    onSuccess: refresh,
  });
  const publication = useMutation({
    mutationFn: (isPublished: boolean) => setSchedulePublished(selected!.id, isPublished),
    onSuccess: refresh,
  });
  const approval = useMutation({
    mutationFn: () => saveScheduleTerms(selected!, !selected!.requiresApproval),
    onSuccess: refresh,
  });
  const error = create.error ?? hours.error ?? breaks.error ?? closure.error ?? publication.error ?? approval.error;

  return (
    <AuthScaffold>
      <Button label={copy.back} variant="secondary" onPress={() => router.back()} />
      <AppText variant="title">{copy.scheduleTitle}</AppText>
      <Card>
        {(schedules.data ?? []).map((item) => (
          <Button
            key={item.id}
            label={`${item.isPublished ? '● ' : ''}${item.currency} ${item.consultationFee}`}
            variant={selected?.id === item.id ? 'primary' : 'secondary'}
            onPress={() => setSelectedId(item.id)}
          />
        ))}
        {schedules.data?.length === 0 ? <AppText variant="muted">{copy.noSchedules}</AppText> : null}
        <TextField label={copy.consultationFee} value={fee} onChangeText={setFee} keyboardType="decimal-pad" />
        <TextField label={copy.slotLength} value={duration} onChangeText={setDuration} keyboardType="number-pad" />
        <TextField label={copy.buffer} value={buffer} onChangeText={setBuffer} keyboardType="number-pad" />
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
        <Button label={copy.addSchedule} variant="secondary" loading={create.isPending} onPress={() => create.mutate()} />
      </Card>
      {selected ? (
        <Card>
          <AppText variant="label">
            {copy.verificationStatus}: {selected.isPublished ? copy.publishSchedule : copy.statusDraft}
          </AppText>
          <TextField label={copy.hoursStart} value={startsAt} onChangeText={setStartsAt} />
          <TextField label={copy.hoursEnd} value={endsAt} onChangeText={setEndsAt} />
          <Button label={copy.saveHours} loading={hours.isPending} onPress={() => hours.mutate()} />
          <TextField label={copy.breakStart} value={breakStart} onChangeText={setBreakStart} />
          <TextField label={copy.breakEnd} value={breakEnd} onChangeText={setBreakEnd} />
          <Button label={copy.saveBreak} variant="secondary" loading={breaks.isPending} onPress={() => breaks.mutate()} />
          <TextField label={copy.closedDate} value={closedDate} onChangeText={setClosedDate} placeholder="2026-11-04" />
          <TextField label={copy.reason} value={reason} onChangeText={setReason} />
          <Button label={copy.addClosure} variant="secondary" loading={closure.isPending} onPress={() => closure.mutate()} />
          {selected.closures.map((item) => (
            <AppText key={item.id} variant="muted">
              {item.date}
              {item.reason ? ` · ${item.reason}` : ''}
            </AppText>
          ))}
          <Button
            label={selected.requiresApproval ? copy.approvalRequired : copy.instantBooking}
            variant="secondary"
            loading={approval.isPending}
            onPress={() => approval.mutate()}
          />
          <Button
            label={selected.isPublished ? copy.hideSchedule : copy.publishSchedule}
            loading={publication.isPending}
            onPress={() => publication.mutate(!selected.isPublished)}
          />
        </Card>
      ) : null}
      {error ? <AppText variant="muted">{problemMessage(error, copy.save)}</AppText> : null}
    </AuthScaffold>
  );
}
