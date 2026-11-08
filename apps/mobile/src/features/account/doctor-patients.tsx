import { useQuery } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useMemo, useState } from 'react';
import { Pressable, ScrollView, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { ClinicSwitcher, useSelectedDoctorClinicId } from '@/components/doctor/clinic-switcher';
import { DoctorShell } from '@/components/layout/doctor-shell';
import { Screen } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { IconChevronRight } from '@/components/ui/prodoc-icons';
import { ProfileAvatar } from '@/components/ui/profile-avatar';
import { SearchBar } from '@/components/ui/search-bar';
import { Segmented } from '@/components/ui/segmented';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { listDoctorClinics, listDoctorPatients } from '@/services/api/doctor-portal-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

type PatientFilter = 'all' | 'recent' | 'followup';

export function DoctorPatientsScreen() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState<PatientFilter>('all');
  const clinics = useQuery({ queryKey: ['doctor', 'clinics'], queryFn: listDoctorClinics });
  const clinicId = useSelectedDoctorClinicId(clinics.data);
  const patients = useQuery({
    queryKey: ['doctor', 'patients', clinicId ?? 'all'],
    queryFn: () => listDoctorPatients(clinicId),
  });

  const rows = useMemo(() => {
    const q = search.trim().toLowerCase();
    const now = Date.now();
    return (patients.data ?? []).filter((patient) => {
      if (filter === 'recent') {
        if (!patient.lastVisitAtUtc) return false;
        const age = now - new Date(patient.lastVisitAtUtc).getTime();
        if (age > 1000 * 60 * 60 * 24 * 30) return false;
      }
      if (filter === 'followup') {
        if (!['Completed', 'CheckedIn', 'Confirmed'].includes(patient.lastStatus ?? '')) return false;
      }
      if (!q) return true;
      return patient.patientName.toLowerCase().includes(q);
    });
  }, [filter, patients.data, search]);

  return (
    <DoctorShell active="patients">
      <Screen>
        <ScrollView
          contentContainerStyle={{
            gap: spacing.md,
            paddingBottom: spacing.xxl,
            paddingTop: Math.max(insets.top - 8, 0),
          }}>
          <ScreenHeader title={copy.doctorPatientsTitle} />
          <ClinicSwitcher
            clinics={clinics.data ?? []}
          />
          <SearchBar
            placeholder={copy.doctorSearchPatients}
            value={search}
            onChangeText={setSearch}
            onClear={() => setSearch('')}
          />
          <Segmented
            value={filter}
            onChange={setFilter}
            options={[
              { value: 'all', label: copy.doctorFilterAll },
              { value: 'recent', label: copy.doctorPatientsRecent },
              { value: 'followup', label: copy.doctorPatientsFollowUp },
            ]}
          />

          {rows.map((patient) => (
            <Pressable
              key={`${patient.patientName}-${patient.lastAppointmentId}`}
              onPress={() => {
                if (patient.lastAppointmentId) {
                  router.push(`/doctor-patient/${patient.lastAppointmentId}` as Href);
                }
              }}
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
              <ProfileAvatar name={patient.patientName} size={48} />
              <View style={{ flex: 1, gap: 2 }}>
                <AppText variant="label" numberOfLines={1}>
                  {patient.patientName}
                </AppText>
                <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                  {copy.doctorKpiPatients}: {patient.visitCount}
                  {patient.lastStatus ? ` · ${patient.lastStatus}` : ''}
                </AppText>
              </View>
              <IconChevronRight size={18} color={theme.colors.textMuted} />
            </Pressable>
          ))}

          {rows.length === 0 ? <AppText variant="muted">{copy.doctorPatientsEmpty}</AppText> : null}
          {patients.error ? <AppText variant="muted">{problemMessage(patients.error, copy.doctorPatientsTitle)}</AppText> : null}
        </ScrollView>
      </Screen>
    </DoctorShell>
  );
}
