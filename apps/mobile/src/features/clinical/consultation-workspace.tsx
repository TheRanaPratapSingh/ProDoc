import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useLocalSearchParams, useRouter, type Href } from 'expo-router';
import { useEffect, useState } from 'react';
import { View } from 'react-native';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import {
  createPrescription,
  finalizePrescription,
  openWorkspace,
  startConsultation,
  updateConsultation,
  updatePrescription,
} from '@/services/api/clinical-api';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

type MedicineDraft = {
  medicineName: string;
  dosage: string;
  frequency: string;
  duration: string;
  route: string;
  instructions: string;
};

export function ConsultationWorkspaceScreen() {
  const copy = useCopy();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { appointmentId } = useLocalSearchParams<{ appointmentId: string }>();
  const workspace = useQuery({
    queryKey: ['workspace', appointmentId],
    queryFn: () => openWorkspace(appointmentId!),
    enabled: Boolean(appointmentId),
  });
  const [symptoms, setSymptoms] = useState('');
  const [observations, setObservations] = useState('');
  const [notes, setNotes] = useState('');
  const [advice, setAdvice] = useState('');
  const [diagnosis, setDiagnosis] = useState('');
  const [rxDiagnosis, setRxDiagnosis] = useState('');
  const [medicines, setMedicines] = useState<MedicineDraft[]>([
    { medicineName: '', dosage: '', frequency: '', duration: '', route: 'Oral', instructions: '' },
  ]);
  const [prescriptionId, setPrescriptionId] = useState<string | null>(null);

  useEffect(() => {
    const active = workspace.data?.activeConsultation;
    if (!active) {
      return;
    }
    setSymptoms(active.symptoms ?? '');
    setObservations(active.clinicalObservations ?? '');
    setNotes(active.doctorNotes ?? '');
    setAdvice(active.advice ?? '');
    setDiagnosis(active.diagnoses[0]?.description ?? '');
  }, [workspace.data?.activeConsultation]);

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['workspace', appointmentId] });
  };

  const start = useMutation({
    mutationFn: () => startConsultation(appointmentId!),
    onSuccess: refresh,
  });
  const saveConsult = useMutation({
    mutationFn: (complete: boolean) =>
      updateConsultation(workspace.data!.activeConsultation!.id, {
        symptoms,
        clinicalObservations: observations,
        doctorNotes: notes,
        advice,
        diagnoses: diagnosis.trim() ? [{ description: diagnosis.trim(), isPrimary: true }] : [],
        complete,
      }),
    onSuccess: refresh,
  });
  const draftRx = useMutation({
    mutationFn: async () => {
      const created = await createPrescription(workspace.data!.activeConsultation!.id);
      setPrescriptionId(created.id);
      return created;
    },
  });
  const saveRx = useMutation({
    mutationFn: async () => {
      const id = prescriptionId ?? (await createPrescription(workspace.data!.activeConsultation!.id)).id;
      setPrescriptionId(id);
      return updatePrescription(id, {
        diagnosis: rxDiagnosis || diagnosis,
        symptoms,
        clinicalObservations: observations,
        advice,
        medicines: medicines
          .filter((item) => item.medicineName.trim())
          .map((item) => ({
            medicineName: item.medicineName,
            dosage: item.dosage,
            frequency: item.frequency,
            duration: item.duration,
            route: item.route,
            instructions: item.instructions || undefined,
          })),
      });
    },
  });
  const finalize = useMutation({
    mutationFn: async () => {
      const saved = await saveRx.mutateAsync();
      return finalizePrescription(saved.id);
    },
    onSuccess: async (rx) => {
      await refresh();
      router.push(`/prescription/${rx.id}` as Href);
    },
  });

  const record = workspace.data?.record;
  const error =
    workspace.error ?? start.error ?? saveConsult.error ?? draftRx.error ?? saveRx.error ?? finalize.error;

  return (
    <AuthScaffold>
      <Button label={copy.back} variant="ghost" onPress={() => router.back()} />
      <AppText variant="title">{copy.consultationWorkspace}</AppText>
      {workspace.data ? (
        <Card>
          <AppText variant="title">{workspace.data.patientName}</AppText>
          <AppText variant="muted">{workspace.data.appointmentStatus}</AppText>
          {workspace.data.appointmentReason ? <AppText>{workspace.data.appointmentReason}</AppText> : null}
        </Card>
      ) : null}
      {record ? (
        <Card>
          <AppText variant="title">{copy.medicalHistory}</AppText>
          <AppText>
            {copy.allergies}: {record.allergies.filter((item) => item.isActive).map((item) => item.substance).join(', ') || copy.noneRecorded}
          </AppText>
          <AppText>
            {copy.conditions}: {record.conditions.filter((item) => item.isActive).map((item) => item.name).join(', ') || copy.noneRecorded}
          </AppText>
          <AppText>
            {copy.medications}: {record.medications.filter((item) => item.isCurrent).map((item) => item.name).join(', ') || copy.noneRecorded}
          </AppText>
          {record.prescriptions.slice(0, 3).map((item) => (
            <Button
              key={item.id}
              label={`${item.number} · ${item.status}`}
              variant="secondary"
              onPress={() => router.push(`/prescription/${item.id}` as Href)}
            />
          ))}
        </Card>
      ) : null}
      {!workspace.data?.activeConsultation ? (
        <Button label={copy.startConsultation} loading={start.isPending} onPress={() => start.mutate()} />
      ) : (
        <View style={{ gap: spacing.md }}>
          <Card>
            <AppText variant="title">{copy.consultationNotes}</AppText>
            <TextField label={copy.clinicalSymptoms} value={symptoms} onChangeText={setSymptoms} />
            <TextField label={copy.clinicalObservations} value={observations} onChangeText={setObservations} />
            <TextField label={copy.doctorNotes} value={notes} onChangeText={setNotes} />
            <TextField label={copy.advice} value={advice} onChangeText={setAdvice} />
            <TextField label={copy.diagnosis} value={diagnosis} onChangeText={setDiagnosis} />
            <Button label={copy.saveDraft} variant="secondary" loading={saveConsult.isPending} onPress={() => saveConsult.mutate(false)} />
            <Button label={copy.completeConsultation} loading={saveConsult.isPending} onPress={() => saveConsult.mutate(true)} />
          </Card>
          <Card>
            <AppText variant="title">{copy.prescriptionBuilder}</AppText>
            <TextField label={copy.diagnosis} value={rxDiagnosis || diagnosis} onChangeText={setRxDiagnosis} />
            {medicines.map((medicine, index) => (
              <View key={`med-${index}`} style={{ gap: spacing.sm }}>
                <TextField
                  label={copy.medicineName}
                  value={medicine.medicineName}
                  onChangeText={(value) =>
                    setMedicines((rows) => rows.map((row, i) => (i === index ? { ...row, medicineName: value } : row)))
                  }
                />
                <TextField
                  label={copy.dosage}
                  value={medicine.dosage}
                  onChangeText={(value) => setMedicines((rows) => rows.map((row, i) => (i === index ? { ...row, dosage: value } : row)))}
                />
                <TextField
                  label={copy.frequency}
                  value={medicine.frequency}
                  onChangeText={(value) => setMedicines((rows) => rows.map((row, i) => (i === index ? { ...row, frequency: value } : row)))}
                />
                <TextField
                  label={copy.duration}
                  value={medicine.duration}
                  onChangeText={(value) => setMedicines((rows) => rows.map((row, i) => (i === index ? { ...row, duration: value } : row)))}
                />
                <TextField
                  label={copy.route}
                  value={medicine.route}
                  onChangeText={(value) => setMedicines((rows) => rows.map((row, i) => (i === index ? { ...row, route: value } : row)))}
                />
                <TextField
                  label={copy.instructions}
                  value={medicine.instructions}
                  onChangeText={(value) =>
                    setMedicines((rows) => rows.map((row, i) => (i === index ? { ...row, instructions: value } : row)))
                  }
                />
              </View>
            ))}
            <Button
              label={copy.addMedicine}
              variant="secondary"
              onPress={() =>
                setMedicines((rows) => [
                  ...rows,
                  { medicineName: '', dosage: '', frequency: '', duration: '', route: 'Oral', instructions: '' },
                ])
              }
            />
            <Button label={copy.saveDraft} variant="secondary" loading={saveRx.isPending || draftRx.isPending} onPress={() => saveRx.mutate()} />
            <Button label={copy.finalizePrescription} loading={finalize.isPending} onPress={() => finalize.mutate()} />
          </Card>
        </View>
      )}
      {error ? <AppText variant="muted">{problemMessage(error, copy.consultationWorkspace)}</AppText> : null}
    </AuthScaffold>
  );
}
