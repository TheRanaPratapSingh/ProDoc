import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { useState } from 'react';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import { listProviderReviewInbox, respondToReview } from '@/services/api/review-api';
import { problemMessage } from '@/utils/problem';

export function ProviderReviewInboxScreen() {
  const copy = useCopy();
  const router = useRouter();
  const queryClient = useQueryClient();
  const inbox = useQuery({ queryKey: ['reviews', 'inbox'], queryFn: listProviderReviewInbox });
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
    <AuthScaffold>
      <Button label={copy.back} variant="secondary" onPress={() => router.back()} />
      <AppText variant="title">{copy.reviewInboxTitle}</AppText>
      <AppText variant="muted">{copy.reviewInboxBody}</AppText>
      {(inbox.data ?? []).map((review) => {
        const hasOwn = review.responses.length > 0;
        return (
          <Card key={review.id}>
            <AppText>
              {review.overallRating}★ · {review.targetKind} · {copy.reviewVerifiedBadge}
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
  );
}
