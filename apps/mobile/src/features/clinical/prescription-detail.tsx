import { useMutation, useQuery } from '@tanstack/react-query';
import { useLocalSearchParams, useRouter, type Href } from 'expo-router';
import { Platform } from 'react-native';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { amendPrescription, downloadPrescriptionPdf, getPrescription } from '@/services/api/clinical-api';
import { problemMessage } from '@/utils/problem';
import { useAccountPermissions } from '@/stores/session-store';
import { useState } from 'react';
import { TextField } from '@/components/ui/text-field';

export function PrescriptionDetailScreen() {
  const copy = useCopy();
  const router = useRouter();
  const { id } = useLocalSearchParams<{ id: string }>();
  const permissions = useAccountPermissions();
  const canAmend = permissions.includes('prescriptions.publish');
  const [reason, setReason] = useState('');
  const prescription = useQuery({
    queryKey: ['prescription', id],
    queryFn: () => getPrescription(id!),
    enabled: Boolean(id),
  });
  const download = useMutation({
    mutationFn: async () => {
      const blob = await downloadPrescriptionPdf(id!);
      if (Platform.OS === 'web' && typeof window !== 'undefined') {
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = `${prescription.data?.number ?? 'prescription'}.pdf`;
        anchor.click();
        URL.revokeObjectURL(url);
      }
    },
  });
  const amend = useMutation({
    mutationFn: () => amendPrescription(id!, reason),
    onSuccess: (rx) => {
      router.replace(`/prescription/${rx.id}` as Href);
    },
  });
  const current = prescription.data?.current;
  const error = prescription.error ?? download.error ?? amend.error;

  return (
    <AuthScaffold>
      <ScreenHeader title={copy.prescriptionDetails} />
      {prescription.data && current ? (
        <Card>
          <AppText variant="title">
            {prescription.data.number} · v{current.versionNumber}
          </AppText>
          <AppText variant="muted">{prescription.data.status}</AppText>
          <AppText>
            {copy.patient}: {current.patientName}
          </AppText>
          <AppText>
            {copy.doctor}: {current.doctorName}
          </AppText>
          <AppText>
            {copy.clinic}: {current.clinicName} — {current.branchName}
          </AppText>
          <AppText>
            {copy.diagnosis}: {current.diagnosis}
          </AppText>
          {current.medicines.map((item) => (
            <AppText key={item.id}>
              {item.medicineName} · {item.dosage} · {item.frequency} · {item.duration} · {item.route}
            </AppText>
          ))}
          {current.advice ? (
            <AppText>
              {copy.advice}: {current.advice}
            </AppText>
          ) : null}
          {current.hasPdf ? <Button label={copy.downloadPdf} loading={download.isPending} onPress={() => download.mutate()} /> : null}
          {canAmend && prescription.data.status !== 'Draft' ? (
            <>
              <TextField label={copy.amendmentReason} value={reason} onChangeText={setReason} />
              <Button label={copy.amendPrescription} loading={amend.isPending} onPress={() => amend.mutate()} />
            </>
          ) : null}
        </Card>
      ) : null}
      {error ? <AppText variant="muted">{problemMessage(error, copy.prescriptionDetails)}</AppText> : null}
    </AuthScaffold>
  );
}
