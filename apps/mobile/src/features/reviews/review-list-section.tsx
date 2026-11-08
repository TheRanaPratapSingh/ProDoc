import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { Pressable, View } from 'react-native';
import Animated, { FadeInDown } from 'react-native-reanimated';

import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ProfileAvatar } from '@/components/ui/profile-avatar';
import { StarRatingDisplay } from '@/components/ui/star-rating-display';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { VerificationBadge } from '@/components/ui/verification-badge';
import { useCopy } from '@/i18n/copy';
import {
  getClinicRatingSummary,
  getDoctorRatingSummary,
  listClinicReviews,
  listDoctorReviews,
  reportReview,
  type RatingSummary,
  type ReviewDetails,
} from '@/services/api/review-api';
import { notifySuccess } from '@/stores/feedback-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { duration, radius, spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

const PAGE_SIZE = 10;

function Stars({ value, size = 16 }: { value: number | null | undefined; size?: number }) {
  const theme = useProdocTheme();
  if (value == null) return null;
  return (
    <View style={{ flexDirection: 'row', gap: 2 }}>
      {[1, 2, 3, 4, 5].map((star) => (
        <AppText
          key={star}
          style={{
            fontSize: size,
            lineHeight: size + 4,
            color: star <= value ? theme.colors.primary : theme.colors.border,
          }}>
          ★
        </AppText>
      ))}
    </View>
  );
}

function DistributionBars({ summary }: { summary: RatingSummary }) {
  const theme = useProdocTheme();
  const copy = useCopy();
  const total = Math.max(
    1,
    summary.distribution.reduce((sum, bucket) => sum + bucket.count, 0),
  );
  return (
    <View style={{ gap: spacing.sm }}>
      {summary.distribution.map((bucket) => {
        const widthPct = Math.max(4, Math.round((bucket.count / total) * 100));
        return (
          <View key={bucket.stars} style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm }}>
            <AppText variant="caption" style={{ width: 28, color: theme.colors.textMuted }}>
              {bucket.stars}★
            </AppText>
            <View
              style={{
                flex: 1,
                height: 8,
                borderRadius: radius.pill,
                backgroundColor: theme.colors.primaryLight,
                overflow: 'hidden',
              }}>
              <View
                style={{
                  width: `${widthPct}%`,
                  height: '100%',
                  backgroundColor: theme.colors.primary,
                  borderRadius: radius.pill,
                }}
              />
            </View>
            <AppText variant="caption" style={{ width: 28, textAlign: 'right', color: theme.colors.textMuted }}>
              {bucket.count}
            </AppText>
          </View>
        );
      })}
      <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
        {copy.reviewDistributionNote}
      </AppText>
    </View>
  );
}

export function RatingHeader({ summary, compact = false }: { summary: RatingSummary; compact?: boolean }) {
  const copy = useCopy();
  const average = summary.averageIsPublic && summary.averageOverall != null ? summary.averageOverall : null;
  return (
    <Card elevated style={{ gap: spacing.md }}>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.md }}>
        <View style={{ gap: 4, flex: 1 }}>
          <AppText variant="label">{copy.reviewRatings}</AppText>
          <StarRatingDisplay
            average={average}
            reviewCount={summary.publishedReviewCount}
            size={18}
          />
        </View>
      </View>
      {!compact && average != null ? <DistributionBars summary={summary} /> : null}
    </Card>
  );
}

function CategoryChips({ review }: { review: ReviewDetails }) {
  const copy = useCopy();
  const theme = useProdocTheme();
  const chips = [
    review.staffBehaviourRating != null ? { label: copy.reviewBehaviour, value: review.staffBehaviourRating } : null,
    review.communicationRating != null ? { label: copy.reviewCommunication, value: review.communicationRating } : null,
    review.consultationRating != null ? { label: copy.reviewConsultation, value: review.consultationRating } : null,
    review.cleanlinessRating != null ? { label: copy.reviewCleanliness, value: review.cleanlinessRating } : null,
    review.waitingTimeRating != null ? { label: copy.reviewClinicExperience, value: review.waitingTimeRating } : null,
  ].filter(Boolean) as { label: string; value: number }[];

  if (chips.length === 0) return null;
  return (
    <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
      {chips.map((chip) => (
        <View
          key={chip.label}
          style={{
            paddingHorizontal: spacing.sm,
            paddingVertical: 4,
            borderRadius: radius.md,
            backgroundColor: theme.colors.primaryLight,
            borderWidth: 1,
            borderColor: theme.colors.border,
          }}>
          <AppText variant="caption" style={{ color: theme.colors.primaryPressed }}>
            {chip.label} · {chip.value}★
          </AppText>
        </View>
      ))}
    </View>
  );
}

function ReviewCard({
  review,
  index,
  onReport,
}: {
  review: ReviewDetails;
  index: number;
  onReport: (reviewId: string, reason: string) => void;
}) {
  const copy = useCopy();
  const theme = useProdocTheme();
  const [reason, setReason] = useState('');
  const [reporting, setReporting] = useState(false);
  const author = review.authorDisplayName || copy.reviewAnonymousPatient;
  const submitted = new Date(review.createdAtUtc).toLocaleDateString(undefined, {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  });
  const visited = review.appointmentCompletedAtUtc
    ? new Date(review.appointmentCompletedAtUtc).toLocaleDateString(undefined, {
        day: 'numeric',
        month: 'short',
        year: 'numeric',
      })
    : null;

  return (
    <Animated.View entering={theme.reduceMotion ? undefined : FadeInDown.delay(Math.min(index, 5) * 35).duration(duration.enter)}>
      <Card elevated style={{ gap: spacing.md }}>
        <View style={{ flexDirection: 'row', gap: spacing.md, alignItems: 'flex-start' }}>
          <ProfileAvatar name={author} size={44} />
          <View style={{ flex: 1, gap: 4 }}>
            <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm, alignItems: 'center' }}>
              <AppText variant="headline">{author}</AppText>
              {review.isVerifiedVisit ? <VerificationBadge label={copy.reviewVerifiedBadge} /> : null}
            </View>
            <Stars value={review.overallRating} />
            <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
              {copy.reviewSubmittedOn} {submitted}
              {visited ? ` · ${copy.reviewVisitedOn} ${visited}` : ''}
            </AppText>
          </View>
        </View>

        <CategoryChips review={review} />
        {review.body ? <AppText>{review.body}</AppText> : null}

        {review.responses.map((response) => (
          <View
            key={response.id}
            style={{
              padding: spacing.md,
              borderRadius: radius.lg,
              backgroundColor: theme.colors.primaryLight,
              gap: 4,
            }}>
            <AppText variant="label">{copy.reviewProviderResponse}</AppText>
            <AppText variant="muted">{response.body}</AppText>
          </View>
        ))}

        {reporting ? (
          <View style={{ gap: spacing.sm }}>
            <TextField label={copy.reviewReportReason} value={reason} onChangeText={setReason} />
            <View style={{ flexDirection: 'row', gap: spacing.sm }}>
              <Button
                label={copy.reviewReport}
                variant="secondary"
                disabled={reason.trim().length < 4}
                onPress={() => {
                  onReport(review.id, reason.trim());
                  setReason('');
                  setReporting(false);
                }}
                style={{ flex: 1 }}
              />
              <Button label={copy.reviewSkip} variant="secondary" onPress={() => setReporting(false)} style={{ flex: 1 }} />
            </View>
          </View>
        ) : (
          <Pressable onPress={() => setReporting(true)} hitSlop={8}>
            <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
              {copy.reviewReport}
            </AppText>
          </Pressable>
        )}
      </Card>
    </Animated.View>
  );
}

function ReviewSection({
  summary,
  reviews,
  loadingMore,
  canLoadMore,
  onLoadMore,
  onReport,
  reportError,
}: {
  summary?: RatingSummary;
  reviews: ReviewDetails[];
  loadingMore: boolean;
  canLoadMore: boolean;
  onLoadMore: () => void;
  onReport: (reviewId: string, reason: string) => void;
  reportError?: unknown;
}) {
  const copy = useCopy();
  return (
    <View style={{ gap: spacing.lg }}>
      <AppText variant="label">{copy.reviewSectionTitle}</AppText>
      {summary ? <RatingHeader summary={summary} /> : null}
      {reviews.map((review, index) => (
        <ReviewCard key={review.id} review={review} index={index} onReport={onReport} />
      ))}
      {reviews.length === 0 ? <AppText variant="muted">{copy.reviewEmptyPublic}</AppText> : null}
      {canLoadMore ? (
        <Button label={copy.reviewLoadMore} variant="secondary" loading={loadingMore} onPress={onLoadMore} />
      ) : null}
      {reportError ? <AppText variant="muted">{problemMessage(reportError, copy.reviewReport)}</AppText> : null}
    </View>
  );
}

export function DoctorReviewSection({ doctorId }: { doctorId: string }) {
  const copy = useCopy();
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [items, setItems] = useState<ReviewDetails[]>([]);
  const summary = useQuery({ queryKey: ['rating', 'doctor', doctorId], queryFn: () => getDoctorRatingSummary(doctorId) });
  const reviews = useQuery({
    queryKey: ['reviews', 'doctor', doctorId, page],
    queryFn: () => listDoctorReviews(doctorId, page, PAGE_SIZE),
  });

  useEffect(() => {
    if (!reviews.data) return;
    setItems((previous) => {
      if (page === 1) return reviews.data ?? [];
      const seen = new Set(previous.map((item) => item.id));
      return [...previous, ...(reviews.data ?? []).filter((item) => !seen.has(item.id))];
    });
  }, [page, reviews.data]);

  const report = useMutation({
    mutationFn: ({ reviewId, reason }: { reviewId: string; reason: string }) => reportReview(reviewId, reason),
    onSuccess: async () => {
      notifySuccess(copy.reviewReportSuccess);
      await queryClient.invalidateQueries({ queryKey: ['reviews', 'doctor', doctorId] });
    },
  });

  return (
    <ReviewSection
      summary={summary.data}
      reviews={items}
      loadingMore={reviews.isFetching && page > 1 && !reviews.isPlaceholderData}
      canLoadMore={(reviews.data?.length ?? 0) >= PAGE_SIZE}
      onLoadMore={() => setPage((current) => current + 1)}
      onReport={(reviewId, reason) => report.mutate({ reviewId, reason })}
      reportError={report.error}
    />
  );
}

export function ClinicReviewSection({ clinicId }: { clinicId: string }) {
  const copy = useCopy();
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [items, setItems] = useState<ReviewDetails[]>([]);
  const summary = useQuery({ queryKey: ['rating', 'clinic', clinicId], queryFn: () => getClinicRatingSummary(clinicId) });
  const reviews = useQuery({
    queryKey: ['reviews', 'clinic', clinicId, page],
    queryFn: () => listClinicReviews(clinicId, page, PAGE_SIZE),
  });

  useEffect(() => {
    if (!reviews.data) return;
    setItems((previous) => {
      if (page === 1) return reviews.data ?? [];
      const seen = new Set(previous.map((item) => item.id));
      return [...previous, ...(reviews.data ?? []).filter((item) => !seen.has(item.id))];
    });
  }, [page, reviews.data]);

  const report = useMutation({
    mutationFn: ({ reviewId, reason }: { reviewId: string; reason: string }) => reportReview(reviewId, reason),
    onSuccess: async () => {
      notifySuccess(copy.reviewReportSuccess);
      await queryClient.invalidateQueries({ queryKey: ['reviews', 'clinic', clinicId] });
    },
  });

  return (
    <ReviewSection
      summary={summary.data}
      reviews={items}
      loadingMore={reviews.isFetching && page > 1 && !reviews.isPlaceholderData}
      canLoadMore={(reviews.data?.length ?? 0) >= PAGE_SIZE}
      onLoadMore={() => setPage((current) => current + 1)}
      onReport={(reviewId, reason) => report.mutate({ reviewId, reason })}
      reportError={report.error}
    />
  );
}
