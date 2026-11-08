import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { View } from 'react-native';

import { ClinicSwitcher, useSelectedDoctorClinicId } from '@/components/doctor/clinic-switcher';
import { DoctorShell } from '@/components/layout/doctor-shell';
import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { StatusBadge } from '@/components/ui/status-badge';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { listDoctorClinics, listDoctorPrescriptions } from '@/services/api/doctor-portal-api';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

export function DoctorPrescriptionsScreen() {
  const copy = useCopy();
  const router = useRouter();
  const queryClient = useQueryClient();
  const clinics = useQuery({ queryKey: ['doctor', 'clinics'], queryFn: listDoctorClinics });
  const clinicId = useSelectedDoctorClinicId(clinics.data);
  const rows = useQuery({
    queryKey: ['doctor', 'prescriptions', clinicId ?? 'all'],
    queryFn: () => listDoctorPrescriptions(clinicId),
  });

  return (
    <DoctorShell active="prescriptions">
      <AuthScaffold>
        <ScreenHeader title={copy.doctorPrescriptionsTitle} />
        <ClinicSwitcher
          clinics={clinics.data ?? []}
          onChanged={() => void queryClient.invalidateQueries({ queryKey: ['doctor', 'prescriptions'] })}
        />
        {(rows.data ?? []).map((item) => (
          <Card key={`${item.prescriptionId}-${item.appointmentId}`} style={{ gap: spacing.sm }}>
            <View style={{ flexDirection: 'row', justifyContent: 'space-between', gap: spacing.sm }}>
              <AppText variant="label">{item.prescriptionNumber ?? item.appointmentReference ?? 'Rx'}</AppText>
              {item.prescriptionStatus ? <StatusBadge label={item.prescriptionStatus} tone="primary" /> : null}
            </View>
            <AppText>{item.patientName}</AppText>
            {item.diagnosis ? <AppText variant="muted">{item.diagnosis}</AppText> : null}
            <AppText variant="muted">
              {item.clinicName} · {new Date(item.startsAtUtc).toLocaleString()}
            </AppText>
            <Button
              label={copy.openConsultation}
              onPress={() => router.push(`/consultation/${item.appointmentId}` as Href)}
            />
            {item.prescriptionId ? (
              <Button
                label={copy.prescriptionDetails}
                variant="secondary"
                onPress={() => router.push(`/prescription/${item.prescriptionId}` as Href)}
              />
            ) : null}
          </Card>
        ))}
        {rows.data?.length === 0 ? <AppText variant="muted">{copy.doctorPrescriptionsEmpty}</AppText> : null}
        {rows.error ? <AppText variant="muted">{problemMessage(rows.error, copy.doctorPrescriptionsTitle)}</AppText> : null}
      </AuthScaffold>
    </DoctorShell>
  );
}
