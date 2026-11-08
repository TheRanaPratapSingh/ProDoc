import { useQuery } from '@tanstack/react-query';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { listAdminQueues } from '@/services/api/queue-api';
import { problemMessage } from '@/utils/problem';

function day(offset = 0) {
  const date = new Date();
  date.setDate(date.getDate() + offset);
  return date.toISOString().slice(0, 10);
}

export function QueueMonitor() {
  const copy = useCopy();
  const from = day(0);
  const to = day(0);
  const rows = useQuery({ queryKey: ['admin', 'queues', from], queryFn: () => listAdminQueues(from, to) });

  return (
    <AuthScaffold>
      <ScreenHeader title={copy.queueMonitor} />
      {(rows.data ?? []).map((row, index) => (
        <Card key={`${row.clinicName}-${row.doctorName}-${index}`}>
          <AppText>
            {row.clinicName} · {row.branchName}
          </AppText>
          <AppText>{row.doctorName}</AppText>
          <AppText variant="muted">
            {copy.waiting}: {row.waiting} · {copy.completedVisit}: {row.completed} · {copy.walkIn}: {row.walkIns}
          </AppText>
          <AppText variant="muted">
            {copy.estimatedWait}: {row.averageWaitMinutes?.toFixed(0) ?? '—'} / {row.averageConsultationMinutes?.toFixed(0) ?? '—'}
          </AppText>
        </Card>
      ))}
      {rows.data?.length === 0 ? <AppText variant="muted">{copy.noQueue}</AppText> : null}
      {rows.error ? <AppText variant="muted">{problemMessage(rows.error, copy.queueMonitor)}</AppText> : null}
    </AuthScaffold>
  );
}
