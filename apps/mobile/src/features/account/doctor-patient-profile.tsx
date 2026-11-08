import { useQuery } from '@tanstack/react-query';
import { useLocalSearchParams, useRouter, type Href } from 'expo-router';
import { useState } from 'react';
import { Pressable, ScrollView, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { DoctorShell } from '@/components/layout/doctor-shell';
import { Screen } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { IconChevronRight, IconHealthRecords } from '@/components/ui/prodoc-icons';
import { ProfileAvatar } from '@/components/ui/profile-avatar';
import { Segmented } from '@/components/ui/segmented';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { openWorkspace } from '@/services/api/clinical-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

type Tab = 'overview' | 'history' | 'prescriptions' | 'reports';

export function DoctorPatientProfileScreen() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const { appointmentId } = useLocalSearchParams<{ appointmentId: string }>();
  const [tab, setTab] = useState<Tab>('overview');
  const workspace = useQuery({
    queryKey: ['workspace', appointmentId, 'patient-profile'],
    queryFn: () => openWorkspace(appointmentId!),
    enabled: Boolean(appointmentId),
  });
  const record = workspace.data?.record;

  const cards = [
    {
      title: copy.doctorChiefComplaints,
      body: workspace.data?.appointmentReason || copy.noneRecorded,
    },
    {
      title: copy.doctorMedicalHistory,
      body:
        record?.conditions
          .filter((item) => item.isActive)
          .map((item) => item.name)
          .join(', ') || copy.noneRecorded,
    },
    {
      title: copy.allergies,
      body:
        record?.allergies
          .filter((item) => item.isActive)
          .map((item) => item.substance)
          .join(', ') || copy.noneRecorded,
    },
    {
      title: copy.medications,
      body:
        record?.medications
          .filter((item) => item.isCurrent)
          .map((item) => item.name)
          .join(', ') || copy.noneRecorded,
    },
  ];

  return (
    <DoctorShell active="patients">
      <Screen>
        <ScrollView
          contentContainerStyle={{
            gap: spacing.md,
            paddingBottom: spacing.xxl,
            paddingTop: Math.max(insets.top - 8, 0),
          }}>
          <ScreenHeader title={copy.doctorPatientProfile} />
          {workspace.data ? (
            <>
              <View style={{ alignItems: 'center', gap: spacing.sm }}>
                <ProfileAvatar name={workspace.data.patientName} size={72} />
                <AppText variant="title">{workspace.data.patientName}</AppText>
                <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                  {workspace.data.appointmentStatus}
                  {workspace.data.appointmentReason ? ` · ${workspace.data.appointmentReason}` : ''}
                </AppText>
              </View>

              <Segmented
                value={tab}
                onChange={setTab}
                options={[
                  { value: 'overview', label: copy.doctorFilterAll },
                  { value: 'history', label: copy.doctorMedicalHistory },
                  { value: 'prescriptions', label: copy.doctorNavPrescriptions },
                  { value: 'reports', label: copy.doctorDocumentsReports },
                ]}
              />

              {tab === 'overview' || tab === 'history'
                ? cards.map((card) => (
                    <View
                      key={card.title}
                      style={{
                        backgroundColor: theme.colors.surface,
                        borderRadius: 16,
                        borderWidth: 1,
                        borderColor: theme.colors.border,
                        padding: spacing.lg,
                        flexDirection: 'row',
                        gap: spacing.md,
                        alignItems: 'center',
                      }}>
                      <View
                        style={{
                          width: 40,
                          height: 40,
                          borderRadius: 20,
                          backgroundColor: theme.colors.primaryLight,
                          alignItems: 'center',
                          justifyContent: 'center',
                        }}>
                        <IconHealthRecords size={18} color={theme.colors.primary} />
                      </View>
                      <View style={{ flex: 1, gap: 2 }}>
                        <AppText variant="label">{card.title}</AppText>
                        <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                          {card.body}
                        </AppText>
                      </View>
                      <IconChevronRight size={18} color={theme.colors.textMuted} />
                    </View>
                  ))
                : null}

              {tab === 'prescriptions' ? (
                <View style={{ gap: spacing.sm }}>
                  {(record?.prescriptions ?? []).length === 0 ? (
                    <AppText variant="muted">{copy.doctorPrescriptionsEmpty}</AppText>
                  ) : (
                    record!.prescriptions.map((item) => (
                      <Pressable
                        key={item.id}
                        onPress={() => router.push(`/prescription/${item.id}` as Href)}
                        style={{
                          backgroundColor: theme.colors.surface,
                          borderRadius: 14,
                          borderWidth: 1,
                          borderColor: theme.colors.border,
                          padding: spacing.md,
                        }}>
                        <AppText variant="label">
                          {item.number} · {item.status}
                        </AppText>
                      </Pressable>
                    ))
                  )}
                </View>
              ) : null}

              {tab === 'reports' ? <AppText variant="muted">{copy.doctorDocumentsReports}</AppText> : null}

              <Button
                label={copy.openConsultation}
                onPress={() => router.push(`/consultation/${appointmentId}` as Href)}
              />
            </>
          ) : null}
          {workspace.isLoading ? <AppText variant="muted">{copy.loading}</AppText> : null}
          {workspace.error ? (
            <AppText variant="muted">{problemMessage(workspace.error, copy.doctorPatientProfile)}</AppText>
          ) : null}
        </ScrollView>
      </Screen>
    </DoctorShell>
  );
}
