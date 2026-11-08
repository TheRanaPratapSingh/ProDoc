import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, View } from 'react-native';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { EmptyState } from '@/components/ui/empty-state';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { deleteReview, listMyReviews } from '@/services/api/review-api';
import { notifySuccess } from '@/stores/feedback-store';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';
import { useRouter, type Href } from 'expo-router';

export function MyReviewsScreen() {
  const copy = useCopy();
  const router = useRouter();
  const queryClient = useQueryClient();
  const mine = useQuery({ queryKey: ['reviews', 'mine'], queryFn: listMyReviews });
  const remove = useMutation({
    mutationFn: (reviewId: string) => deleteReview(reviewId),
    onSuccess: async () => {
      notifySuccess(copy.reviewDeleteSuccess);
      await queryClient.invalidateQueries({ queryKey: ['reviews'] });
      await queryClient.invalidateQueries({ queryKey: ['rating'] });
      await queryClient.invalidateQueries({ queryKey: ['appointments'] });
    },
  });

  return (
    <AuthScaffold>
      <ScreenHeader title={copy.reviewSectionTitle} />
      {mine.isPending && !mine.data ? <AppText variant="muted">{copy.loading}</AppText> : null}
      {mine.isError && !mine.data ? (
        <AppText variant="muted">{problemMessage(mine.error, copy.reviewSectionTitle)}</AppText>
      ) : null}
      {(mine.data ?? []).map((review) => (
        <Card key={review.id} elevated style={{ gap: spacing.sm }}>
          <AppText variant="label">
            {review.targetKind}
            {review.overallRating != null ? ` · ${review.overallRating}★` : ''}
          </AppText>
          {review.body ? <AppText>{review.body}</AppText> : null}
          <AppText variant="muted">
            {copy.reviewSubmittedOn}{' '}
            {new Date(review.createdAtUtc).toLocaleDateString(undefined, {
              day: 'numeric',
              month: 'short',
              year: 'numeric',
            })}
          </AppText>
          <View style={{ flexDirection: 'row', gap: spacing.sm }}>
            <Button
              label={copy.reviewViewCta}
              variant="secondary"
              style={{ flex: 1 }}
              onPress={() =>
                router.push(
                  `/write-review?appointmentId=${encodeURIComponent(review.appointmentId)}&targetKind=${encodeURIComponent(review.targetKind)}&reviewId=${encodeURIComponent(review.id)}&mode=view` as Href,
                )
              }
            />
            {review.canEdit ? (
              <Button
                label={copy.reviewEditCta}
                variant="secondary"
                style={{ flex: 1 }}
                onPress={() =>
                  router.push(
                    `/write-review?appointmentId=${encodeURIComponent(review.appointmentId)}&targetKind=${encodeURIComponent(review.targetKind)}&reviewId=${encodeURIComponent(review.id)}&mode=edit` as Href,
                  )
                }
              />
            ) : null}
            {review.canDelete ? (
              <Button
                label={copy.reviewDelete}
                variant="danger"
                style={{ flex: 1 }}
                loading={remove.isPending && remove.variables === review.id}
                onPress={() =>
                  Alert.alert(copy.reviewDelete, copy.confirmCancelVisit, [
                    { text: copy.reviewSkip, style: 'cancel' },
                    {
                      text: copy.reviewDelete,
                      style: 'destructive',
                      onPress: () => remove.mutate(review.id),
                    },
                  ])
                }
              />
            ) : null}
          </View>
        </Card>
      ))}
      {!mine.isPending && (mine.data?.length ?? 0) === 0 ? (
        <EmptyState title={copy.reviewEmptyPublic} body={copy.reviewVerifiedNote} />
      ) : null}
      {remove.error ? <AppText variant="muted">{problemMessage(remove.error, copy.reviewDelete)}</AppText> : null}
    </AuthScaffold>
  );
}
