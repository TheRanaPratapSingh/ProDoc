import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useMemo, useState } from 'react';
import { ScrollView, View } from 'react-native';
import Animated, { FadeInDown } from 'react-native-reanimated';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { PatientCard } from '@/components/ui/patient-card';
import { PhoneInput } from '@/components/ui/phone-input';
import { SectionHeader } from '@/components/ui/section-header';
import { DashboardStatCard } from '@/components/ui/stat-card';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import { indianMobileOptionalSchema, indianMobileSchema } from '@/schemas/common';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { duration, spacing } from '@/theme/tokens';
import {
  assignClinicStaff,
  checkInAppointment,
  checkInByQr,
  getQueueSettings,
  listClinicStaff,
  listDeskClinics,
  listDeskDoctors,
  registerDeskWalkIn,
  saveQueueSettings,
  searchBranchAppointments,
} from '@/services/api/queue-api';
import { useSessionStore } from '@/stores/session-store';
import { normalizeIndianMobile } from '@/utils/phone';
import { problemMessage } from '@/utils/problem';

function today() {
  return new Date().toISOString().slice(0, 10);
}

export function ReceptionDesk() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const queryClient = useQueryClient();
  const canManageStaff = useSessionStore((state) => state.account?.permissions.includes('staff.manage') ?? false);
  const clinics = useQuery({ queryKey: ['clinics', 'desk'], queryFn: listDeskClinics });
  const [clinicId, setClinicId] = useState('');
  const [branchId, setBranchId] = useState('');
  const clinic = (clinics.data ?? []).find((item) => item.id === clinicId) ?? clinics.data?.[0];
  const branch = clinic?.branches.find((item) => item.id === branchId) ?? clinic?.branches[0];
  const activeClinicId = clinic?.id ?? '';
  const activeBranchId = branch?.id ?? '';
  const doctors = useQuery({
    queryKey: ['desk', 'doctors', activeClinicId, activeBranchId],
    queryFn: () => listDeskDoctors(activeClinicId, activeBranchId),
    enabled: Boolean(activeClinicId && activeBranchId),
  });
  const [reference, setReference] = useState('');
  const [patientName, setPatientName] = useState('');
  const [phone, setPhone] = useState('');
  const [qrToken, setQrToken] = useState('');
  const [walkInName, setWalkInName] = useState('');
  const [walkInDoctorId, setWalkInDoctorId] = useState('');
  const [staffPhone, setStaffPhone] = useState('');
  const [prefix, setPrefix] = useState('A');
  const settings = useQuery({
    queryKey: ['queue', 'settings', activeClinicId, activeBranchId],
    queryFn: () => getQueueSettings(activeClinicId, activeBranchId),
    enabled: Boolean(activeClinicId && activeBranchId && canManageStaff),
  });
  const staff = useQuery({
    queryKey: ['clinic', 'staff', activeClinicId],
    queryFn: () => listClinicStaff(activeClinicId),
    enabled: Boolean(activeClinicId && canManageStaff),
  });
  const search = useMutation({
    mutationFn: () => {
      const phoneCheck = indianMobileOptionalSchema.safeParse(phone);
      if (!phoneCheck.success) {
        throw new Error(phoneCheck.error.issues[0]?.message ?? 'Enter a valid Indian mobile number.');
      }
      const normalizedPhone = phone.trim() ? normalizeIndianMobile(phone) : null;
      if (phone.trim() && !normalizedPhone) {
        throw new Error('Enter a valid Indian mobile number.');
      }
      return searchBranchAppointments(activeClinicId, activeBranchId, {
        reference: reference || undefined,
        patientName: patientName || undefined,
        phone: normalizedPhone ?? undefined,
        date: today(),
      });
    },
  });
  const checkIn = useMutation({
    mutationFn: (appointmentId: string) => checkInAppointment(appointmentId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['queue'] });
    },
  });
  const qr = useMutation({
    mutationFn: () => checkInByQr(qrToken.trim()),
    onSuccess: () => setQrToken(''),
  });
  const walkIn = useMutation({
    mutationFn: () =>
      registerDeskWalkIn(activeClinicId, activeBranchId, {
        doctorId: walkInDoctorId,
        patientName: walkInName.trim(),
      }),
    onSuccess: () => setWalkInName(''),
  });
  const assign = useMutation({
    mutationFn: () => {
      const parsed = indianMobileSchema.safeParse(staffPhone);
      if (!parsed.success) {
        throw new Error(parsed.error.issues[0]?.message ?? 'Enter a valid Indian mobile number.');
      }
      const phoneNumber = normalizeIndianMobile(parsed.data);
      if (!phoneNumber) {
        throw new Error('Enter a valid Indian mobile number.');
      }
      return assignClinicStaff(activeClinicId, { phoneNumber, role: 'Receptionist' });
    },
    onSuccess: async () => {
      setStaffPhone('');
      await queryClient.invalidateQueries({ queryKey: ['clinic', 'staff', activeClinicId] });
    },
  });
  const saveSettings = useMutation({
    mutationFn: () =>
      saveQueueSettings(activeClinicId, activeBranchId, {
        tokenPrefix: prefix || settings.data?.tokenPrefix || 'A',
        scheduledPatientPriority: settings.data?.scheduledPatientPriority ?? true,
        earlyCheckInMinutes: settings.data?.earlyCheckInMinutes ?? 60,
        lateCheckInMinutes: settings.data?.lateCheckInMinutes ?? 120,
        allowWalkIns: settings.data?.allowWalkIns ?? true,
        allowPriorityPatients: settings.data?.allowPriorityPatients ?? false,
      }),
  });
  const error =
    search.error ?? checkIn.error ?? qr.error ?? walkIn.error ?? assign.error ?? saveSettings.error ?? clinics.error;

  const checkedInCount = useMemo(
    () => (search.data ?? []).filter((item) => item.status === 'CheckedIn').length,
    [search.data],
  );
  const enter = theme.reduceMotion ? undefined : FadeInDown.duration(duration.enter);

  return (
    <AuthScaffold>
      <ScreenHeader title={copy.receptionDesk} />
      <Animated.View entering={enter} style={{ gap: spacing.lg }}>
        <SectionHeader title={copy.clinics} />
        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: spacing.sm }}>
          {(clinics.data ?? []).map((item) => (
            <Button
              key={item.id}
              label={item.name}
              variant={activeClinicId === item.id ? 'primary' : 'secondary'}
              onPress={() => {
                setClinicId(item.id);
                setBranchId(item.branches[0]?.id ?? '');
              }}
            />
          ))}
        </ScrollView>
        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: spacing.sm }}>
          {(clinic?.branches ?? []).map((item) => (
            <Button
              key={item.id}
              label={item.name}
              variant={activeBranchId === item.id ? 'primary' : 'secondary'}
              onPress={() => setBranchId(item.id)}
            />
          ))}
        </ScrollView>

        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md }}>
          <DashboardStatCard label={copy.todaysAppointments} value={search.data?.length ?? '—'} />
          <DashboardStatCard label={copy.checkedInCount} value={search.data ? checkedInCount : '—'} />
          <DashboardStatCard label={copy.walkIn} value={walkIn.data ? 1 : 0} />
          <DashboardStatCard label={copy.doctors} value={(doctors.data ?? []).length} />
        </View>

        <Card elevated>
          <SectionHeader title={copy.manageQueue} />
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
            <Button label={copy.bookAppointment} variant="secondary" onPress={() => router.push('/doctors' as Href)} />
            {activeClinicId && activeBranchId && walkInDoctorId ? (
              <Button
                label={copy.waitingRoom}
                variant="secondary"
                onPress={() => router.push(`/display/${activeClinicId}/${activeBranchId}/${walkInDoctorId}` as Href)}
              />
            ) : null}
          </View>
        </Card>

        <Card elevated>
          <SectionHeader title={copy.scanOrPaste} />
          <AppText variant="muted">{copy.cameraUnavailable}</AppText>
          <TextField label={copy.checkInCode} value={qrToken} onChangeText={setQrToken} />
          <Button label={copy.checkInNow} loading={qr.isPending} onPress={() => qr.mutate()} />
          {qr.isSuccess && qr.data ? (
            <AppText style={{ color: theme.colors.success }}>
              {copy.checkedIn}: {qr.data.tokenLabel} · {qr.data.displayName}
            </AppText>
          ) : null}
          {qr.error ? <AppText variant="muted">{problemMessage(qr.error, copy.checkInCode)}</AppText> : null}
        </Card>

        <Card elevated>
          <SectionHeader title={copy.todaysAppointments} />
          <TextField label={copy.bookingReference} value={reference} onChangeText={setReference} />
          <TextField label={copy.patientName} value={patientName} onChangeText={setPatientName} />
          <PhoneInput label={copy.phoneLabel} value={phone} onChangeText={setPhone} />
          <Button label={copy.search} loading={search.isPending} onPress={() => search.mutate()} />
          {(search.data ?? []).map((item) => (
            <PatientCard
              key={item.id}
              name={item.patientName}
              meta={[
                item.tokenLabel ? `${copy.tokenNumber} ${item.tokenLabel}` : null,
                item.doctorName,
                new Date(item.startsAtUtc).toLocaleString(undefined, {
                  day: 'numeric',
                  month: 'short',
                  hour: 'numeric',
                  minute: '2-digit',
                }),
              ]
                .filter(Boolean)
                .join(' · ')}
              status={item.status}
              primaryAction={{
                label: copy.checkInNow,
                loading: checkIn.isPending,
                onPress: () => checkIn.mutate(item.id),
              }}
            />
          ))}
        </Card>

        <Card elevated>
          <SectionHeader title={copy.walkIn} />
          <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: spacing.sm }}>
            {(doctors.data ?? []).map((item) => (
              <Button
                key={item.id}
                label={item.displayName}
                variant={walkInDoctorId === item.id ? 'primary' : 'secondary'}
                onPress={() => setWalkInDoctorId(item.id)}
              />
            ))}
          </ScrollView>
          <TextField label={copy.patientName} value={walkInName} onChangeText={setWalkInName} />
          <Button label={copy.walkIn} loading={walkIn.isPending} disabled={!walkInDoctorId} onPress={() => walkIn.mutate()} />
          {walkIn.data ? (
            <PatientCard
              name={walkIn.data.patientDisplayName}
              meta={walkIn.data.tokenLabel}
              status={copy.walkIn}
              statusTone="success"
            />
          ) : null}
          {activeClinicId && activeBranchId && walkInDoctorId ? (
            <Button
              label={copy.waitingRoom}
              variant="secondary"
              onPress={() => router.push(`/display/${activeClinicId}/${activeBranchId}/${walkInDoctorId}` as Href)}
            />
          ) : null}
        </Card>

        {canManageStaff ? (
          <Card elevated>
            <SectionHeader title={copy.clinicStaff} />
            <PhoneInput label={copy.staffPhone} value={staffPhone} onChangeText={setStaffPhone} />
            <Button label={copy.assignStaff} loading={assign.isPending} onPress={() => assign.mutate()} />
            {(staff.data ?? []).map((member) => (
              <AppText key={member.id} variant="muted">
                {member.phoneNumber ?? member.userId} · {member.role}
                {member.isActive ? '' : ' (inactive)'}
              </AppText>
            ))}
            <AppText variant="label">{copy.queueSettings}</AppText>
            <TextField label={copy.tokenPrefix} value={prefix || settings.data?.tokenPrefix || 'A'} onChangeText={setPrefix} />
            <Button label={copy.save} loading={saveSettings.isPending} onPress={() => saveSettings.mutate()} />
          </Card>
        ) : null}
        {error ? <AppText variant="muted">{problemMessage(error, copy.receptionDesk)}</AppText> : null}
      </Animated.View>
    </AuthScaffold>
  );
}
