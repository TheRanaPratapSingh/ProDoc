import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { Platform } from 'react-native';

import { AppShell } from '@/components/layout/app-shell';
import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { getMedicalRecord, listMyMedicalRecords, uploadMedicalDocument } from '@/services/api/clinical-api';
import { problemMessage } from '@/utils/problem';
import { useState } from 'react';

export function HealthRecordsScreen() {
  const copy = useCopy();
  const router = useRouter();
  const queryClient = useQueryClient();
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const list = useQuery({ queryKey: ['medical-records'], queryFn: listMyMedicalRecords });
  const activeId = selectedId ?? list.data?.[0]?.id ?? null;
  const detail = useQuery({
    queryKey: ['medical-record', activeId],
    queryFn: () => getMedicalRecord(activeId!),
    enabled: Boolean(activeId),
  });
  const upload = useMutation({
    mutationFn: async () => {
      if (!activeId || Platform.OS !== 'web' || typeof document === 'undefined') {
        throw new Error(copy.uploadWebOnly);
      }
      const input = document.createElement('input');
      input.type = 'file';
      input.accept = 'application/pdf,image/jpeg,image/png,image/webp';
      const file = await new Promise<File>((resolve, reject) => {
        input.onchange = () => {
          const chosen = input.files?.[0];
          if (!chosen) {
            reject(new Error(copy.cancel));
            return;
          }
          resolve(chosen);
        };
        input.click();
      });
      return uploadMedicalDocument(activeId, file, file.name, file.name);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['medical-record', activeId] });
      await queryClient.invalidateQueries({ queryKey: ['medical-records'] });
    },
  });
  const error = list.error ?? detail.error ?? upload.error;

  return (
    <AppShell active="records">
    <AuthScaffold>
      <Button label={copy.back} variant="ghost" onPress={() => router.back()} />
      <AppText variant="title">{copy.healthRecords}</AppText>
      {(list.data ?? []).map((item) => (
        <Button
          key={item.id}
          label={`${item.subjectName} · ${item.prescriptionCount} Rx`}
          variant={activeId === item.id ? 'primary' : 'secondary'}
          onPress={() => setSelectedId(item.id)}
        />
      ))}
      {detail.data ? (
        <Card>
          <AppText variant="title">{detail.data.subjectName}</AppText>
          <AppText>
            {copy.allergies}: {detail.data.allergies.map((item) => item.substance).join(', ') || copy.noneRecorded}
          </AppText>
          <AppText variant="title">{copy.consultationHistory}</AppText>
          {detail.data.consultations.map((item) => (
            <Card key={item.id}>
              <AppText>
                {item.doctorName} · {item.clinicName}
              </AppText>
              <AppText variant="muted">{new Date(item.startedAtUtc).toLocaleString()}</AppText>
              <AppText>{item.diagnoses.join(', ') || item.status}</AppText>
            </Card>
          ))}
          <AppText variant="title">{copy.prescriptionHistory}</AppText>
          {detail.data.prescriptions.map((item) => (
            <Button
              key={item.id}
              label={`${item.number} · ${item.status}`}
              variant="secondary"
              onPress={() => router.push(`/prescription/${item.id}` as Href)}
            />
          ))}
          <AppText variant="title">{copy.medicalDocuments}</AppText>
          {detail.data.documents.map((item) => (
            <AppText key={item.id}>
              {item.title} · {item.fileName}
            </AppText>
          ))}
          <Button label={copy.uploadReport} loading={upload.isPending} onPress={() => upload.mutate()} />
        </Card>
      ) : null}
      {error ? <AppText variant="muted">{problemMessage(error, copy.healthRecords)}</AppText> : null}
    </AuthScaffold>
    </AppShell>
  );
}
