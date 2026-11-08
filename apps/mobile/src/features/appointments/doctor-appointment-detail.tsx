import { useQuery } from '@tanstack/react-query';
import { useLocalSearchParams, useRouter, type Href } from 'expo-router';
import { Pressable, ScrollView, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { DoctorShell } from '@/components/layout/doctor-shell';
import { Screen } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { ClinicLocationCard } from '@/components/ui/clinic-location-card';
import { IconChevronRight, IconHealthRecords } from '@/components/ui/prodoc-icons';
import { ProfileAvatar } from '@/components/ui/profile-avatar';
import { StatusBadge } from '@/components/ui/status-badge';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { getPracticeAppointment } from '@/services/api/provider-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

function statusTone(status: string): 'neutral' | 'primary' | 'success' | 'warning' | 'error' {
  const value = status.toLowerCase();
  if (value.includes('wait') || value.includes('check')) return 'warning';
  if (value.includes('complete') || value.includes('confirm')) return 'success';
  if (value.includes('cancel') || value.includes('reject')) return 'error';
  if (value.includes('consult')) return 'primary';
  return 'neutral';
}

function DetailRow({ label, value }: { label: string; value: string }) {
  const theme = useProdocTheme();
  return (
    <View style={{ flexDirection: 'row', justifyContent: 'space-between', gap: spacing.md, paddingVertical: 6 }}>
      <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
        {label}
      </AppText>
      <AppText variant="label" style={{ flex: 1, textAlign: 'right' }} numberOfLines={2}>
        {value}
      </AppText>
    </View>
  );
}

export function DoctorAppointmentDetailScreen() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const { id } = useLocalSearchParams<{ id: string }>();
  const appointment = useQuery({
    queryKey: ['appointments', 'practice', id],
    queryFn: () => getPracticeAppointment(id!),
    enabled: Boolean(id),
  });
  const visit = appointment.data;

  const patientHref = (id ? `/doctor-patient/${id}` : '/doctor-patients') as Href;
  const links = [
    { title: copy.doctorMedicalHistory, href: patientHref },
    {
      title: copy.doctorPreviousPrescriptions,
      href: patientHref,
    },
    { title: copy.doctorDocumentsReports, href: patientHref },
  ];

  return (
    <DoctorShell active="appointments">
      <Screen>
        <ScrollView
          contentContainerStyle={{
            gap: spacing.md,
            paddingBottom: spacing.xxl,
            paddingTop: Math.max(insets.top - 8, 0),
          }}>
          <ScreenHeader title={copy.doctorAppointmentDetails} />
          {visit ? (
            <>
              <View
                style={{
                  backgroundColor: theme.colors.surface,
                  borderRadius: 18,
                  borderWidth: 1,
                  borderColor: theme.colors.border,
                  padding: spacing.lg,
                  gap: spacing.md,
                }}>
                <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.md }}>
                  <ProfileAvatar name={visit.patientName} size={56} />
                  <View style={{ flex: 1, gap: 2 }}>
                    <AppText variant="title" numberOfLines={1}>
                      {visit.patientName}
                    </AppText>
                    <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                      {[visit.clinicName, visit.branchName]
                        .map((part) => part?.trim())
                        .filter(Boolean)
                        .filter((part, index, all) => index === 0 || part?.toLowerCase() !== all[0]?.toLowerCase())
                        .join(' · ')}
                    </AppText>
                  </View>
                </View>
                <ClinicLocationCard
                  title={copy.clinicLocation}
                  clinicName={
                    [visit.clinicName, visit.branchName]
                      .map((part) => part?.trim())
                      .filter(Boolean)
                      .filter((part, index, all) => index === 0 || part?.toLowerCase() !== all[0]?.toLowerCase())
                      .join(' · ')
                  }
                  formattedAddress={visit.address}
                  latitude={visit.latitude}
                  longitude={visit.longitude}
                  googlePlaceId={visit.googlePlaceId}
                  mapsUrl={visit.mapsUrl}
                  directionsUrl={visit.directionsUrl}
                  locationVerified={visit.locationVerified}
                  unavailableLabel={copy.clinicLocationUnavailable}
                  viewOnMapLabel={copy.viewOnGoogleMaps}
                  getDirectionsLabel={copy.getDirections}
                  unverifiedHint={copy.clinicLocationUnverified}
                />
                <DetailRow label={copy.doctorAppointmentId} value={visit.reference ?? visit.id.slice(0, 8).toUpperCase()} />
                {visit.tokenLabel ? <DetailRow label={copy.tokenNumber} value={visit.tokenLabel} /> : null}
                <DetailRow
                  label={copy.doctorDateTime}
                  value={new Date(visit.startsAtUtc).toLocaleString([], {
                    weekday: 'short',
                    day: 'numeric',
                    month: 'short',
                    hour: 'numeric',
                    minute: '2-digit',
                  })}
                />
                <DetailRow label={copy.doctorVisitType} value={visit.reason || copy.doctorGeneralCheckup} />
                <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
                  <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                    {copy.status}
                  </AppText>
                  <StatusBadge label={visit.status} tone={statusTone(visit.status)} />
                </View>
              </View>

              <Button
                label={copy.openConsultation}
                onPress={() => router.push(`/consultation/${visit.id}` as Href)}
              />
              <Button
                label={copy.doctorViewPatientProfile}
                variant="secondary"
                onPress={() => router.push(`/doctor-patient/${visit.id}` as Href)}
              />

              <View
                style={{
                  backgroundColor: theme.colors.surface,
                  borderRadius: 18,
                  borderWidth: 1,
                  borderColor: theme.colors.border,
                  overflow: 'hidden',
                }}>
                {links.map((link, index) => (
                  <Pressable
                    key={link.title}
                    onPress={() => router.push(link.href)}
                    style={{
                      flexDirection: 'row',
                      alignItems: 'center',
                      gap: spacing.md,
                      paddingHorizontal: spacing.lg,
                      paddingVertical: spacing.md + 2,
                      borderTopWidth: index === 0 ? 0 : 1,
                      borderTopColor: theme.colors.border,
                    }}>
                    <View
                      style={{
                        width: 36,
                        height: 36,
                        borderRadius: 18,
                        backgroundColor: theme.colors.primaryLight,
                        alignItems: 'center',
                        justifyContent: 'center',
                      }}>
                      <IconHealthRecords size={18} color={theme.colors.primary} />
                    </View>
                    <AppText variant="label" style={{ flex: 1 }}>
                      {link.title}
                    </AppText>
                    <IconChevronRight size={18} color={theme.colors.textMuted} />
                  </Pressable>
                ))}
              </View>
            </>
          ) : null}
          {appointment.isLoading ? <AppText variant="muted">{copy.loading}</AppText> : null}
          {appointment.error ? (
            <AppText variant="muted">{problemMessage(appointment.error, copy.doctorAppointmentDetails)}</AppText>
          ) : null}
        </ScrollView>
      </Screen>
    </DoctorShell>
  );
}
