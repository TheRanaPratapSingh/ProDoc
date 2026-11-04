import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useMemo, useState } from 'react';
import { Linking, View } from 'react-native';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { TimeSlot } from '@/components/ui/time-slot';
import { useCopy } from '@/i18n/copy';
import { spacing } from '@/theme/tokens';
import {
  addFamily,
  confirmAppointment,
  getAvailability,
  getDiscoveredDoctor,
  getSlots,
  holdAppointment,
  listFamily,
  releaseAppointment,
  type AppointmentDetails,
} from '@/services/api/provider-api';
import { assertMutationAllowedOnline } from '@/services/offline/offline-queue';
import { selectIsEffectivelyOnline, useNetworkStore } from '@/stores/network-store';
import { problemMessage } from '@/utils/problem';

function localDate(offset = 0) {
  const date = new Date();
  date.setDate(date.getDate() + offset);
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

function stamp(value: string) {
  return new Date(value).toISOString().replace(/[-:]/g, '').replace(/\.\d{3}/, '');
}

export function BookVisit({ scheduleId, doctorId, branchId }: { scheduleId: string; doctorId: string; branchId: string }) {
  const copy = useCopy();
  const router = useRouter();
  const queryClient = useQueryClient();
  const online = useNetworkStore(selectIsEffectivelyOnline);
  const [date, setDate] = useState(localDate(0));
  const [hold, setHold] = useState<AppointmentDetails | null>(null);
  const [receipt, setReceipt] = useState<AppointmentDetails | null>(null);
  const [patientId, setPatientId] = useState('self');
  const [reason, setReason] = useState('');
  const [notes, setNotes] = useState('');
  const [familyName, setFamilyName] = useState('');
  const [relationship, setRelationship] = useState('child');
  const [offlineError, setOfflineError] = useState<string | null>(null);
  const doctor = useQuery({ queryKey: ['discovery', 'doctor', doctorId], queryFn: () => getDiscoveredDoctor(doctorId), enabled: Boolean(doctorId) });
  const calendar = useQuery({
    queryKey: ['availability', doctorId, branchId, 'book'],
    queryFn: () => getAvailability(doctorId, localDate(0), localDate(14), branchId || undefined),
    enabled: Boolean(doctorId),
  });
  const slots = useQuery({
    queryKey: ['slots', scheduleId, date],
    queryFn: () => getSlots(scheduleId, date),
    enabled: Boolean(scheduleId) && !hold && !receipt,
  });
  const family = useQuery({ queryKey: ['family'], queryFn: listFamily });
  const place = doctor.data?.practices.find((item) => item.scheduleId === scheduleId);
  const next = useMemo(() => calendar.data?.find((day) => day.status === 'available'), [calendar.data]);
  const statusLabel: Record<string, string> = {
    available: copy.available,
    unavailable: copy.unavailable,
    full: copy.full,
    holiday: copy.holiday,
  };

  const reserve = useMutation({
    mutationFn: async (start: string) => {
      assertMutationAllowedOnline('booking.hold', online);
      setOfflineError(null);
      if (hold) {
        await releaseAppointment(hold.id);
      }
      return holdAppointment(scheduleId, date, start);
    },
    onSuccess: (nextHold) => {
      setHold(nextHold);
      void queryClient.invalidateQueries({ queryKey: ['slots', scheduleId, date] });
    },
    onError: (error) => {
      if (error instanceof Error && error.message === 'offline_action_blocked') {
        setOfflineError(copy.offlineActionBlocked);
      }
    },
  });
  const confirm = useMutation({
    mutationFn: () => {
      assertMutationAllowedOnline('booking.confirm', online);
      setOfflineError(null);
      return confirmAppointment(hold!.id, {
        familyMemberId: patientId === 'self' ? undefined : patientId,
        reason: reason.trim() || undefined,
        notes: notes.trim() || undefined,
      });
    },
    onSuccess: (visit) => {
      setReceipt(visit);
      setHold(null);
      void queryClient.invalidateQueries({ queryKey: ['appointments'] });
    },
    onError: (error) => {
      if (error instanceof Error && error.message === 'offline_action_blocked') {
        setOfflineError(copy.offlineActionBlocked);
      }
    },
  });
  const createFamily = useMutation({
    mutationFn: () => addFamily({ fullName: familyName.trim(), relationship }),
    onSuccess: async (member) => {
      setPatientId(member.id);
      setFamilyName('');
      await queryClient.invalidateQueries({ queryKey: ['family'] });
    },
  });

  const shown = receipt ?? hold;
  const location = shown
    ? shown.latitude != null && shown.longitude != null
      ? `https://www.google.com/maps/search/?api=1&query=${shown.latitude},${shown.longitude}`
      : `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(shown.address)}`
    : null;
  const calendarUrl = receipt
    ? `https://calendar.google.com/calendar/render?action=TEMPLATE&text=${encodeURIComponent(receipt.doctorName)}&dates=${stamp(receipt.startsAtUtc)}/${stamp(receipt.endsAtUtc)}&location=${encodeURIComponent(receipt.address)}&details=${encodeURIComponent(receipt.reference ?? receipt.id)}`
    : null;
  const error = reserve.error ?? confirm.error ?? createFamily.error ?? calendar.error ?? slots.error;

  return (
    <AuthScaffold showHero={false}>
      <Button label={copy.back} variant="ghost" onPress={() => router.back()} />
      {!online ? <AppText variant="muted">{copy.offlineActionBlocked}</AppText> : null}
      {offlineError ? <AppText variant="muted">{offlineError}</AppText> : null}
      {doctor.data ? (
        <Card>
          <AppText variant="title">{doctor.data.displayName}</AppText>
          <AppText variant="muted">{copy.verified}</AppText>
          <AppText variant="muted">
            {doctor.data.primarySpecialization} · {doctor.data.yearsOfExperience}
          </AppText>
          <AppText variant="muted">{doctor.data.languages.join(', ')}</AppText>
          {place ? (
            <>
              <AppText>{[place.clinicName, place.branchName].filter(Boolean).join(' · ')}</AppText>
              <AppText variant="muted">{[place.addressLine, place.city].filter(Boolean).join(', ')}</AppText>
              <AppText variant="muted">
                {copy.consultationFee}: ₹{place.consultationFee}
                {place.distanceKm != null ? ` · ${place.distanceKm.toFixed(1)} ${copy.distance}` : ''}
              </AppText>
            </>
          ) : null}
        </Card>
      ) : null}
      {receipt ? (
        <Card>
          <AppText variant="title">{copy.bookingReference}</AppText>
          <AppText>{receipt.reference}</AppText>
          <AppText variant="muted">{receipt.status === 'Confirmed' ? copy.confirmed : copy.requested}</AppText>
          <AppText>{receipt.doctorName}</AppText>
          <AppText>{[receipt.clinicName, receipt.branchName].filter(Boolean).join(' · ')}</AppText>
          <AppText variant="muted">{receipt.address}</AppText>
          <AppText>
            {new Date(receipt.startsAtUtc).toLocaleString()} – {new Date(receipt.endsAtUtc).toLocaleTimeString()}
          </AppText>
          <AppText>{receipt.patientName}</AppText>
          <AppText>
            {copy.consultationFee}: ₹{receipt.consultationFee}
          </AppText>
          <AppText variant="muted">{copy.payAtClinic}</AppText>
          <Button label={copy.myVisits} variant="secondary" onPress={() => router.push('/visits' as Href)} />
          {calendarUrl ? <Button label={copy.addToCalendar} variant="secondary" onPress={() => void Linking.openURL(calendarUrl)} /> : null}
          {location ? <Button label={copy.viewLocation} variant="secondary" onPress={() => void Linking.openURL(location)} /> : null}
          <Button label={copy.backHome} onPress={() => router.replace('/home' as Href)} />
        </Card>
      ) : null}
      {hold && !receipt ? (
        <Card>
          <AppText variant="label">{copy.holdMinutes}</AppText>
          <AppText>{hold.doctorName}</AppText>
          <AppText>{[hold.clinicName, hold.branchName].join(' · ')}</AppText>
          <AppText>{new Date(hold.startsAtUtc).toLocaleString()}</AppText>
          <AppText>
            {copy.consultationFee}: ₹{hold.consultationFee}
          </AppText>
          <AppText variant="muted">{hold.requiresApproval ? copy.approvalRequired : copy.instantBooking}</AppText>
          <AppText variant="muted">{copy.payAtClinic}</AppText>
          <AppText variant="muted">
            {copy.cancellationPolicy}: {hold.cancellationPolicy}
          </AppText>
          <AppText variant="label">{copy.visitFor}</AppText>
          <Button label={copy.myself} variant={patientId === 'self' ? 'primary' : 'secondary'} onPress={() => setPatientId('self')} />
          {(family.data ?? []).map((member) => (
            <Button
              key={member.id}
              label={`${member.fullName} · ${member.relationship}`}
              variant={patientId === member.id ? 'primary' : 'secondary'}
              onPress={() => setPatientId(member.id)}
            />
          ))}
          <TextField label={copy.patientName} value={familyName} onChangeText={setFamilyName} />
          <Button label={copy.child} variant={relationship === 'child' ? 'primary' : 'secondary'} onPress={() => setRelationship('child')} />
          <Button label={copy.spouse} variant={relationship === 'spouse' ? 'primary' : 'secondary'} onPress={() => setRelationship('spouse')} />
          <Button label={copy.parent} variant={relationship === 'parent' ? 'primary' : 'secondary'} onPress={() => setRelationship('parent')} />
          <Button label={copy.other} variant={relationship === 'other' ? 'primary' : 'secondary'} onPress={() => setRelationship('other')} />
          <Button label={copy.addFamily} variant="secondary" loading={createFamily.isPending} onPress={() => createFamily.mutate()} />
          <TextField label={copy.reason} value={reason} onChangeText={setReason} />
          <TextField label={copy.symptoms} value={notes} onChangeText={setNotes} />
          <Button label={copy.confirmBooking} loading={confirm.isPending} onPress={() => confirm.mutate()} />
        </Card>
      ) : null}
      {!hold && !receipt ? (
        <Card>
          <AppText variant="label">{copy.selectDate}</AppText>
          {next ? (
            <AppText variant="muted">
              {copy.nextAvailable}: {next.date}
            </AppText>
          ) : null}
          {(calendar.data ?? []).map((day) => (
            <Button
              key={day.date}
              label={`${day.date} · ${statusLabel[day.status] ?? copy.unavailable}`}
              variant={date === day.date ? 'primary' : 'secondary'}
              disabled={day.status !== 'available'}
              onPress={() => setDate(day.date)}
            />
          ))}
          <AppText variant="label">{copy.selectTime}</AppText>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
            {(slots.data ?? []).map((slot) => (
              <TimeSlot
                key={slot.start}
                label={`${slot.start.slice(0, 5)}`}
                disabled={reserve.isPending}
                onPress={() => reserve.mutate(slot.start)}
              />
            ))}
          </View>
          {slots.data?.length === 0 ? <AppText variant="muted">{copy.noSlots}</AppText> : null}
        </Card>
      ) : null}
      {error ? <AppText variant="muted">{problemMessage(error, copy.bookAppointment)}</AppText> : null}
    </AuthScaffold>
  );
}
