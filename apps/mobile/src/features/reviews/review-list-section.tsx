import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { View } from 'react-native';

import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
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
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

function SummaryCard({ summary }: { summary: RatingSummary }) {
  const copy = useCopy();
  return (
    <Card>
      <AppText variant="label">{copy.reviewRatings}</AppText>
      {summary.averageIsPublic && summary.averageOverall != null ? (
        <AppText variant="title">
          {summary.averageOverall.toFixed(1)} · {summary.publishedReviewCount} {copy.reviewCountLabel}
        </AppText>
      ) : (
        <AppText variant="muted">{summary.displayNote}</AppText>
      )}
      {summary.distribution.map((bucket) => (
        <AppText key={bucket.stars} variant="muted">
          {bucket.stars}★ · {bucket.count}
        </AppText>
      ))}
    </Card>
  );
}

function ReviewCard({
  review,
  onReport,
}: {
  review: ReviewDetails;
  onReport: (reviewId: string, reason: string) => void;
}) {
  const copy = useCopy();
  const [reason, setReason] = useState('');
  return (
    <Card>
      <AppText>
        {review.overallRating}★ {review.isVerifiedVisit ? `· ${copy.reviewVerifiedBadge}` : ''}
      </AppText>
      {review.body ? <AppText>{review.body}</AppText> : null}
      {review.responses.map((response) => (
        <AppText key={response.id} variant="muted">
          {copy.reviewProviderResponse}: {response.body}
        </AppText>
      ))}
      <TextField label={copy.reviewReportReason} value={reason} onChangeText={setReason} />
      <Button
        label={copy.reviewReport}
        variant="secondary"
        disabled={reason.trim().length < 4}
        onPress={() => onReport(review.id, reason.trim())}
      />
    </Card>
  );
}

export function DoctorReviewSection({ doctorId }: { doctorId: string }) {
  const copy = useCopy();
  const queryClient = useQueryClient();
  const summary = useQuery({ queryKey: ['rating', 'doctor', doctorId], queryFn: () => getDoctorRatingSummary(doctorId) });
  const reviews = useQuery({ queryKey: ['reviews', 'doctor', doctorId], queryFn: () => listDoctorReviews(doctorId) });
  const report = useMutation({
    mutationFn: ({ reviewId, reason }: { reviewId: string; reason: string }) => reportReview(reviewId, reason),
    onSuccess: async () => queryClient.invalidateQueries({ queryKey: ['reviews', 'doctor', doctorId] }),
  });

  return (
    <View style={{ gap: spacing.lg }}>
      <AppText variant="label">{copy.reviewSectionTitle}</AppText>
      {summary.data ? <SummaryCard summary={summary.data} /> : null}
      {(reviews.data ?? []).map((review) => (
        <ReviewCard
          key={review.id}
          review={review}
          onReport={(reviewId, reason) => report.mutate({ reviewId, reason })}
        />
      ))}
      {reviews.data?.length === 0 ? <AppText variant="muted">{copy.reviewEmptyPublic}</AppText> : null}
      {report.error ? <AppText variant="muted">{problemMessage(report.error, copy.reviewReport)}</AppText> : null}
    </View>
  );
}

export function ClinicReviewSection({ clinicId }: { clinicId: string }) {
  const copy = useCopy();
  const queryClient = useQueryClient();
  const summary = useQuery({ queryKey: ['rating', 'clinic', clinicId], queryFn: () => getClinicRatingSummary(clinicId) });
  const reviews = useQuery({ queryKey: ['reviews', 'clinic', clinicId], queryFn: () => listClinicReviews(clinicId) });
  const report = useMutation({
    mutationFn: ({ reviewId, reason }: { reviewId: string; reason: string }) => reportReview(reviewId, reason),
    onSuccess: async () => queryClient.invalidateQueries({ queryKey: ['reviews', 'clinic', clinicId] }),
  });

  return (
    <View style={{ gap: spacing.lg }}>
      <AppText variant="label">{copy.reviewSectionTitle}</AppText>
      {summary.data ? <SummaryCard summary={summary.data} /> : null}
      {(reviews.data ?? []).map((review) => (
        <ReviewCard
          key={review.id}
          review={review}
          onReport={(reviewId, reason) => report.mutate({ reviewId, reason })}
        />
      ))}
      {reviews.data?.length === 0 ? <AppText variant="muted">{copy.reviewEmptyPublic}</AppText> : null}
      {report.error ? <AppText variant="muted">{problemMessage(report.error, copy.reviewReport)}</AppText> : null}
    </View>
  );
}
