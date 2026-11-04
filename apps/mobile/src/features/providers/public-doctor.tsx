import { useQuery } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useState } from 'react';
import { Linking, View } from 'react-native';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ResponsiveHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { StatusBadge } from '@/components/ui/status-badge';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { TimeSlot } from '@/components/ui/time-slot';
import { DoctorReviewSection } from '@/features/reviews/review-list-section';
import { useCopy } from '@/i18n/copy';
import { addFavourite, getAvailability, getDiscoveredDoctor, getSlots } from '@/services/api/provider-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

function today() {
  return new Date().toISOString().slice(0, 10);
}

export function PublicDoctor({ doctorId }: { doctorId: string }) {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const [date, setDate] = useState(today());
  const [selectedDate, setSelectedDate] = useState(today());
  const [scheduleId, setScheduleId] = useState<string | null>(null);
  const doctor = useQuery({
    queryKey: ['discovery', 'doctor', doctorId],
    queryFn: () => getDiscoveredDoctor(doctorId),
  });
  const activeSchedule = scheduleId ?? doctor.data?.practices[0]?.scheduleId ?? null;
  const calendar = useQuery({
    queryKey: ['availability', doctorId, selectedDate],
    queryFn: () => getAvailability(doctorId, selectedDate, selectedDate),
    enabled: Boolean(doctorId),
  });
  const slots = useQuery({
    queryKey: ['slots', activeSchedule, selectedDate],
    queryFn: () => getSlots(activeSchedule!, selectedDate),
    enabled: Boolean(activeSchedule),
  });

  const initials =
    doctor.data?.displayName
      ?.split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase() ?? '')
      .join('') ?? 'Dr';

  return (
    <AuthScaffold>
      <ResponsiveHeader title={doctor.data?.displayName ?? copy.findDoctor} onBack={() => router.back()} backLabel={copy.back} />
      {doctor.data ? (
        <>
          <Card elevated>
            <View style={{ flexDirection: 'row', gap: spacing.md, alignItems: 'center' }}>
              <View
                style={{
                  width: 64,
                  height: 64,
                  borderRadius: radius.md,
                  backgroundColor: theme.colors.primaryLight,
                  alignItems: 'center',
                  justifyContent: 'center',
                }}>
                <AppText variant="headline" style={{ color: theme.colors.primary }}>
                  {initials}
                </AppText>
              </View>
              <View style={{ flex: 1, gap: spacing.xs }}>
                <AppText variant="headline">{doctor.data.displayName}</AppText>
                <AppText variant="muted">
                  {doctor.data.primarySpecialization} · {doctor.data.yearsOfExperience}
                </AppText>
                <StatusBadge label={copy.verified} tone="primary" />
              </View>
            </View>
            {doctor.data.biography ? <AppText>{doctor.data.biography}</AppText> : null}
            <AppText variant="muted">{doctor.data.qualifications.join(', ')}</AppText>
            <AppText variant="muted">{doctor.data.expertise.join(', ')}</AppText>
            <AppText variant="muted">{doctor.data.languages.join(', ')}</AppText>
            {doctor.data.registrationCouncil ? (
              <AppText variant="muted">
                {copy.registrationPublic}: {doctor.data.registrationCouncil}, {doctor.data.registrationState}
              </AppText>
            ) : null}
            <Button label={copy.favourite} variant="secondary" onPress={() => void addFavourite(doctor.data.id)} />
          </Card>
          {doctor.data.practices.map((place) => (
            <Card key={place.scheduleId} elevated>
              <AppText variant="headline">
                {[place.clinicName, place.branchName].filter(Boolean).join(' · ') || copy.independent}
              </AppText>
              <AppText variant="muted">{[place.addressLine, place.city].filter(Boolean).join(', ')}</AppText>
              <AppText variant="muted">
                {copy.consultationFee}: ₹{place.consultationFee} · {place.slotDurationMinutes} min
              </AppText>
              <Button
                label={copy.openTimes}
                variant={activeSchedule === place.scheduleId ? 'primary' : 'secondary'}
                onPress={() => setScheduleId(place.scheduleId)}
              />
              {place.directionsUrl ? (
                <Button label={copy.directions} variant="secondary" onPress={() => void Linking.openURL(place.directionsUrl!)} />
              ) : null}
              <Button
                label={copy.bookAppointment}
                onPress={() =>
                  router.push(`/book/${place.scheduleId}?doctorId=${doctor.data.id}&branchId=${place.branchId ?? ''}` as Href)
                }
              />
            </Card>
          ))}
          <Card elevated>
            <TextField label={copy.closedDate} value={date} onChangeText={setDate} />
            <Button label={copy.openTimes} onPress={() => setSelectedDate(date)} />
            <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
              {(slots.data ?? []).map((slot) => (
                <TimeSlot key={slot.start} label={slot.start.slice(0, 5)} onPress={() => undefined} />
              ))}
            </View>
            {slots.data?.length === 0 ? <AppText variant="muted">{copy.noSlots}</AppText> : null}
            {calendar.data?.map((day) => (
              <AppText key={day.date} variant="muted">
                {day.date}: {day.status}
                {day.slotCount ? ` · ${day.slotCount}` : ''}
              </AppText>
            ))}
            {slots.error ? <AppText variant="muted">{problemMessage(slots.error, copy.openTimes)}</AppText> : null}
          </Card>
          <DoctorReviewSection doctorId={doctor.data.id} />
        </>
      ) : null}
      {doctor.error ? <AppText variant="muted">{problemMessage(doctor.error, copy.findDoctor)}</AppText> : null}
    </AuthScaffold>
  );
}
