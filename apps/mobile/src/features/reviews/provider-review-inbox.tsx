import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';

import { DoctorShell } from '@/components/layout/doctor-shell';
import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { DashboardStatCard } from '@/components/ui/stat-card';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import { getMyDoctor } from '@/services/api/provider-api';
import { getDoctorRatingSummary, listProviderReviewInbox, respondToReview } from '@/services/api/review-api';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';
import { View } from 'react-native';

export function ProviderReviewInboxScreen() {
  const copy = useCopy();
  const queryClient = useQueryClient();
  const inbox = useQuery({ queryKey: ['reviews', 'inbox'], queryFn: listProviderReviewInbox });
  const doctor = useQuery({ queryKey: ['doctor', 'me'], queryFn: getMyDoctor });
  const rating = useQuery({
    queryKey: ['reviews', 'doctor-summary', doctor.data?.id],
    queryFn: () => getDoctorRatingSummary(doctor.data!.id),
    enabled: Boolean(doctor.data?.id),
  });
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  const [error, setError] = useState<string>();

  const respond = useMutation({
    mutationFn: ({ reviewId, body }: { reviewId: string; body: string }) => respondToReview(reviewId, body),
    onSuccess: async (_, variables) => {
      setDrafts((prev) => ({ ...prev, [variables.reviewId]: '' }));
      await queryClient.invalidateQueries({ queryKey: ['reviews', 'inbox'] });
      setError(undefined);
    },
    onError: (caught) => setError(problemMessage(caught, copy.reviewRespond)),
  });

  return (
    <DoctorShell active="reviews">
    <AuthScaffold>
      <ScreenHeader title={copy.reviewInboxTitle} />
      <AppText variant="muted">{copy.doctorReviewsReadOnly}</AppText>
      {rating.data ? (
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md }}>
          <DashboardStatCard
            label={copy.doctorAvgRating}
            value={rating.data.averageOverall != null ? rating.data.averageOverall.toFixed(1) : '—'}
          />
          <DashboardStatCard label={copy.reviewVerifiedBadge} value={rating.data.publishedReviewCount} />
        </View>
      ) : null}
      {(rating.data?.distribution ?? []).map((bucket) => (
        <AppText key={bucket.stars} variant="muted">
          {bucket.stars}★ · {bucket.count}
        </AppText>
      ))}
      <AppText variant="muted">{copy.reviewInboxBody}</AppText>
      {(inbox.data ?? []).map((review) => {
        const hasOwn = review.responses.length > 0;
        return (
          <Card key={review.id}>
            <AppText>
              {review.overallRating != null ? `${review.overallRating}★ · ` : ''}
              {review.targetKind} · {copy.reviewVerifiedBadge}
            </AppText>
            {review.body ? <AppText>{review.body}</AppText> : null}
            {review.responses.map((response) => (
              <AppText key={response.id} variant="muted">
                {copy.reviewProviderResponse}: {response.body}
              </AppText>
            ))}
            {!hasOwn ? (
              <>
                <TextField
                  label={copy.reviewResponse}
                  value={drafts[review.id] ?? ''}
                  onChangeText={(value) => setDrafts((prev) => ({ ...prev, [review.id]: value }))}
                  multiline
                />
                <Button
                  label={copy.reviewRespond}
                  loading={respond.isPending}
                  disabled={(drafts[review.id] ?? '').trim().length < 2}
                  onPress={() => respond.mutate({ reviewId: review.id, body: (drafts[review.id] ?? '').trim() })}
                />
              </>
            ) : null}
          </Card>
        );
      })}
      {inbox.data?.length === 0 ? <AppText variant="muted">{copy.reviewInboxEmpty}</AppText> : null}
      {error ? <AppText variant="muted">{error}</AppText> : null}
    </AuthScaffold>
    </DoctorShell>
  );
}
