import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useMemo, useState } from 'react';
import { Platform, ScrollView, View } from 'react-native';
import Animated, { FadeInDown } from 'react-native-reanimated';

import { AppShell } from '@/components/layout/app-shell';
import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ChartCard, HealthMetricCard } from '@/components/ui/health-metric-card';
import { EmptyState } from '@/components/ui/empty-state';
import {
  IconHealthBp,
  IconHealthDiabetes,
  IconHealthRecords,
  IconSpecialtyHeart,
} from '@/components/ui/prodoc-icons';
import { SectionHeader } from '@/components/ui/section-header';
import { CardSkeleton } from '@/components/ui/skeleton';
import { StatusBadge } from '@/components/ui/status-badge';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { getMedicalRecord, listMyMedicalRecords, uploadMedicalDocument } from '@/services/api/clinical-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { duration, spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

export function HealthRecordsScreen() {
  const copy = useCopy();
  const theme = useProdocTheme();
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
  const enter = theme.reduceMotion ? undefined : FadeInDown.duration(duration.enter);

  const chartRows = useMemo(() => {
    if (!detail.data) return [];
    return [
      { label: copy.allergies, value: detail.data.allergies.length },
      { label: copy.homeConditions, value: detail.data.conditions.length },
      { label: copy.homeMedications, value: detail.data.medications.length },
      { label: copy.homeConsultations, value: detail.data.consultations.length },
      { label: copy.prescriptions, value: detail.data.prescriptions.length },
      { label: copy.medicalDocuments, value: detail.data.documents.length },
    ].filter((row) => row.value > 0);
  }, [copy, detail.data]);

  return (
    <AppShell active="records">
      <AuthScaffold>
        <ScreenHeader title={copy.healthRecords} />
        {list.isLoading ? <CardSkeleton /> : null}
        {!list.isLoading && (list.data ?? []).length === 0 ? (
          <EmptyState
            title={copy.healthRecords}
            body={copy.noneRecorded}
            icon={<IconHealthRecords size={28} color={theme.colors.primary} />}
          />
        ) : null}

        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: spacing.sm }}>
          {(list.data ?? []).map((item) => (
            <Button
              key={item.id}
              label={`${item.subjectName} · ${item.prescriptionCount} Rx`}
              variant={activeId === item.id ? 'primary' : 'secondary'}
              onPress={() => setSelectedId(item.id)}
            />
          ))}
        </ScrollView>

        {detail.data ? (
          <Animated.View entering={enter} style={{ gap: spacing.lg }}>
            <Card elevated style={{ gap: spacing.sm }}>
              <AppText variant="title">{detail.data.subjectName}</AppText>
              <AppText variant="muted">
                {copy.allergies}:{' '}
                {detail.data.allergies.map((item) => item.substance).join(', ') || copy.noneRecorded}
              </AppText>
            </Card>

            <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md }}>
              <HealthMetricCard
                label={copy.allergies}
                value={detail.data.allergies.length}
                icon={<IconHealthBp size={20} color={theme.colors.primary} />}
              />
              <HealthMetricCard
                label={copy.prescriptions}
                value={detail.data.prescriptions.length}
                icon={<IconHealthDiabetes size={20} color={theme.colors.primary} />}
              />
              <HealthMetricCard
                label={copy.homeConsultations}
                value={detail.data.consultations.length}
                icon={<IconSpecialtyHeart size={20} color={theme.colors.primary} />}
              />
              <HealthMetricCard
                label={copy.medicalDocuments}
                value={detail.data.documents.length}
                icon={<IconHealthRecords size={20} color={theme.colors.primary} />}
              />
            </View>

            {chartRows.length > 0 ? <ChartCard title={copy.healthHistory} rows={chartRows} /> : null}

            <SectionHeader title={copy.consultationHistory} />
            {detail.data.consultations.length === 0 ? (
              <AppText variant="muted">{copy.noneRecorded}</AppText>
            ) : (
              detail.data.consultations.map((item) => (
                <Card key={item.id} elevated style={{ gap: spacing.sm }}>
                  <View style={{ flexDirection: 'row', justifyContent: 'space-between', gap: spacing.sm }}>
                    <AppText variant="label" style={{ flex: 1 }}>
                      {item.doctorName}
                    </AppText>
                    <StatusBadge label={item.status} tone="primary" />
                  </View>
                  <AppText variant="muted">{item.clinicName}</AppText>
                  <AppText variant="muted">{new Date(item.startedAtUtc).toLocaleString()}</AppText>
                  <AppText>{item.diagnoses.join(', ') || item.status}</AppText>
                </Card>
              ))
            )}

            <SectionHeader title={copy.prescriptionHistory} />
            {detail.data.prescriptions.map((item) => (
              <Button
                key={item.id}
                label={`${item.number} · ${item.status}`}
                variant="secondary"
                onPress={() => router.push(`/prescription/${item.id}` as Href)}
              />
            ))}

            <SectionHeader title={copy.medicalDocuments} />
            {detail.data.documents.map((item) => (
              <Card key={item.id} style={{ gap: spacing.xs, padding: spacing.md }}>
                <AppText variant="label">{item.title}</AppText>
                <AppText variant="muted">{item.fileName}</AppText>
              </Card>
            ))}

            <Button label={copy.uploadReport} loading={upload.isPending} onPress={() => upload.mutate()} />
          </Animated.View>
        ) : null}
        {error ? <AppText variant="muted">{problemMessage(error, copy.healthRecords)}</AppText> : null}
      </AuthScaffold>
    </AppShell>
  );
}
