import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useLocalSearchParams, useRouter, type Href } from 'expo-router';
import { useEffect, useState } from 'react';
import { Pressable, ScrollView, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Screen } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { ProfileAvatar } from '@/components/ui/profile-avatar';
import { Segmented } from '@/components/ui/segmented';
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
import { addDaysIso, createFollowUp } from '@/services/api/follow-up-api';
import { clinicDateIso } from '@/features/providers/schedule-day';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { notifySuccess } from '@/stores/feedback-store';
import { notifyApiError, problemMessage } from '@/utils/problem';

type FollowUpPreset = 'none' | '3' | '7' | '15' | '30' | 'custom';

type MedicineDraft = {
  medicineName: string;
  dosage: string;
  frequency: string;
  duration: string;
  route: string;
  instructions: string;
};

type Tab = 'consultation' | 'prescription';

export function ConsultationWorkspaceScreen() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const queryClient = useQueryClient();
  const { appointmentId } = useLocalSearchParams<{ appointmentId: string }>();
  const [tab, setTab] = useState<Tab>('consultation');
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
  const [followUpPreset, setFollowUpPreset] = useState<FollowUpPreset>('none');
  const [followUpDate, setFollowUpDate] = useState('');
  const [followUpNote, setFollowUpNote] = useState('');
  const [requiresBooking, setRequiresBooking] = useState(true);
  const [followUpError, setFollowUpError] = useState<string | null>(null);

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
    if (active.followUpDate) {
      setFollowUpDate(active.followUpDate);
      setFollowUpPreset('custom');
    }
  }, [workspace.data?.activeConsultation]);

  const resolveFollowUpDate = () => {
    if (followUpPreset === 'none') return null;
    if (followUpPreset === 'custom') return followUpDate.trim() || null;
    const days = Number(followUpPreset);
    return addDaysIso(clinicDateIso(), days);
  };

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['workspace', appointmentId] });
  };

  const start = useMutation({
    mutationFn: () => startConsultation(appointmentId!),
    onSuccess: refresh,
  });
  const saveConsult = useMutation({
    mutationFn: async (complete: boolean) => {
      const consultationId = workspace.data?.activeConsultation?.id;
      if (!consultationId) {
        throw new Error('Start the consultation before saving notes.');
      }
      const recommended = resolveFollowUpDate();
      if (complete && followUpPreset !== 'none') {
        if (!recommended) {
          setFollowUpError('Choose a follow-up date in the future.');
          throw new Error('Choose a follow-up date in the future.');
        }
        if (recommended <= clinicDateIso()) {
          setFollowUpError('Choose a follow-up date in the future.');
          throw new Error('Choose a follow-up date in the future.');
        }
      }
      setFollowUpError(null);
      const saved = await updateConsultation(consultationId, {
        symptoms,
        clinicalObservations: observations,
        doctorNotes: notes,
        advice,
        followUpDate: recommended ?? undefined,
        diagnoses: diagnosis.trim() ? [{ description: diagnosis.trim(), isPrimary: true }] : [],
        complete,
      });
      if (complete && recommended) {
        await createFollowUp({
          sourceConsultationId: consultationId,
          recommendedDate: recommended,
          followUpNote: followUpNote.trim() || undefined,
          requiresBooking,
        });
      }
      return saved;
    },
    onSuccess: async (_data, complete) => {
      await refresh();
      if (complete && followUpPreset !== 'none') {
        notifySuccess('Follow-up recommendation saved.');
      }
    },
    onError: (error) => {
      notifyApiError(error, 'Unable to save consultation notes.');
    },
  });
  const draftRx = useMutation({
    mutationFn: async () => {
      const consultationId = workspace.data?.activeConsultation?.id;
      if (!consultationId) {
        throw new Error('Start the consultation before creating a prescription.');
      }
      const created = await createPrescription(consultationId);
      setPrescriptionId(created.id);
      return created;
    },
    onError: (error) => {
      notifyApiError(error, 'Unable to create a prescription draft.');
    },
  });
  const saveRx = useMutation({
    mutationFn: async () => {
      const consultationId = workspace.data?.activeConsultation?.id;
      if (!consultationId && !prescriptionId) {
        throw new Error('Start the consultation before saving a prescription.');
      }
      const id = prescriptionId ?? (await createPrescription(consultationId!)).id;
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
    onError: (error) => {
      notifyApiError(error, 'Unable to save the prescription.');
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
    <Screen>
      <ScrollView
        contentContainerStyle={{
          gap: spacing.md,
          paddingBottom: spacing.xxl,
          paddingTop: Math.max(insets.top - 8, 0),
        }}
        keyboardShouldPersistTaps="handled">
        <ScreenHeader title={copy.consultationWorkspace} />

        {workspace.data ? (
          <View
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
            <ProfileAvatar name={workspace.data.patientName} size={48} />
            <View style={{ flex: 1, gap: 2 }}>
              <AppText variant="label">{workspace.data.patientName}</AppText>
              <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                {workspace.data.appointmentStatus}
              </AppText>
            </View>
            <Pressable onPress={() => router.push(`/doctor-patient/${appointmentId}` as Href)}>
              <AppText variant="label" style={{ color: theme.colors.primary }}>
                {copy.doctorViewAll}
              </AppText>
            </Pressable>
          </View>
        ) : null}

        {record ? (
          <View
            style={{
              backgroundColor: theme.colors.surface,
              borderRadius: 16,
              borderWidth: 1,
              borderColor: theme.colors.border,
              padding: spacing.md,
              gap: spacing.xs,
            }}>
            <AppText variant="label">{copy.medicalHistory}</AppText>
            <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
              {copy.allergies}:{' '}
              {record.allergies.filter((item) => item.isActive).map((item) => item.substance).join(', ') ||
                copy.noneRecorded}
            </AppText>
            <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
              {copy.medications}:{' '}
              {record.medications.filter((item) => item.isCurrent).map((item) => item.name).join(', ') ||
                copy.noneRecorded}
            </AppText>
          </View>
        ) : null}

        {!workspace.data?.activeConsultation ? (
          <Button label={copy.startConsultation} loading={start.isPending} onPress={() => start.mutate()} />
        ) : (
          <>
            <Segmented
              value={tab}
              onChange={setTab}
              options={[
                { value: 'consultation', label: copy.consultationWorkspace },
                { value: 'prescription', label: copy.prescriptionBuilder },
              ]}
            />

            {tab === 'consultation' ? (
              <View
                style={{
                  backgroundColor: theme.colors.surface,
                  borderRadius: 16,
                  borderWidth: 1,
                  borderColor: theme.colors.border,
                  padding: spacing.lg,
                  gap: spacing.md,
                }}>
                <TextField label={`${copy.clinicalSymptoms} *`} value={symptoms} onChangeText={setSymptoms} />
                <TextField label={copy.diagnosis} value={diagnosis} onChangeText={setDiagnosis} />
                <TextField label={copy.clinicalObservations} value={observations} onChangeText={setObservations} />
                <TextField label={copy.doctorNotes} value={notes} onChangeText={setNotes} />
                <TextField label={copy.advice} value={advice} onChangeText={setAdvice} />

                <View style={{ gap: spacing.sm, paddingTop: spacing.sm }}>
                  <AppText variant="label" style={{ color: theme.colors.text }}>
                    Schedule Follow-up
                  </AppText>
                  <AppText variant="muted" style={{ fontSize: 13, lineHeight: 18 }}>
                    Doctor-defined recommendation only. Patients must book to confirm a visit.
                  </AppText>
                  <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
                    {(
                      [
                        ['none', 'None'],
                        ['3', '3 days'],
                        ['7', '7 days'],
                        ['15', '15 days'],
                        ['30', '30 days'],
                        ['custom', 'Custom'],
                      ] as const
                    ).map(([value, label]) => {
                      const selected = followUpPreset === value;
                      return (
                        <Pressable
                          key={value}
                          onPress={() => {
                            setFollowUpPreset(value);
                            if (value !== 'none' && value !== 'custom') {
                              setFollowUpDate(addDaysIso(clinicDateIso(), Number(value)));
                            }
                            if (value === 'none') {
                              setFollowUpDate('');
                              setFollowUpError(null);
                            }
                          }}
                          style={{
                            paddingHorizontal: 12,
                            paddingVertical: 8,
                            borderRadius: 12,
                            borderWidth: 1,
                            borderColor: selected ? theme.colors.primary : theme.colors.border,
                            backgroundColor: selected ? theme.colors.primaryLight : theme.colors.surface,
                          }}>
                          <AppText
                            variant="label"
                            style={{ color: selected ? theme.colors.primary : theme.colors.text, fontSize: 13 }}>
                            {label}
                          </AppText>
                        </Pressable>
                      );
                    })}
                  </View>
                  {followUpPreset === 'custom' ? (
                    <TextField
                      label="Follow-up date (YYYY-MM-DD) *"
                      value={followUpDate}
                      onChangeText={setFollowUpDate}
                      placeholder="2026-11-14"
                    />
                  ) : followUpPreset !== 'none' && followUpDate ? (
                    <AppText variant="body">Follow-up date: {followUpDate}</AppText>
                  ) : null}
                  {followUpPreset !== 'none' ? (
                    <>
                      <TextField
                        label="Follow-up note (optional)"
                        value={followUpNote}
                        onChangeText={setFollowUpNote}
                        placeholder="Follow up for consultation review."
                      />
                      <Pressable
                        onPress={() => setRequiresBooking((value) => !value)}
                        style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm }}>
                        <View
                          style={{
                            width: 20,
                            height: 20,
                            borderRadius: 6,
                            borderWidth: 1,
                            borderColor: theme.colors.primary,
                            backgroundColor: requiresBooking ? theme.colors.primary : 'transparent',
                          }}
                        />
                        <AppText variant="body">Patient should book another appointment</AppText>
                      </Pressable>
                    </>
                  ) : null}
                  {followUpError ? (
                    <AppText variant="muted" style={{ color: theme.colors.error }}>
                      {followUpError}
                    </AppText>
                  ) : null}
                </View>

                <View style={{ flexDirection: 'row', gap: spacing.sm }}>
                  <View style={{ flex: 1 }}>
                    <Button
                      label={copy.saveDraft}
                      variant="secondary"
                      loading={saveConsult.isPending}
                      onPress={() => saveConsult.mutate(false)}
                    />
                  </View>
                  <View style={{ flex: 1 }}>
                    <Button label={copy.prescriptionBuilder} onPress={() => setTab('prescription')} />
                  </View>
                </View>
                <Button
                  label={copy.completeConsultation}
                  loading={saveConsult.isPending}
                  onPress={() => saveConsult.mutate(true)}
                />
              </View>
            ) : (
              <View
                style={{
                  backgroundColor: theme.colors.surface,
                  borderRadius: 16,
                  borderWidth: 1,
                  borderColor: theme.colors.border,
                  padding: spacing.lg,
                  gap: spacing.md,
                }}>
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
                      onChangeText={(value) =>
                        setMedicines((rows) => rows.map((row, i) => (i === index ? { ...row, dosage: value } : row)))
                      }
                    />
                    <TextField
                      label={copy.frequency}
                      value={medicine.frequency}
                      onChangeText={(value) =>
                        setMedicines((rows) => rows.map((row, i) => (i === index ? { ...row, frequency: value } : row)))
                      }
                    />
                    <TextField
                      label={copy.duration}
                      value={medicine.duration}
                      onChangeText={(value) =>
                        setMedicines((rows) => rows.map((row, i) => (i === index ? { ...row, duration: value } : row)))
                      }
                    />
                    <TextField
                      label={copy.route}
                      value={medicine.route}
                      onChangeText={(value) =>
                        setMedicines((rows) => rows.map((row, i) => (i === index ? { ...row, route: value } : row)))
                      }
                    />
                    <TextField
                      label={copy.instructions}
                      value={medicine.instructions}
                      onChangeText={(value) =>
                        setMedicines((rows) =>
                          rows.map((row, i) => (i === index ? { ...row, instructions: value } : row)),
                        )
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
                <View style={{ flexDirection: 'row', gap: spacing.sm }}>
                  <View style={{ flex: 1 }}>
                    <Button
                      label={copy.saveDraft}
                      variant="secondary"
                      loading={saveRx.isPending || draftRx.isPending}
                      onPress={() => saveRx.mutate()}
                    />
                  </View>
                  <View style={{ flex: 1 }}>
                    <Button
                      label={copy.finalizePrescription}
                      loading={finalize.isPending}
                      onPress={() => finalize.mutate()}
                    />
                  </View>
                </View>
              </View>
            )}
          </>
        )}
        {error ? <AppText variant="muted">{problemMessage(error, copy.consultationWorkspace)}</AppText> : null}
      </ScrollView>
    </Screen>
  );
}
