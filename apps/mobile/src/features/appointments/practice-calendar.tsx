import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useMemo, useState } from 'react';
import { Pressable, ScrollView, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { ClinicSwitcher, useSelectedDoctorClinicId } from '@/components/doctor/clinic-switcher';
import { DoctorShell } from '@/components/layout/doctor-shell';
import { Screen } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { IconChevronRight } from '@/components/ui/prodoc-icons';
import { ProfileAvatar } from '@/components/ui/profile-avatar';
import { SearchBar } from '@/components/ui/search-bar';
import { Segmented } from '@/components/ui/segmented';
import { StatusBadge } from '@/components/ui/status-badge';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import { listDoctorClinics, listPracticeAppointmentsScoped } from '@/services/api/doctor-portal-api';
import { bookWalkIn, decideAppointment, listMySchedules } from '@/services/api/provider-api';
import { useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

function localDate(offset = 0) {
  const date = new Date();
  date.setDate(date.getDate() + offset);
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

type FilterKey = 'today' | 'upcoming' | 'completed' | 'cancelled' | 'all';

function statusTone(status: string): 'neutral' | 'primary' | 'success' | 'warning' | 'error' {
  const value = status.toLowerCase();
  if (value.includes('wait') || value.includes('check') || value.includes('request')) return 'warning';
  if (value.includes('complete') || value.includes('confirm')) return 'success';
  if (value.includes('cancel') || value.includes('reject')) return 'error';
  if (value.includes('consult')) return 'primary';
  return 'neutral';
}

export function PracticeCalendar() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const queryClient = useQueryClient();
  const canWalkIn = useSessionStore((state) => state.account?.permissions.includes('appointments.book.walkin') ?? false);
  const clinics = useQuery({ queryKey: ['doctor', 'clinics'], queryFn: listDoctorClinics });
  const clinicId = useSelectedDoctorClinicId(clinics.data);
  const from = localDate(-7);
  const to = localDate(21);
  const today = localDate(0);
  const visits = useQuery({
    queryKey: ['appointments', 'practice', from, to, clinicId ?? 'all'],
    queryFn: () => listPracticeAppointmentsScoped(from, to, clinicId, true),
  });
  const schedules = useQuery({ queryKey: ['schedules', 'mine'], queryFn: listMySchedules, enabled: canWalkIn });
  const [scheduleId, setScheduleId] = useState<string | null>(null);
  const [date, setDate] = useState(localDate(1));
  const [time, setTime] = useState('09:00');
  const [patientName, setPatientName] = useState('');
  const [reason, setReason] = useState('');
  const [filter, setFilter] = useState<FilterKey>('today');
  const [search, setSearch] = useState('');

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['appointments', 'practice'] });
    await queryClient.invalidateQueries({ queryKey: ['doctor'] });
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

  const counts = useMemo(() => {
    const rows = visits.data ?? [];
    return {
      today: rows.filter((visit) => localDateFromUtc(visit.startsAtUtc) === today).length,
      upcoming: rows.filter(
        (visit) =>
          localDateFromUtc(visit.startsAtUtc) > today &&
          ['Confirmed', 'Requested', 'CheckedIn'].includes(visit.status),
      ).length,
      completed: rows.filter((visit) => visit.status === 'Completed').length,
    };
  }, [today, visits.data]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return (visits.data ?? []).filter((visit) => {
      const iso = localDateFromUtc(visit.startsAtUtc);
      if (filter === 'today' && iso !== today) return false;
      if (filter === 'upcoming' && !(iso > today && ['Confirmed', 'Requested', 'CheckedIn'].includes(visit.status))) return false;
      if (filter === 'completed' && visit.status !== 'Completed') return false;
      if (filter === 'cancelled' && !['Cancelled', 'Rejected'].includes(visit.status)) return false;
      if (!q) return true;
      return (
        visit.patientName.toLowerCase().includes(q) ||
        (visit.reference ?? '').toLowerCase().includes(q) ||
        (visit.clinicName ?? '').toLowerCase().includes(q)
      );
    });
  }, [visits.data, filter, search, today]);

  const clinicSchedules = (schedules.data ?? []).filter((item) => item.clinicId && item.branchId);
  const error = visits.error ?? decision.error ?? walkIn.error;

  return (
    <DoctorShell active="appointments">
      <Screen>
        <ScrollView
          contentContainerStyle={{
            gap: spacing.md,
            paddingBottom: spacing.xxl,
            paddingTop: Math.max(insets.top - 8, 0),
          }}>
          <ScreenHeader title={copy.practiceVisits} />
          <ClinicSwitcher clinics={clinics.data ?? []} onChanged={() => void refresh()} />
          <Segmented
            value={filter}
            onChange={setFilter}
            options={[
              { value: 'today', label: `${copy.doctorFilterToday} (${counts.today})` },
              { value: 'upcoming', label: copy.doctorFilterUpcoming },
              { value: 'completed', label: copy.doctorFilterCompleted },
              { value: 'cancelled', label: copy.doctorFilterCancelled },
              { value: 'all', label: copy.doctorFilterAll },
            ]}
          />
          <SearchBar
            placeholder={copy.doctorSearchAppointment}
            value={search}
            onChangeText={setSearch}
            onClear={() => setSearch('')}
          />

          {filtered.map((visit) => (
            <Pressable
              key={visit.id}
              onPress={() => router.push(`/doctor-appointment/${visit.id}` as Href)}
              style={{
                backgroundColor: theme.colors.surface,
                borderRadius: 16,
                borderWidth: 1,
                borderColor: theme.colors.border,
                padding: spacing.md,
                flexDirection: 'row',
                alignItems: 'center',
                gap: spacing.md,
              }}>
              <ProfileAvatar name={visit.patientName} size={44} />
              <View style={{ flex: 1, gap: 2 }}>
                <AppText variant="label" numberOfLines={1}>
                  {visit.patientName || copy.patientName}
                </AppText>
                <AppText variant="caption" style={{ color: theme.colors.textMuted }} numberOfLines={1}>
                  {new Date(visit.startsAtUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                  {visit.reason ? ` · ${visit.reason}` : visit.reference ? ` · ${visit.reference}` : ''}
                </AppText>
              </View>
              <StatusBadge label={visit.status} tone={statusTone(visit.status)} />
              <IconChevronRight size={18} color={theme.colors.textMuted} />
            </Pressable>
          ))}

          {filtered.length === 0 ? <AppText variant="muted">{copy.noVisits}</AppText> : null}

          {canWalkIn ? (
            <View
              style={{
                backgroundColor: theme.colors.surface,
                borderRadius: 16,
                borderWidth: 1,
                borderColor: theme.colors.border,
                padding: spacing.lg,
                gap: spacing.sm,
              }}>
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
            </View>
          ) : null}

          <Button
            label={copy.doctorBookAppointment}
            onPress={() => router.push(canWalkIn ? '/practice' : '/schedule')}
          />
          {error ? <AppText variant="muted">{problemMessage(error, copy.practiceVisits)}</AppText> : null}
        </ScrollView>
      </Screen>
    </DoctorShell>
  );
}

function localDateFromUtc(value: string) {
  const day = new Date(value);
  return `${day.getFullYear()}-${`${day.getMonth() + 1}`.padStart(2, '0')}-${`${day.getDate()}`.padStart(2, '0')}`;
}
