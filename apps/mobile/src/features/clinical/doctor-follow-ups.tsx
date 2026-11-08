import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Pressable, ScrollView, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { DoctorShell } from '@/components/layout/doctor-shell';
import { Screen } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/ui/empty-state';
import { ErrorState } from '@/components/ui/error-state';
import { FollowUpCard } from '@/components/ui/follow-up-card';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { ClinicSwitcher } from '@/components/doctor/clinic-switcher';
import {
  cancelFollowUp,
  completeFollowUp,
  listDoctorFollowUps,
  updateFollowUp,
  type FollowUpDetails,
} from '@/services/api/follow-up-api';
import { listDoctorClinics } from '@/services/api/doctor-portal-api';
import { useDoctorClinicStore } from '@/stores/doctor-clinic-store';
import { notifySuccess } from '@/stores/feedback-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { notifyApiError, problemMessage } from '@/utils/problem';

const STATUS_FILTERS = ['All', 'Recommended', 'Due', 'Overdue', 'Appointment Booked', 'Completed', 'Cancelled'] as const;

export function DoctorFollowUpsScreen() {
  const theme = useProdocTheme();
  const insets = useSafeAreaInsets();
  const queryClient = useQueryClient();
  const clinicId = useDoctorClinicStore((state) => state.selectedClinicId);
  const [status, setStatus] = useState<(typeof STATUS_FILTERS)[number]>('All');
  const [patient, setPatient] = useState('');
  const [editing, setEditing] = useState<FollowUpDetails | null>(null);
  const [editDate, setEditDate] = useState('');
  const [editNote, setEditNote] = useState('');

  const clinics = useQuery({ queryKey: ['doctor', 'clinics'], queryFn: listDoctorClinics });
  const followUps = useQuery({
    queryKey: ['doctor', 'follow-ups', clinicId, status, patient],
    queryFn: () =>
      listDoctorFollowUps({
        clinicId: clinicId || undefined,
        status: status === 'All' ? undefined : status,
        patient: patient.trim() || undefined,
      }),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['doctor', 'follow-ups'] });

  const saveEdit = useMutation({
    mutationFn: () =>
      updateFollowUp(editing!.id, {
        recommendedDate: editDate,
        followUpNote: editNote.trim() || undefined,
      }),
    onSuccess: async () => {
      setEditing(null);
      notifySuccess('Follow-up updated.');
      await refresh();
    },
    onError: (error) => notifyApiError(error, 'Unable to update follow-up.'),
  });

  const cancel = useMutation({
    mutationFn: (id: string) => cancelFollowUp(id),
    onSuccess: async () => {
      notifySuccess('Follow-up cancelled.');
      await refresh();
    },
    onError: (error) => notifyApiError(error, 'Unable to cancel follow-up.'),
  });

  const complete = useMutation({
    mutationFn: (id: string) => completeFollowUp(id),
    onSuccess: async () => {
      notifySuccess('Follow-up marked completed.');
      await refresh();
    },
    onError: (error) => notifyApiError(error, 'Unable to complete follow-up.'),
  });

  const rows = followUps.data ?? [];

  return (
    <DoctorShell active="more">
      <Screen>
        <ScrollView
          contentContainerStyle={{
            gap: spacing.md,
            paddingBottom: Math.max(insets.bottom, spacing.xl) + spacing.lg,
            paddingTop: spacing.sm,
          }}>
          <ScreenHeader title="Follow-up Management" />
          <ClinicSwitcher clinics={clinics.data ?? []} />
          <TextField label="Patient" value={patient} onChangeText={setPatient} placeholder="Search patient" />
          <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: spacing.sm }}>
            {STATUS_FILTERS.map((item) => {
              const selected = status === item;
              return (
                <Pressable
                  key={item}
                  onPress={() => setStatus(item)}
                  style={{
                    paddingHorizontal: 12,
                    paddingVertical: 8,
                    borderRadius: 12,
                    borderWidth: 1,
                    borderColor: selected ? theme.colors.primary : theme.colors.border,
                    backgroundColor: selected ? theme.colors.primaryLight : theme.colors.surface,
                  }}>
                  <AppText variant="label" style={{ color: selected ? theme.colors.primary : theme.colors.text }}>
                    {item}
                  </AppText>
                </Pressable>
              );
            })}
          </ScrollView>

          {followUps.isError ? (
            <ErrorState
              title="Unable to load follow-ups"
              body={problemMessage(followUps.error, 'Unable to load follow-ups.')}
              actionLabel="Retry"
              onAction={() => void followUps.refetch()}
            />
          ) : null}
          {!followUps.isLoading && rows.length === 0 ? (
            <EmptyState title="No follow-ups" body="Recommended follow-ups for your patients will appear here." />
          ) : null}

          {rows.map((item) => (
            <FollowUpCard
              key={item.id}
              item={item}
              footer={
                item.status === 'Recommended' || item.status === 'AppointmentBooked' ? (
                  <View style={{ gap: spacing.sm, marginTop: spacing.sm }}>
                    <AppText variant="muted">Patient: {item.patientName}</AppText>
                    <AppText variant="muted">
                      Booked: {item.hasBookedAppointment ? 'Yes' : 'Not yet'}
                    </AppText>
                    <View style={{ flexDirection: 'row', gap: spacing.sm }}>
                      <View style={{ flex: 1 }}>
                        <Button
                          label="Edit"
                          variant="secondary"
                          onPress={() => {
                            setEditing(item);
                            setEditDate(item.recommendedDate);
                            setEditNote(item.followUpNote ?? '');
                          }}
                        />
                      </View>
                      <View style={{ flex: 1 }}>
                        <Button label="Complete" variant="secondary" onPress={() => complete.mutate(item.id)} />
                      </View>
                    </View>
                    <Button label="Cancel recommendation" variant="secondary" onPress={() => cancel.mutate(item.id)} />
                  </View>
                ) : (
                  <AppText variant="muted" style={{ marginTop: spacing.sm }}>
                    Patient: {item.patientName}
                  </AppText>
                )
              }
            />
          ))}

          {editing ? (
            <View
              style={{
                backgroundColor: theme.colors.surface,
                borderRadius: 16,
                borderWidth: 1,
                borderColor: theme.colors.border,
                padding: spacing.lg,
                gap: spacing.md,
              }}>
              <AppText variant="title" style={{ fontSize: 18 }}>
                Edit follow-up · {editing.patientName}
              </AppText>
              <TextField label="Follow-up date (YYYY-MM-DD) *" value={editDate} onChangeText={setEditDate} />
              <TextField label="Note (optional)" value={editNote} onChangeText={setEditNote} />
              <Button label="Save Follow-up" loading={saveEdit.isPending} onPress={() => saveEdit.mutate()} />
              <Button label="Close" variant="secondary" onPress={() => setEditing(null)} />
            </View>
          ) : null}
        </ScrollView>
      </Screen>
    </DoctorShell>
  );
}
