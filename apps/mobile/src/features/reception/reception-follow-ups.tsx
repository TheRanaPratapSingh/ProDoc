import { useQuery } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useState } from 'react';
import { ScrollView, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { ReceptionShell } from '@/components/layout/reception-shell';
import { Screen } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { ReceptionClinicSwitcher, useReceptionDeskSelection } from '@/components/reception/clinic-switcher';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/ui/empty-state';
import { ErrorState } from '@/components/ui/error-state';
import { FollowUpCard } from '@/components/ui/follow-up-card';
import { TextField } from '@/components/ui/text-field';
import { AppText } from '@/components/ui/text';
import { listReceptionFollowUps } from '@/services/api/follow-up-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

export function ReceptionFollowUpsScreen() {
  const theme = useProdocTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const { clinicId } = useReceptionDeskSelection();
  const [patient, setPatient] = useState('');

  const followUps = useQuery({
    queryKey: ['reception', 'follow-ups', clinicId, patient],
    queryFn: () =>
      listReceptionFollowUps({
        clinicId,
        patient: patient.trim() || undefined,
      }),
    enabled: Boolean(clinicId),
  });

  return (
    <ReceptionShell active="appointments">
      <Screen>
        <ScrollView
          contentContainerStyle={{
            gap: spacing.md,
            paddingBottom: Math.max(insets.bottom, spacing.xl) + spacing.lg,
            paddingTop: spacing.sm,
          }}>
          <ScreenHeader title="Follow-up Recommendations" />
          <ReceptionClinicSwitcher />
          <AppText variant="muted" style={{ lineHeight: 20, color: theme.colors.textMuted }}>
            View doctor recommendations for your clinic. Only doctors can change clinical recommendations.
          </AppText>
          <TextField label="Patient" value={patient} onChangeText={setPatient} placeholder="Search patient" />

          {!clinicId ? (
            <EmptyState title="Select a clinic" body="Choose a clinic to view upcoming follow-ups." />
          ) : null}
          {followUps.isError ? (
            <ErrorState
              title="Unable to load follow-ups"
              body={problemMessage(followUps.error, 'Unable to load follow-ups.')}
              actionLabel="Retry"
              onAction={() => void followUps.refetch()}
            />
          ) : null}
          {clinicId && !followUps.isLoading && (followUps.data?.length ?? 0) === 0 ? (
            <EmptyState title="No follow-ups" body="Upcoming doctor follow-up recommendations will appear here." />
          ) : null}

          {(followUps.data ?? []).map((item) => (
            <View key={item.id} style={{ gap: spacing.sm }}>
              <FollowUpCard
                item={item}
                footer={
                  <View style={{ gap: spacing.xs, marginTop: spacing.sm }}>
                    <AppText variant="muted">Patient: {item.patientName}</AppText>
                    <AppText variant="muted">
                      Appointment booked: {item.hasBookedAppointment ? 'Yes' : 'Not yet'}
                    </AppText>
                  </View>
                }
              />
              {!item.hasBookedAppointment && item.requiresBooking && item.status !== 'Cancelled' ? (
                <Button
                  label="Help book follow-up"
                  onPress={() =>
                    router.push(
                      `/practices/${item.doctorId}?clinicId=${encodeURIComponent(item.clinicId)}&branchId=${encodeURIComponent(item.branchId)}&followUpId=${encodeURIComponent(item.id)}` as Href,
                    )
                  }
                />
              ) : null}
            </View>
          ))}
        </ScrollView>
      </Screen>
    </ReceptionShell>
  );
}
