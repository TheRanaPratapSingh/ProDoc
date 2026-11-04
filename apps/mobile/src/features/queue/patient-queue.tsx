import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'expo-router';

import { AppShell } from '@/components/layout/app-shell';
import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ResponsiveHeader } from '@/components/layout/responsive-header';
import { AppointmentCard } from '@/components/ui/appointment-card';
import { EmptyState } from '@/components/ui/empty-state';
import { ErrorState } from '@/components/ui/error-state';
import { CardSkeleton } from '@/components/ui/skeleton';
import { Card } from '@/components/ui/card';
import { useCopy } from '@/i18n/copy';
import { listMyQueue } from '@/services/api/queue-api';
import { problemMessage } from '@/utils/problem';

export function PatientQueue() {
  const copy = useCopy();
  const router = useRouter();
  const queue = useQuery({ queryKey: ['queue', 'mine'], queryFn: listMyQueue, refetchInterval: 15000 });

  return (
    <AppShell active="queue">
      <AuthScaffold>
        <ResponsiveHeader title={copy.myQueue} onBack={() => router.back()} backLabel={copy.back} />
        {queue.isLoading ? (
          <Card>
            <CardSkeleton />
          </Card>
        ) : null}
        {(queue.data ?? []).map((entry) => (
          <AppointmentCard
            key={entry.id}
            title={entry.tokenLabel}
            subtitle={entry.doctorName}
            meta={[
              [entry.clinicName, entry.branchName].filter(Boolean).join(' · '),
              entry.statusMessage ?? entry.status,
              entry.patientsAhead != null ? `${copy.patientsAhead}: ${entry.patientsAhead}` : null,
              entry.estimatedWaitMinutes != null ? `${copy.estimatedWait}: ~${entry.estimatedWaitMinutes}` : null,
              new Date(entry.updatedAtUtc).toLocaleString(),
            ]
              .filter(Boolean)
              .join('\n')}
            status={entry.status}
            statusTone="primary"
          />
        ))}
        {queue.data?.length === 0 ? <EmptyState title={copy.noQueue} body={copy.loginSubtitle} /> : null}
        {queue.error ? (
          <ErrorState title={copy.myQueue} body={problemMessage(queue.error, copy.myQueue)} actionLabel={copy.continue} onAction={() => void queue.refetch()} />
        ) : null}
      </AuthScaffold>
    </AppShell>
  );
}
