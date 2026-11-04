import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useState } from 'react';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { StatusBadge } from '@/components/ui/status-badge';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
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
import { problemMessage } from '@/utils/problem';

function today() {
  return new Date().toISOString().slice(0, 10);
}

export function ReceptionDesk() {
  const copy = useCopy();
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
    mutationFn: () =>
      searchBranchAppointments(activeClinicId, activeBranchId, {
        reference: reference || undefined,
        patientName: patientName || undefined,
        phone: phone || undefined,
        date: today(),
      }),
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
    mutationFn: () => assignClinicStaff(activeClinicId, { phoneNumber: staffPhone, role: 'Receptionist' }),
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

  return (
    <AuthScaffold>
      <Button label={copy.back} variant="ghost" onPress={() => router.back()} />
      <AppText variant="title">{copy.receptionDesk}</AppText>
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
      {(clinic?.branches ?? []).map((item) => (
        <Button
          key={item.id}
          label={item.name}
          variant={activeBranchId === item.id ? 'primary' : 'secondary'}
          onPress={() => setBranchId(item.id)}
        />
      ))}
      <Card elevated>
        <AppText variant="label">{copy.scanOrPaste}</AppText>
        <AppText variant="muted">{copy.cameraUnavailable}</AppText>
        <TextField label={copy.checkInCode} value={qrToken} onChangeText={setQrToken} />
        <Button label={copy.checkInNow} loading={qr.isPending} onPress={() => qr.mutate()} />
      </Card>
      <Card elevated>
        <TextField label={copy.bookingReference} value={reference} onChangeText={setReference} />
        <TextField label={copy.patientName} value={patientName} onChangeText={setPatientName} />
        <TextField label={copy.phoneLabel} value={phone} onChangeText={setPhone} />
        <Button label={copy.search} loading={search.isPending} onPress={() => search.mutate()} />
        {(search.data ?? []).map((item) => (
          <Card key={item.id}>
            <AppText variant="headline">{item.reference ?? item.id}</AppText>
            <StatusBadge label={item.status} tone="primary" />
            <AppText>{item.patientName}</AppText>
            <AppText variant="muted">
              {item.doctorName} · {new Date(item.startsAtUtc).toLocaleString()}
            </AppText>
            <Button label={copy.checkInNow} loading={checkIn.isPending} onPress={() => checkIn.mutate(item.id)} />
          </Card>
        ))}
      </Card>
      <Card>
        <AppText variant="label">{copy.walkIn}</AppText>
        {(doctors.data ?? []).map((item) => (
          <Button
            key={item.id}
            label={item.displayName}
            variant={walkInDoctorId === item.id ? 'primary' : 'secondary'}
            onPress={() => setWalkInDoctorId(item.id)}
          />
        ))}
        <TextField label={copy.patientName} value={walkInName} onChangeText={setWalkInName} />
        <Button label={copy.walkIn} loading={walkIn.isPending} disabled={!walkInDoctorId} onPress={() => walkIn.mutate()} />
        {walkIn.data ? (
          <AppText>
            {walkIn.data.tokenLabel} · {walkIn.data.patientDisplayName}
          </AppText>
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
        <Card>
          <AppText variant="label">{copy.clinicStaff}</AppText>
          <TextField label={copy.staffPhone} value={staffPhone} onChangeText={setStaffPhone} />
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
    </AuthScaffold>
  );
}
