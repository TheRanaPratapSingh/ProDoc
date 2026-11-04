import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { useMemo, useState } from 'react';
import { Pressable, View } from 'react-native';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import { createReview, listEligibleReviews, submitFeedback } from '@/services/api/review-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

function StarPicker({ value, onChange, label }: { value: number; onChange: (value: number) => void; label: string }) {
  const theme = useProdocTheme();
  return (
    <View style={{ gap: spacing.sm }}>
      <AppText variant="label">{label}</AppText>
      <View style={{ flexDirection: 'row', gap: spacing.sm }}>
        {[1, 2, 3, 4, 5].map((star) => (
          <Pressable
            key={star}
            onPress={() => onChange(star)}
            style={{
              width: 40,
              height: 40,
              borderRadius: radius.sm,
              alignItems: 'center',
              justifyContent: 'center',
              backgroundColor: star <= value ? theme.colors.primary : theme.colors.surface,
              borderWidth: 1,
              borderColor: theme.colors.border,
            }}>
            <AppText style={{ color: star <= value ? theme.colors.onPrimary : theme.colors.text }}>{star}</AppText>
          </Pressable>
        ))}
      </View>
    </View>
  );
}

export function ReviewComposeScreen() {
  const copy = useCopy();
  const router = useRouter();
  const queryClient = useQueryClient();
  const eligible = useQuery({ queryKey: ['reviews', 'eligible'], queryFn: listEligibleReviews });
  const [selectedAppointmentId, setSelectedAppointmentId] = useState<string | null>(null);
  const [targetKind, setTargetKind] = useState<'Doctor' | 'Clinic'>('Doctor');
  const [overall, setOverall] = useState(5);
  const [communication, setCommunication] = useState(5);
  const [cleanliness, setCleanliness] = useState(5);
  const [staff, setStaff] = useState(5);
  const [waiting, setWaiting] = useState(5);
  const [body, setBody] = useState('');
  const [feedbackBody, setFeedbackBody] = useState('');
  const [error, setError] = useState<string>();

  const selected = useMemo(
    () => eligible.data?.find((item) => item.appointmentId === selectedAppointmentId) ?? eligible.data?.[0] ?? null,
    [eligible.data, selectedAppointmentId],
  );

  const create = useMutation({
    mutationFn: () =>
      createReview({
        appointmentId: selected!.appointmentId,
        targetKind,
        overallRating: overall,
        communicationRating: targetKind === 'Doctor' ? communication : undefined,
        cleanlinessRating: targetKind === 'Clinic' ? cleanliness : undefined,
        staffBehaviourRating: targetKind === 'Clinic' ? staff : undefined,
        waitingTimeRating: waiting,
        body: body || undefined,
      }),
    onSuccess: async () => {
      setBody('');
      await queryClient.invalidateQueries({ queryKey: ['reviews'] });
      setError(undefined);
    },
    onError: (caught) => setError(problemMessage(caught, copy.reviewSubmit)),
  });

  const feedback = useMutation({
    mutationFn: () =>
      submitFeedback({
        category: 'PlatformUsability',
        rating: overall,
        body: feedbackBody,
        appointmentId: selected?.appointmentId,
      }),
    onSuccess: () => {
      setFeedbackBody('');
      setError(undefined);
    },
    onError: (caught) => setError(problemMessage(caught, copy.feedbackSubmit)),
  });

  return (
    <AuthScaffold>
      <Button label={copy.back} variant="secondary" onPress={() => router.back()} />
      <AppText variant="title">{copy.reviewWriteTitle}</AppText>
      <AppText variant="muted">{copy.reviewVerifiedNote}</AppText>
      <Card>
        {(eligible.data ?? []).map((item) => (
          <Pressable key={item.appointmentId} onPress={() => setSelectedAppointmentId(item.appointmentId)}>
            <AppText>
              {item.appointmentReference} · {item.doctorName}
            </AppText>
            <AppText variant="muted">
              {item.clinicName}
              {item.canReviewDoctor ? ` · ${copy.reviewDoctor}` : ''}
              {item.canReviewClinic ? ` · ${copy.reviewClinic}` : ''}
            </AppText>
          </Pressable>
        ))}
        {eligible.data?.length === 0 ? <AppText variant="muted">{copy.reviewNoneEligible}</AppText> : null}
      </Card>
      {selected ? (
        <Card>
          <View style={{ flexDirection: 'row', gap: spacing.sm }}>
            {selected.canReviewDoctor ? (
              <Button label={copy.reviewDoctor} variant={targetKind === 'Doctor' ? 'primary' : 'secondary'} onPress={() => setTargetKind('Doctor')} />
            ) : null}
            {selected.canReviewClinic ? (
              <Button label={copy.reviewClinic} variant={targetKind === 'Clinic' ? 'primary' : 'secondary'} onPress={() => setTargetKind('Clinic')} />
            ) : null}
          </View>
          <StarPicker label={copy.reviewOverall} value={overall} onChange={setOverall} />
          {targetKind === 'Doctor' ? (
            <StarPicker label={copy.reviewCommunication} value={communication} onChange={setCommunication} />
          ) : (
            <>
              <StarPicker label={copy.reviewCleanliness} value={cleanliness} onChange={setCleanliness} />
              <StarPicker label={copy.reviewStaff} value={staff} onChange={setStaff} />
            </>
          )}
          <StarPicker label={copy.reviewWaiting} value={waiting} onChange={setWaiting} />
          <TextField label={copy.reviewBody} value={body} onChangeText={setBody} multiline />
          <Button
            label={copy.reviewSubmit}
            loading={create.isPending}
            disabled={!selected.canReviewDoctor && !selected.canReviewClinic}
            onPress={() => create.mutate()}
          />
        </Card>
      ) : null}
      <Card>
        <AppText variant="label">{copy.feedbackPrivateTitle}</AppText>
        <AppText variant="muted">{copy.feedbackPrivateBody}</AppText>
        <TextField label={copy.feedbackMessage} value={feedbackBody} onChangeText={setFeedbackBody} multiline />
        <Button
          label={copy.feedbackSubmit}
          variant="secondary"
          loading={feedback.isPending}
          disabled={feedbackBody.trim().length < 8}
          onPress={() => feedback.mutate()}
        />
      </Card>
      {error ? <AppText variant="muted">{error}</AppText> : null}
    </AuthScaffold>
  );
}
