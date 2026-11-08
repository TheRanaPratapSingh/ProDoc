import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useLocalSearchParams, useRouter, type Href } from 'expo-router';
import { useEffect, useMemo, useState } from 'react';
import { Pressable, View } from 'react-native';
import Animated, { FadeInDown } from 'react-native-reanimated';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { EmptyState } from '@/components/ui/empty-state';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import {
  createReview,
  getReview,
  listEligibleReviews,
  updateReview,
  type ReviewDetails,
} from '@/services/api/review-api';
import { notifySuccess } from '@/stores/feedback-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { duration, radius, spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

const MAX_BODY = 2000;

function StarRow({
  value,
  onChange,
  label,
  hint,
  readOnly = false,
}: {
  value: number | null;
  onChange: (value: number | null) => void;
  label: string;
  hint?: string;
  readOnly?: boolean;
}) {
  const theme = useProdocTheme();
  return (
    <View style={{ gap: spacing.sm }}>
      <View style={{ gap: 2 }}>
        <AppText variant="label">{label}</AppText>
        {hint ? (
          <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
            {hint}
          </AppText>
        ) : null}
      </View>
      <View style={{ flexDirection: 'row', gap: spacing.sm, alignItems: 'center' }}>
        {[1, 2, 3, 4, 5].map((star) => {
          const selected = value != null && star <= value;
          return (
            <Pressable
              key={star}
              accessibilityRole="button"
              accessibilityLabel={`${star} stars`}
              hitSlop={6}
              disabled={readOnly}
              onPress={() => {
                if (readOnly) return;
                onChange(value === star ? null : star);
              }}
              style={{
                width: 44,
                height: 44,
                borderRadius: radius.md,
                alignItems: 'center',
                justifyContent: 'center',
                backgroundColor: selected ? theme.colors.primaryLight : theme.colors.surface,
                borderWidth: 1,
                borderColor: selected ? theme.colors.primary : theme.colors.border,
                opacity: readOnly ? 0.9 : 1,
              }}>
              <AppText
                style={{
                  fontSize: 22,
                  lineHeight: 26,
                  color: selected ? theme.colors.primary : theme.colors.textMuted,
                }}>
                ★
              </AppText>
            </Pressable>
          );
        })}
        {value != null && !readOnly ? (
          <Pressable onPress={() => onChange(null)} hitSlop={8}>
            <AppText variant="caption" style={{ color: theme.colors.primary }}>
              Clear
            </AppText>
          </Pressable>
        ) : null}
      </View>
    </View>
  );
}

function applyReviewToForm(
  review: ReviewDetails,
  setters: {
    setTargetKind: (value: 'Doctor' | 'Clinic') => void;
    setOverall: (value: number | null) => void;
    setBehaviour: (value: number | null) => void;
    setCommunication: (value: number | null) => void;
    setConsultation: (value: number | null) => void;
    setClinicExperience: (value: number | null) => void;
    setCleanliness: (value: number | null) => void;
    setStaff: (value: number | null) => void;
    setWaiting: (value: number | null) => void;
    setBody: (value: string) => void;
  },
) {
  setters.setTargetKind(review.targetKind === 'Clinic' ? 'Clinic' : 'Doctor');
  setters.setOverall(review.overallRating);
  setters.setBehaviour(review.staffBehaviourRating);
  setters.setCommunication(review.communicationRating);
  setters.setConsultation(review.consultationRating);
  setters.setCleanliness(review.cleanlinessRating);
  setters.setStaff(review.staffBehaviourRating);
  setters.setWaiting(review.waitingTimeRating);
  setters.setClinicExperience(review.waitingTimeRating ?? review.cleanlinessRating);
  setters.setBody(review.body ?? '');
}

export function ReviewComposeScreen() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const queryClient = useQueryClient();
  const params = useLocalSearchParams<{
    appointmentId?: string;
    targetKind?: string;
    reviewId?: string;
    mode?: string;
  }>();
  const readOnly = params.mode === 'view';
  const isEdit = Boolean(params.reviewId) && params.mode !== 'view';
  const eligible = useQuery({
    queryKey: ['reviews', 'eligible'],
    queryFn: listEligibleReviews,
    enabled: !params.reviewId,
  });
  const existing = useQuery({
    queryKey: ['reviews', 'detail', params.reviewId],
    queryFn: () => getReview(params.reviewId!),
    enabled: Boolean(params.reviewId),
  });
  const [selectedAppointmentId, setSelectedAppointmentId] = useState<string | null>(params.appointmentId ?? null);
  const [targetKind, setTargetKind] = useState<'Doctor' | 'Clinic'>(
    params.targetKind === 'Clinic' ? 'Clinic' : 'Doctor',
  );
  const [overall, setOverall] = useState<number | null>(null);
  const [behaviour, setBehaviour] = useState<number | null>(null);
  const [communication, setCommunication] = useState<number | null>(null);
  const [consultation, setConsultation] = useState<number | null>(null);
  const [clinicExperience, setClinicExperience] = useState<number | null>(null);
  const [cleanliness, setCleanliness] = useState<number | null>(null);
  const [staff, setStaff] = useState<number | null>(null);
  const [waiting, setWaiting] = useState<number | null>(null);
  const [body, setBody] = useState('');
  const [error, setError] = useState<string>();
  const [hydrated, setHydrated] = useState(!params.reviewId);

  useEffect(() => {
    if (!existing.data || hydrated) return;
    applyReviewToForm(existing.data, {
      setTargetKind,
      setOverall,
      setBehaviour,
      setCommunication,
      setConsultation,
      setClinicExperience,
      setCleanliness,
      setStaff,
      setWaiting,
      setBody,
    });
    setHydrated(true);
  }, [existing.data, hydrated]);

  const selected = useMemo(() => {
    const rows = eligible.data ?? [];
    if (selectedAppointmentId) {
      return rows.find((item) => item.appointmentId === selectedAppointmentId) ?? null;
    }
    return rows[0] ?? null;
  }, [eligible.data, selectedAppointmentId]);

  useEffect(() => {
    if (!selected || params.reviewId) return;
    if (targetKind === 'Doctor' && !selected.canReviewDoctor && selected.canReviewClinic) {
      setTargetKind('Clinic');
    } else if (targetKind === 'Clinic' && !selected.canReviewClinic && selected.canReviewDoctor) {
      setTargetKind('Doctor');
    }
  }, [selected, targetKind, params.reviewId]);

  const trimmedBody = body.trim();
  const hasContent =
    overall != null ||
    behaviour != null ||
    communication != null ||
    consultation != null ||
    clinicExperience != null ||
    cleanliness != null ||
    staff != null ||
    waiting != null ||
    trimmedBody.length > 0;

  const save = useMutation({
    mutationFn: async () => {
      const payload = {
        overallRating: overall,
        communicationRating: targetKind === 'Doctor' ? communication : null,
        staffBehaviourRating: targetKind === 'Doctor' ? behaviour : staff,
        consultationRating: targetKind === 'Doctor' ? consultation : null,
        cleanlinessRating: targetKind === 'Clinic' ? cleanliness ?? clinicExperience : null,
        waitingTimeRating: targetKind === 'Clinic' ? waiting : clinicExperience,
        body: trimmedBody || null,
      };
      if (params.reviewId) {
        return updateReview(params.reviewId, payload);
      }
      if (!selected) throw new Error('no_selection');
      return createReview({
        appointmentId: selected.appointmentId,
        targetKind,
        ...payload,
      });
    },
    onSuccess: async (saved) => {
      setError(undefined);
      notifySuccess(copy.reviewSubmitSuccess);
      await queryClient.invalidateQueries({ queryKey: ['reviews'] });
      await queryClient.invalidateQueries({ queryKey: ['rating'] });
      await queryClient.invalidateQueries({ queryKey: ['appointments'] });
      if (saved.doctorId) {
        await queryClient.invalidateQueries({ queryKey: ['rating', 'doctor', saved.doctorId] });
      }
      if (saved.clinicId) {
        await queryClient.invalidateQueries({ queryKey: ['rating', 'clinic', saved.clinicId] });
      }
      if (router.canGoBack()) {
        router.back();
      } else {
        router.replace('/visits' as Href);
      }
    },
    onError: (caught) => setError(problemMessage(caught, copy.reviewSubmit)),
  });

  const title = readOnly ? copy.reviewViewTitle : isEdit ? copy.reviewEditTitle : copy.reviewWriteTitle;
  const showForm = Boolean(params.reviewId) ? hydrated && Boolean(existing.data) : Boolean(selected);

  return (
    <AuthScaffold>
      <ScreenHeader title={title} />
      {!params.reviewId ? <AppText variant="muted">{copy.reviewVerifiedNote}</AppText> : null}

      {eligible.isPending && !params.reviewId ? <AppText variant="muted">{copy.loading}</AppText> : null}
      {existing.isPending && params.reviewId ? <AppText variant="muted">{copy.loading}</AppText> : null}
      {eligible.isError && !params.reviewId ? (
        <AppText variant="muted">{problemMessage(eligible.error, copy.reviewWriteTitle)}</AppText>
      ) : null}
      {existing.isError && params.reviewId ? (
        <AppText variant="muted">{problemMessage(existing.error, copy.reviewViewTitle)}</AppText>
      ) : null}

      {!eligible.isPending && (eligible.data?.length ?? 0) === 0 && !params.reviewId ? (
        <EmptyState title={copy.reviewNoneEligible} body={copy.reviewVerifiedNote} />
      ) : null}

      {(eligible.data?.length ?? 0) > 0 && !params.appointmentId && !params.reviewId ? (
        <Card elevated style={{ gap: spacing.sm }}>
          <AppText variant="label">{copy.reviewSelectVisit}</AppText>
          {(eligible.data ?? []).map((item, index) => {
            const active = selected?.appointmentId === item.appointmentId;
            return (
              <Animated.View
                key={item.appointmentId}
                entering={theme.reduceMotion ? undefined : FadeInDown.delay(index * 40).duration(duration.enter)}>
                <Pressable
                  onPress={() => setSelectedAppointmentId(item.appointmentId)}
                  style={{
                    padding: spacing.md,
                    borderRadius: radius.lg,
                    borderWidth: 1,
                    borderColor: active ? theme.colors.primary : theme.colors.border,
                    backgroundColor: active ? theme.colors.primaryLight : theme.colors.surface,
                    gap: 4,
                  }}>
                  <AppText variant="body">
                    {item.doctorName} · {item.appointmentReference}
                  </AppText>
                  <AppText variant="muted">
                    {item.clinicName}
                    {item.canReviewDoctor ? ` · ${copy.reviewDoctor}` : ''}
                    {item.canReviewClinic ? ` · ${copy.reviewClinic}` : ''}
                  </AppText>
                </Pressable>
              </Animated.View>
            );
          })}
        </Card>
      ) : null}

      {showForm ? (
        <Card elevated style={{ gap: spacing.lg }}>
          {selected && (selected.canReviewDoctor || selected.canReviewClinic) && !params.reviewId ? (
            <View style={{ flexDirection: 'row', gap: spacing.sm }}>
              {selected.canReviewDoctor ? (
                <Button
                  label={copy.reviewDoctor}
                  variant={targetKind === 'Doctor' ? 'primary' : 'secondary'}
                  onPress={() => setTargetKind('Doctor')}
                  style={{ flex: 1 }}
                />
              ) : null}
              {selected.canReviewClinic ? (
                <Button
                  label={copy.reviewClinic}
                  variant={targetKind === 'Clinic' ? 'primary' : 'secondary'}
                  onPress={() => setTargetKind('Clinic')}
                  style={{ flex: 1 }}
                />
              ) : null}
            </View>
          ) : null}

          {selected ? (
            <AppText variant="muted">
              {targetKind === 'Doctor' ? selected.doctorName : selected.clinicName}
            </AppText>
          ) : null}

          <StarRow
            label={copy.reviewOverall}
            hint={copy.reviewOverallHint}
            value={overall}
            onChange={setOverall}
            readOnly={readOnly}
          />

          {targetKind === 'Doctor' ? (
            <>
              <StarRow
                label={copy.reviewBehaviour}
                hint={copy.reviewBehaviourHint}
                value={behaviour}
                onChange={setBehaviour}
                readOnly={readOnly}
              />
              <StarRow
                label={copy.reviewCommunication}
                hint={copy.reviewCommunicationHint}
                value={communication}
                onChange={setCommunication}
                readOnly={readOnly}
              />
              <StarRow
                label={copy.reviewConsultation}
                hint={copy.reviewConsultationHint}
                value={consultation}
                onChange={setConsultation}
                readOnly={readOnly}
              />
              <StarRow
                label={copy.reviewClinicExperience}
                hint={copy.reviewClinicExperienceHint}
                value={clinicExperience}
                onChange={setClinicExperience}
                readOnly={readOnly}
              />
            </>
          ) : (
            <>
              <StarRow
                label={copy.reviewClinicExperience}
                hint={copy.reviewClinicExperienceHint}
                value={clinicExperience}
                onChange={setClinicExperience}
                readOnly={readOnly}
              />
              <StarRow label={copy.reviewCleanliness} value={cleanliness} onChange={setCleanliness} readOnly={readOnly} />
              <StarRow label={copy.reviewStaff} value={staff} onChange={setStaff} readOnly={readOnly} />
              <StarRow label={copy.reviewWaiting} value={waiting} onChange={setWaiting} readOnly={readOnly} />
            </>
          )}

          <View style={{ gap: spacing.xs }}>
            <TextField
              label={copy.reviewBody}
              value={body}
              onChangeText={(next) => {
                if (readOnly) return;
                setBody(next.slice(0, MAX_BODY));
              }}
              multiline
              editable={!readOnly}
              placeholder={copy.reviewBodyPlaceholder}
            />
            <AppText variant="caption" style={{ color: theme.colors.textMuted, alignSelf: 'flex-end' }}>
              {trimmedBody.length}/{MAX_BODY}
            </AppText>
          </View>

          {readOnly ? (
            <View style={{ flexDirection: 'row', gap: spacing.sm }}>
              {existing.data?.canEdit ? (
                <Button
                  label={copy.reviewEditCta}
                  style={{ flex: 1 }}
                  onPress={() =>
                    router.replace(
                      `/write-review?appointmentId=${encodeURIComponent(existing.data!.appointmentId)}&targetKind=${encodeURIComponent(existing.data!.targetKind)}&reviewId=${encodeURIComponent(existing.data!.id)}&mode=edit` as Href,
                    )
                  }
                />
              ) : null}
              <Button
                label={copy.reviewSkip}
                variant="secondary"
                style={{ flex: 1 }}
                onPress={() => {
                  if (router.canGoBack()) router.back();
                  else router.replace('/visits' as Href);
                }}
              />
            </View>
          ) : (
            <>
              <Button
                label={isEdit ? copy.reviewEditCta : copy.reviewSubmit}
                loading={save.isPending}
                disabled={!hasContent || save.isPending}
                onPress={() => {
                  if (save.isPending) return;
                  save.mutate();
                }}
              />
              <Button
                label={copy.reviewSkip}
                variant="secondary"
                disabled={save.isPending}
                onPress={() => {
                  if (router.canGoBack()) router.back();
                  else router.replace('/visits' as Href);
                }}
              />
            </>
          )}
        </Card>
      ) : null}

      {error ? <AppText variant="muted">{error}</AppText> : null}
    </AuthScaffold>
  );
}
