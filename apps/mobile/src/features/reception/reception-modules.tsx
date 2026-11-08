import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useState } from 'react';
import { Alert, ScrollView, View } from 'react-native';

import { ReceptionClinicSwitcher, useReceptionDeskSelection } from '@/components/reception/clinic-switcher';
import { ReceptionShell, type ReceptionTab } from '@/components/layout/reception-shell';
import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ClinicLocationCard } from '@/components/ui/clinic-location-card';
import { EmptyState } from '@/components/ui/empty-state';
import { DashboardStatCard } from '@/components/ui/stat-card';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { NotificationCenter } from '@/features/notifications/notification-center';
import { useCopy } from '@/i18n/copy';
import {
  getReceptionClinicInfo,
  getReceptionDashboard,
  getReceptionRecordsStatus,
  getReceptionReport,
} from '@/services/api/reception-api';
import { listPracticeAppointments } from '@/services/api/provider-api';
import { listDeskDoctors } from '@/services/api/queue-api';
import { clearSession, useSessionStore } from '@/stores/session-store';
import { logout } from '@/services/api/auth-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { needsRoleSelection } from '@/utils/home-surface';
import { problemMessage } from '@/utils/problem';
import { formatYearsExperience } from '@/utils/years-of-experience';

function todayIso(offset = 0) {
  const date = new Date();
  date.setDate(date.getDate() + offset);
  return date.toISOString().slice(0, 10);
}

function ModuleFrame({
  active,
  title,
  children,
}: {
  active: ReceptionTab;
  title: string;
  children: React.ReactNode;
}) {
  return (
    <ReceptionShell active={active}>
      <AuthScaffold>
        <ScreenHeader title={title} />
        <ScrollView contentContainerStyle={{ gap: spacing.lg, paddingBottom: spacing.xxl }}>
          <ReceptionClinicSwitcher />
          {children}
        </ScrollView>
      </AuthScaffold>
    </ReceptionShell>
  );
}

export function ReceptionDoctors() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const { clinicId, branchId } = useReceptionDeskSelection();
  const doctors = useQuery({
    queryKey: ['desk', 'doctors', clinicId, branchId],
    queryFn: () => listDeskDoctors(clinicId, branchId),
    enabled: Boolean(clinicId && branchId),
  });
  const dashboard = useQuery({
    queryKey: ['reception', 'dashboard', clinicId, branchId],
    queryFn: () => getReceptionDashboard(clinicId, branchId),
    enabled: Boolean(clinicId),
  });

  return (
    <ModuleFrame active="doctors" title={copy.receptionDoctorsTitle}>
      {(doctors.data ?? []).length === 0 ? <EmptyState title={copy.receptionNoClinics} /> : null}
      {(doctors.data ?? []).map((doctor) => {
        const overview = (dashboard.data?.doctors ?? []).find((item) => item.doctorId === doctor.id);
        return (
          <Card key={doctor.id} style={{ gap: 4 }}>
            <AppText variant="label">{doctor.displayName}</AppText>
            <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
              {formatYearsExperience(doctor.yearsOfExperience, copy.yearsExp)}
            </AppText>
            <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
              {overview?.todayAppointments ?? 0} visits today · {overview?.waiting ?? 0} waiting
            </AppText>
          </Card>
        );
      })}
    </ModuleFrame>
  );
}

export function ReceptionCalendar() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const { clinicId } = useReceptionDeskSelection();
  const from = todayIso(0);
  const to = todayIso(7);
  const practice = useQuery({
    queryKey: ['appointments', 'practice', clinicId, from, to],
    queryFn: () => listPracticeAppointments(from, to, { clinicId }),
    enabled: Boolean(clinicId),
  });

  return (
    <ModuleFrame active="calendar" title={copy.receptionCalendarTitle}>
      <Button
        label={copy.receptionNavAvailability}
        variant="secondary"
        onPress={() => router.push('/reception-availability' as Href)}
      />
      {(practice.data ?? []).length === 0 ? <EmptyState title={copy.receptionEmptyQueue} /> : null}
      {(practice.data ?? []).map((visit) => (
        <Card key={visit.id} style={{ gap: 4 }}>
          <AppText variant="label">{visit.patientName}</AppText>
          <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
            {visit.doctorName} · {visit.status} · {new Date(visit.startsAtUtc).toLocaleString()}
            {visit.isWalkIn ? ' · walk-in' : ''}
          </AppText>
        </Card>
      ))}
    </ModuleFrame>
  );
}

export function ReceptionClinicInfoScreen() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const { clinicId } = useReceptionDeskSelection();
  const info = useQuery({
    queryKey: ['reception', 'clinic', clinicId],
    queryFn: () => getReceptionClinicInfo(clinicId),
    enabled: Boolean(clinicId),
  });

  return (
    <ModuleFrame active="clinic" title={copy.receptionClinicTitle}>
      {info.error ? <AppText style={{ color: theme.colors.error }}>{problemMessage(info.error, copy.receptionClinicTitle)}</AppText> : null}
      {info.data ? (
        <Card style={{ gap: spacing.sm }}>
          <AppText variant="label">{info.data.name}</AppText>
          <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
            {info.data.type ?? 'Clinic'} {info.data.phoneNumber ? `· ${info.data.phoneNumber}` : ''}
          </AppText>
          {info.data.branches.map((branch) => (
            <ClinicLocationCard
              key={branch.id}
              title={copy.clinicLocation}
              clinicName={branch.name}
              addressLine={branch.addressLine}
              addressLine2={branch.addressLine2}
              landmark={branch.landmark}
              locality={branch.locality}
              city={branch.city}
              district={branch.district}
              state={branch.state}
              postalCode={branch.postalCode}
              country={branch.country}
              formattedAddress={branch.formattedAddress}
              latitude={branch.latitude}
              longitude={branch.longitude}
              googlePlaceId={branch.googlePlaceId}
              mapsUrl={branch.mapsUrl}
              directionsUrl={branch.directionsUrl}
              locationVerified={branch.locationVerified}
              unavailableLabel={copy.clinicLocationUnavailable}
              viewOnMapLabel={copy.viewOnGoogleMaps}
              getDirectionsLabel={copy.getDirections}
              unverifiedHint={copy.clinicLocationUnverified}
            />
          ))}
          <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
            Doctors: {info.data.doctors.map((doctor) => doctor.displayName).join(', ') || '—'}
          </AppText>
        </Card>
      ) : null}
    </ModuleFrame>
  );
}

export function ReceptionReviews() {
  const copy = useCopy();
  const router = useRouter();
  return (
    <ModuleFrame active="reviews" title={copy.receptionReviewsTitle}>
      <Card style={{ gap: spacing.sm }}>
        <AppText variant="body">
          Reception can view public clinic/doctor reviews and flag inappropriate ones. Responses remain with clinic
          admins and doctors.
        </AppText>
        <Button label="Open doctor directory reviews" variant="secondary" onPress={() => router.push('/doctors' as Href)} />
      </Card>
    </ModuleFrame>
  );
}

export function ReceptionReports() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const { clinicId } = useReceptionDeskSelection();
  const from = todayIso(-7);
  const to = todayIso(0);
  const report = useQuery({
    queryKey: ['reception', 'reports', clinicId, from, to],
    queryFn: () => getReceptionReport(clinicId, from, to),
    enabled: Boolean(clinicId),
  });

  return (
    <ModuleFrame active="reports" title={copy.receptionReportsTitle}>
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md }}>
        <DashboardStatCard label="Appts" value={String(report.data?.appointments ?? 0)} />
        <DashboardStatCard label="Walk-ins" value={String(report.data?.walkIns ?? 0)} />
        <DashboardStatCard label="Done" value={String(report.data?.completed ?? 0)} />
        <DashboardStatCard label="Collected" value={`₹${Math.round(report.data?.collected ?? 0)}`} />
      </View>
      {(report.data?.byDoctor ?? []).map((row) => (
        <Card key={row.doctorId} style={{ gap: 4 }}>
          <AppText variant="label">{row.doctorName}</AppText>
          <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
            {row.appointments} visits · {row.completed} done · ₹{Math.round(row.collected)}
          </AppText>
        </Card>
      ))}
    </ModuleFrame>
  );
}

export function ReceptionRecords() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const { clinicId } = useReceptionDeskSelection();
  const [appointmentId, setAppointmentId] = useState('');
  const status = useQuery({
    queryKey: ['reception', 'records', appointmentId],
    queryFn: () => getReceptionRecordsStatus(appointmentId),
    enabled: Boolean(appointmentId.trim().length > 10),
  });
  const practice = useQuery({
    queryKey: ['appointments', 'practice', clinicId, 'records'],
    queryFn: () => listPracticeAppointments(todayIso(-1), todayIso(0), { clinicId }),
    enabled: Boolean(clinicId),
  });

  return (
    <ModuleFrame active="records" title={copy.receptionRecordsTitle}>
      <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
        View Rx readiness and coordinate reports only — no create/edit/sign of prescriptions.
      </AppText>
      <TextField label="Appointment ID" value={appointmentId} onChangeText={setAppointmentId} />
      {status.data ? (
        <Card style={{ gap: 4 }}>
          <AppText variant="label">Consultation: {status.data.consultationStatus ?? 'None'}</AppText>
          <AppText variant="body">
            Prescription: {status.data.prescriptionReady ? 'Ready' : 'Not ready'} ({status.data.prescriptionStatus ?? '—'})
          </AppText>
        </Card>
      ) : null}
      {(practice.data ?? []).slice(0, 8).map((visit) => (
        <Button
          key={visit.id}
          label={`${visit.patientName} · ${visit.status}`}
          variant="secondary"
          onPress={() => setAppointmentId(visit.id)}
        />
      ))}
    </ModuleFrame>
  );
}

export function ReceptionNotifications() {
  const copy = useCopy();
  return (
    <NotificationCenter
      title={copy.receptionNotificationsTitle}
      shell={(children) => <ReceptionShell active="notifications">{children}</ReceptionShell>}
    />
  );
}

export function ReceptionSettings() {
  const copy = useCopy();
  const router = useRouter();
  const queryClient = useQueryClient();
  const account = useSessionStore((state) => state.account);
  const refreshToken = useSessionStore((state) => state.refreshToken);
  const setActiveSurface = useSessionStore((state) => state.setActiveSurface);
  const multiRole = needsRoleSelection(account);

  const signOut = useMutation({
    mutationFn: async () => {
      if (refreshToken) await logout(refreshToken);
    },
    onSettled: async () => {
      clearSession();
      await queryClient.clear();
      router.replace('/sign-in' as Href);
    },
  });

  return (
    <ModuleFrame active="settings" title={copy.receptionSettingsTitle}>
      <Card style={{ gap: spacing.sm }}>
        <AppText variant="label">{account?.fullName || 'Receptionist'}</AppText>
        <AppText variant="caption">{account?.phoneNumber}</AppText>
      </Card>
      <Button label={copy.profileMenuHelp} variant="secondary" onPress={() => router.push('/help' as Href)} />
      {multiRole ? (
        <Button
          label={copy.switchRole}
          variant="secondary"
          onPress={() => {
            setActiveSurface(null);
            router.push('/select-role' as Href);
          }}
        />
      ) : null}
      <Button
        label={copy.signOut}
        onPress={() =>
          Alert.alert(copy.signOutConfirmTitle, copy.signOutConfirmBody, [
            { text: 'Cancel', style: 'cancel' },
            { text: copy.signOutConfirmAction, style: 'destructive', onPress: () => signOut.mutate() },
          ])
        }
      />
    </ModuleFrame>
  );
}
