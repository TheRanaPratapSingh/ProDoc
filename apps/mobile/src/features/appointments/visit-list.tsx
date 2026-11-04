import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { memo, useState } from 'react';

import { AppShell } from '@/components/layout/app-shell';
import { ScreenList } from '@/components/layout/screen-list';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { EmptyState } from '@/components/ui/empty-state';
import { StatusBadge } from '@/components/ui/status-badge';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import { createAppointmentPayment, verifyPayment } from '@/services/api/billing-api';
import { cancelAppointment, listAppointments, rescheduleAppointment, type AppointmentDetails } from '@/services/api/provider-api';
import { assertMutationAllowedOnline } from '@/services/offline/offline-queue';
import { selectIsEffectivelyOnline, useNetworkStore } from '@/stores/network-store';
import { problemMessage } from '@/utils/problem';

function label(copy: ReturnType<typeof useCopy>, status: string) {
  if (status === 'Confirmed') return copy.confirmed;
  if (status === 'Requested') return copy.requested;
  if (status === 'Rejected') return copy.rejected;
  if (status === 'Cancelled') return copy.cancelled;
  if (status === 'Expired') return copy.expired;
  if (status === 'CheckedIn') return copy.checkedIn;
  if (status === 'Completed') return copy.completedVisit;
  return status;
}

function statusTone(status: string): 'neutral' | 'primary' | 'success' | 'warning' | 'error' {
  if (status === 'Confirmed' || status === 'CheckedIn' || status === 'Completed') return 'success';
  if (status === 'Requested') return 'primary';
  if (status === 'Rejected' || status === 'Cancelled' || status === 'Expired') return 'error';
  return 'neutral';
}

export function VisitList() {
  const copy = useCopy();
  const router = useRouter();
  const queryClient = useQueryClient();
  const online = useNetworkStore(selectIsEffectivelyOnline);
  const visits = useQuery({
    queryKey: ['appointments', 'mine'],
    queryFn: listAppointments,
    placeholderData: (previous) => previous,
  });
  const [active, setActive] = useState<string | null>(null);
  const [date, setDate] = useState('');
  const [time, setTime] = useState('');
  const [reason, setReason] = useState('');
  const [guardError, setGuardError] = useState<string | null>(null);

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['appointments', 'mine'] });
  };

  const withOnlineGuard = async <T,>(action: string, run: () => Promise<T>) => {
    try {
      assertMutationAllowedOnline(action, online);
      setGuardError(null);
      return await run();
    } catch (error) {
      if (error instanceof Error && error.message === 'offline_action_blocked') {
        setGuardError(copy.offlineActionBlocked);
        throw error;
      }
      throw error;
    }
  };

  const cancel = useMutation({
    mutationFn: (id: string) => withOnlineGuard('booking.confirm', () => cancelAppointment(id, reason)),
    onSuccess: refresh,
  });
  const move = useMutation({
    mutationFn: (id: string) =>
      withOnlineGuard('booking.confirm', () => rescheduleAppointment(id, date, time.length === 5 ? `${time}:00` : time)),
    onSuccess: refresh,
  });
  const pay = useMutation({
    mutationFn: async (id: string) =>
      withOnlineGuard('payment.create', async () => {
        const created = await createAppointmentPayment(id, 'PayOnline', `visit-${id}-${Date.now()}`);
        return verifyPayment(created.id);
      }),
    onSuccess: refresh,
  });

  const error = visits.error ?? cancel.error ?? move.error ?? pay.error;
  const cachedNote = !online && (visits.data?.length ?? 0) > 0 ? copy.offlineCachedData : null;

  return (
    <AppShell active="visits">
      <ScreenList
        data={visits.data ?? []}
        keyExtractor={(item) => item.id}
        refreshing={visits.isRefetching}
        onRefresh={() => void refresh()}
        header={
          <>
            <Button label={copy.back} variant="ghost" onPress={() => router.back()} />
            <AppText variant="title">{copy.myVisits}</AppText>
            {cachedNote ? <AppText variant="muted">{cachedNote}</AppText> : null}
            {guardError ? <AppText variant="muted">{guardError}</AppText> : null}
            {error ? <AppText variant="muted">{problemMessage(error, copy.myVisits)}</AppText> : null}
          </>
        }
        empty={<EmptyState title={copy.noVisits} body={copy.loginSubtitle} />}
        renderItem={({ item }) => (
          <VisitCard
            visit={item}
            open={active === item.id}
            onOpen={() => setActive(item.id)}
            date={date}
            time={time}
            reason={reason}
            onDate={setDate}
            onTime={setTime}
            onReason={setReason}
            onCancel={() => cancel.mutate(item.id)}
            onMove={() => move.mutate(item.id)}
            onPay={() => pay.mutate(item.id)}
            paying={pay.isPending}
            pending={cancel.isPending || move.isPending}
            online={online}
          />
        )}
      />
    </AppShell>
  );
}

const VisitCard = memo(function VisitCard({
  visit,
  open,
  onOpen,
  date,
  time,
  reason,
  onDate,
  onTime,
  onReason,
  onCancel,
  onMove,
  onPay,
  paying,
  pending,
  online,
}: {
  visit: AppointmentDetails;
  open: boolean;
  onOpen: () => void;
  date: string;
  time: string;
  reason: string;
  onDate: (value: string) => void;
  onTime: (value: string) => void;
  onReason: (value: string) => void;
  onCancel: () => void;
  onMove: () => void;
  onPay: () => void;
  paying: boolean;
  pending: boolean;
  online: boolean;
}) {
  const copy = useCopy();
  const router = useRouter();
  const changeable = visit.status === 'Confirmed' || visit.status === 'Requested';
  const unpaid =
    visit.paymentStatus !== 'Paid' &&
    (visit.status === 'Confirmed' || visit.status === 'Requested' || visit.status === 'CheckedIn');

  return (
    <Card elevated>
      <AppText variant="headline">{visit.reference ?? visit.id}</AppText>
      <StatusBadge label={label(copy, visit.status)} tone={statusTone(visit.status)} />
      <AppText>{visit.doctorName}</AppText>
      <AppText>{[visit.clinicName, visit.branchName].filter(Boolean).join(' · ')}</AppText>
      <AppText>{visit.startsAtUtc ? new Date(visit.startsAtUtc).toLocaleString() : '—'}</AppText>
      {visit.patientName ? <AppText>{visit.patientName}</AppText> : null}
      {typeof visit.consultationFee === 'number' ? (
        <AppText>
          {copy.consultationFee}: ₹{visit.consultationFee} ·{' '}
          {visit.paymentStatus === 'Paid' ? visit.paymentMethod : copy.payAtClinic}
        </AppText>
      ) : null}
      {unpaid ? (
        <Button label={copy.payOnline} loading={paying} disabled={!online} onPress={onPay} />
      ) : null}
      {visit.status === 'Confirmed' ? (
        <Button
          label={copy.showCheckInQr}
          variant="secondary"
          onPress={() => router.push(`/check-in/${visit.id}` as Href)}
        />
      ) : null}
      {changeable ? (
        <Button label={copy.reschedule} variant="secondary" disabled={!online} onPress={onOpen} />
      ) : null}
      {open && changeable ? (
        <>
          <TextField label={copy.newDate} value={date} onChangeText={onDate} placeholder="2026-11-06" />
          <TextField label={copy.newTime} value={time} onChangeText={onTime} placeholder="10:00" />
          <Button label={copy.reschedule} loading={pending} disabled={!online} onPress={onMove} />
          <TextField label={copy.reason} value={reason} onChangeText={onReason} />
          <Button label={copy.cancelVisit} variant="danger" loading={pending} disabled={!online} onPress={onCancel} />
          {visit.cancellationPolicy ? <AppText variant="muted">{visit.cancellationPolicy}</AppText> : null}
        </>
      ) : null}
    </Card>
  );
});
