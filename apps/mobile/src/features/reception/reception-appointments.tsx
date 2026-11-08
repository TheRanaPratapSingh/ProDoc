import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { Alert, ScrollView, View } from 'react-native';

import { ReceptionClinicSwitcher, useReceptionDeskSelection } from '@/components/reception/clinic-switcher';
import { ReceptionShell } from '@/components/layout/reception-shell';
import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { CalendarView } from '@/components/ui/calendar-view';
import { Card } from '@/components/ui/card';
import { EmptyState } from '@/components/ui/empty-state';
import { PhoneInput } from '@/components/ui/phone-input';
import { SectionHeader } from '@/components/ui/section-header';
import { TimeSlotSelector } from '@/components/ui/time-slot-selector';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import { indianMobileOptionalSchema } from '@/schemas/common';
import { deskBookAppointment } from '@/services/api/reception-api';
import {
  getAvailability,
  getSlots,
  listPracticeAppointments,
} from '@/services/api/provider-api';
import { listDeskDoctors } from '@/services/api/queue-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { normalizeIndianMobile } from '@/utils/phone';
import { problemMessage } from '@/utils/problem';

function todayIso(offset = 0) {
  const date = new Date();
  date.setDate(date.getDate() + offset);
  return date.toISOString().slice(0, 10);
}

export function ReceptionAppointments() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const queryClient = useQueryClient();
  const { clinicId, branchId } = useReceptionDeskSelection();
  const [doctorId, setDoctorId] = useState('');
  const [date, setDate] = useState(todayIso());
  const [monthCursor, setMonthCursor] = useState(() => `${todayIso().slice(0, 7)}-01`);
  const [start, setStart] = useState('');
  const [patientName, setPatientName] = useState('');
  const [phone, setPhone] = useState('');
  const [reason, setReason] = useState('');

  const doctors = useQuery({
    queryKey: ['desk', 'doctors', clinicId, branchId],
    queryFn: () => listDeskDoctors(clinicId, branchId),
    enabled: Boolean(clinicId && branchId),
  });
  const activeDoctorId = doctorId || doctors.data?.[0]?.id || '';
  const monthBounds = useMemo(() => {
    const [y, m] = monthCursor.split('-').map(Number);
    const startDay = `${y}-${`${m}`.padStart(2, '0')}-01`;
    const endDate = new Date(y, m, 0);
    const end = `${endDate.getFullYear()}-${`${endDate.getMonth() + 1}`.padStart(2, '0')}-${`${endDate.getDate()}`.padStart(2, '0')}`;
    return { start: startDay, end };
  }, [monthCursor]);
  const calendar = useQuery({
    queryKey: ['availability', activeDoctorId, branchId, monthCursor],
    queryFn: () => getAvailability(activeDoctorId, monthBounds.start, monthBounds.end, branchId || undefined),
    enabled: Boolean(activeDoctorId),
  });
  const dayInfo = (calendar.data ?? []).find((item) => item.date === date);
  const scheduleId = dayInfo?.scheduleId ?? '';
  const slots = useQuery({
    queryKey: ['slots', scheduleId, date],
    queryFn: () => getSlots(scheduleId, date),
    enabled: Boolean(scheduleId && date),
  });
  const practice = useQuery({
    queryKey: ['appointments', 'practice', clinicId, date],
    queryFn: () => listPracticeAppointments(date, date, { clinicId }),
    enabled: Boolean(clinicId),
  });

  const days = useMemo(
    () =>
      (calendar.data ?? []).map((item) => ({
        date: item.date,
        status: item.status,
      })),
    [calendar.data],
  );

  const book = useMutation({
    mutationFn: async () => {
      const phoneCheck = indianMobileOptionalSchema.safeParse(phone);
      if (!phoneCheck.success) {
        throw new Error(phoneCheck.error.issues[0]?.message ?? 'Enter a valid Indian mobile number.');
      }
      const normalized = phone.trim() ? normalizeIndianMobile(phone) : undefined;
      if (phone.trim() && !normalized) {
        throw new Error('Enter a valid Indian mobile number.');
      }
      if (!patientName.trim() || !scheduleId || !start) {
        throw new Error('Patient name, available date, and time are required.');
      }
      return deskBookAppointment({
        scheduleId,
        date,
        start: start.length === 5 ? `${start}:00` : start,
        patientName: patientName.trim(),
        phone: normalized ?? undefined,
        reason: reason.trim() || undefined,
      });
    },
    onSuccess: async () => {
      setPatientName('');
      setPhone('');
      setReason('');
      setStart('');
      Alert.alert(copy.receptionBookTitle, 'Appointment booked.');
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['appointments'] }),
        queryClient.invalidateQueries({ queryKey: ['reception', 'dashboard'] }),
        queryClient.invalidateQueries({ queryKey: ['slots'] }),
        queryClient.invalidateQueries({ queryKey: ['availability'] }),
      ]);
    },
  });

  return (
    <ReceptionShell active="appointments">
      <AuthScaffold>
        <ScreenHeader title={copy.receptionBookTitle} />
        <ScrollView contentContainerStyle={{ gap: spacing.lg, paddingBottom: spacing.xxl }}>
          <ReceptionClinicSwitcher />
          <SectionHeader title={copy.receptionNavDoctors} />
          <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: spacing.sm }}>
            {(doctors.data ?? []).map((doctor) => (
              <Button
                key={doctor.id}
                label={doctor.displayName}
                variant={activeDoctorId === doctor.id ? 'primary' : 'secondary'}
                onPress={() => {
                  setDoctorId(doctor.id);
                  setStart('');
                }}
              />
            ))}
          </ScrollView>

          <CalendarView
            days={days}
            selectedDate={date}
            onSelect={(next) => {
              setDate(next);
              setStart('');
            }}
            monthCursor={monthCursor}
            onMonthChange={setMonthCursor}
          />
          <TimeSlotSelector
            slots={(slots.data?.slots ?? []).map((slot) => ({
              start: slot.start.slice(0, 5),
              end: slot.end?.slice(0, 5),
              disabled: slot.available === false,
            }))}
            selectedStart={start}
            onSelect={(value) => {
              const slot = slots.data?.slots.find((item) => item.start.slice(0, 5) === value || item.start === value);
              if (slot?.available === false) return;
              setStart(value);
            }}
            emptyLabel={copy.noSlotsOnDate}
          />

          <TextField label={copy.inviteFullName} value={patientName} onChangeText={setPatientName} />
          <PhoneInput label={copy.phoneLabel} value={phone} onChangeText={setPhone} />
          <TextField label="Reason" value={reason} onChangeText={setReason} placeholder="Optional" />
          {book.error ? <AppText style={{ color: theme.colors.error }}>{problemMessage(book.error, copy.receptionBookTitle)}</AppText> : null}
          <Button label={copy.receptionQuickBook} onPress={() => book.mutate()} loading={book.isPending} />

          <SectionHeader title={copy.receptionTodayVisits} />
          {(practice.data ?? []).length === 0 ? <EmptyState title={copy.receptionEmptyQueue} /> : null}
          {(practice.data ?? []).map((visit) => (
            <Card key={visit.id} style={{ gap: 4 }}>
              <AppText variant="label">{visit.patientName}</AppText>
              <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                {visit.doctorName} · {visit.status} · {new Date(visit.startsAtUtc).toLocaleTimeString()}
              </AppText>
            </Card>
          ))}
        </ScrollView>
      </AuthScaffold>
    </ReceptionShell>
  );
}
