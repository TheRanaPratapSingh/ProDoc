import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { useState } from 'react';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import { getReviewQueue, reviewApplication } from '@/services/api/provider-api';
import { problemMessage } from '@/utils/problem';

export function ReviewQueue() {
  const copy = useCopy();
  const router = useRouter();
  const queryClient = useQueryClient();
  const queue = useQuery({ queryKey: ['verifications'], queryFn: getReviewQueue });
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string>();
  const review = useMutation({
    mutationFn: (input: { kind: string; id: string; decision: string }) => reviewApplication(input.kind, input.id, input.decision, reason),
    onSuccess: async () => {
      setReason('');
      await queryClient.invalidateQueries({ queryKey: ['verifications'] });
    },
    onError: (caught) => setError(problemMessage(caught, copy.save)),
  });

  return (
    <AuthScaffold>
      <Button label={copy.back} variant="secondary" onPress={() => router.back()} />
      <AppText variant="title">{copy.reviewTitle}</AppText>
      <Card>
        <TextField label={copy.reason} value={reason} onChangeText={setReason} />
        {(queue.data ?? []).map((item) => (
          <Card key={`${item.kind}-${item.id}`}>
            <AppText>{item.name || item.reference}</AppText>
            <AppText variant="muted">{item.reference} · {item.status}</AppText>
            <Button label={copy.approve} loading={review.isPending} onPress={() => review.mutate({ kind: item.kind, id: item.id, decision: 'Approve' })} />
            <Button label={copy.reject} variant="danger" onPress={() => review.mutate({ kind: item.kind, id: item.id, decision: 'Reject' })} />
          </Card>
        ))}
        {queue.data?.length === 0 ? <AppText variant="muted">{copy.noDoctors}</AppText> : null}
        {error ? <AppText variant="muted">{error}</AppText> : null}
      </Card>
    </AuthScaffold>
  );
}
