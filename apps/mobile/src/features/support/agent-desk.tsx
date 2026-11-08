import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Pressable, View } from 'react-native';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import { getAdminTicket } from '@/services/api/admin-api';
import {
  disputeAction,
  getAgentWorkspace,
  getSupportAnalytics,
  listDisputes,
  manageSupportTicket,
  openDispute,
} from '@/services/api/support-api';
import { useAccountPermissions } from '@/stores/session-store';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

export function AgentDesk() {
  const copy = useCopy();
  const queryClient = useQueryClient();
  const permissions = useAccountPermissions();
  const canSupport = permissions.includes('admin.support');
  const [filter, setFilter] = useState('open');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [note, setNote] = useState('');
  const [internal, setInternal] = useState(true);
  const [disputeSummary, setDisputeSummary] = useState('');
  const [selectedDisputeId, setSelectedDisputeId] = useState<string | null>(null);

  const workspace = useQuery({
    queryKey: ['support', 'workspace', filter],
    queryFn: () => getAgentWorkspace(filter),
    enabled: canSupport,
  });
  const ticket = useQuery({
    queryKey: ['support', 'agent-ticket', selectedId],
    queryFn: () => getAdminTicket(selectedId!),
    enabled: canSupport && Boolean(selectedId),
  });
  const disputes = useQuery({
    queryKey: ['support', 'disputes'],
    queryFn: () => listDisputes(),
    enabled: canSupport,
  });
  const analytics = useQuery({
    queryKey: ['support', 'analytics'],
    queryFn: getSupportAnalytics,
    enabled: canSupport,
  });

  const manage = useMutation({
    mutationFn: (body: Parameters<typeof manageSupportTicket>[1]) => manageSupportTicket(selectedId!, body),
    onSuccess: async () => {
      setNote('');
      await queryClient.invalidateQueries({ queryKey: ['support'] });
    },
  });

  const createDispute = useMutation({
    mutationFn: () => openDispute(selectedId!, 'Payment', disputeSummary),
    onSuccess: async (created) => {
      setDisputeSummary('');
      setSelectedDisputeId(created.id);
      await queryClient.invalidateQueries({ queryKey: ['support', 'disputes'] });
    },
  });

  const actDispute = useMutation({
    mutationFn: (action: string) =>
      disputeAction(selectedDisputeId!, {
        action,
        evidenceRequest: action === 'request-evidence' ? note || 'Please upload booking proof.' : undefined,
        decision: action === 'resolve' ? 'Denied' : undefined,
        decisionNotes: action === 'resolve' ? note || 'Resolved after review.' : undefined,
      }),
    onSuccess: async () => queryClient.invalidateQueries({ queryKey: ['support'] }),
  });

  if (!canSupport) {
    return (
      <AuthScaffold>
        <AppText variant="title">{copy.agentDeskTitle}</AppText>
        <AppText variant="muted">{copy.agentDeskNoAccess}</AppText>
      </AuthScaffold>
    );
  }

  const error =
    workspace.error ?? ticket.error ?? disputes.error ?? analytics.error ?? manage.error ?? createDispute.error ?? actDispute.error;

  return (
    <AuthScaffold>
      <ScreenHeader title={copy.agentDeskTitle} />
      <Card>
        <AppText variant="label">{copy.supportAnalytics}</AppText>
        <AppText variant="muted">
          {copy.supportOpen}: {analytics.data?.openTickets ?? 0} · {copy.supportResolved}:{' '}
          {analytics.data?.resolvedLast30Days ?? 0} · {copy.supportDisputes}: {analytics.data?.openDisputes ?? 0}
        </AppText>
      </Card>
      <Card>
        <AppText variant="muted">
          {copy.supportOpen} {workspace.data?.openCount ?? 0} · {copy.supportPending} {workspace.data?.pendingCount ?? 0} ·{' '}
          {copy.supportHigh} {workspace.data?.highPriorityCount ?? 0} · {copy.supportOverdue}{' '}
          {workspace.data?.overdueCount ?? 0}
        </AppText>
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
          {['open', 'pending', 'high', 'assigned', 'overdue', 'all'].map((item) => (
            <Button
              key={item}
              label={item}
              variant={filter === item ? 'primary' : 'secondary'}
              onPress={() => setFilter(item)}
            />
          ))}
        </View>
        {(workspace.data?.tickets ?? []).map((item) => (
          <Pressable key={item.id} onPress={() => setSelectedId(item.id)}>
            <AppText>
              {item.number} · {item.subject}
            </AppText>
            <AppText variant="muted">
              {item.status} · {item.priority}
              {item.isOverdue ? ` · ${copy.supportOverdue}` : ''}
            </AppText>
          </Pressable>
        ))}
      </Card>
      {ticket.data ? (
        <Card>
          <AppText variant="title">{ticket.data.subject}</AppText>
          <AppText variant="muted">{ticket.data.description}</AppText>
          {ticket.data.notes.map((item) => (
            <AppText key={item.id} variant="muted">
              {item.isInternal ? `[${copy.supportInternal}] ` : ''}
              {item.body}
            </AppText>
          ))}
          <TextField label={copy.supportReply} value={note} onChangeText={setNote} />
          <Button
            label={internal ? copy.supportInternalNote : copy.supportCustomerReply}
            variant="secondary"
            onPress={() => setInternal((value) => !value)}
          />
          <Button
            label={copy.supportSendReply}
            loading={manage.isPending}
            onPress={() => manage.mutate({ note, internalNote: internal })}
          />
          <Button
            label={copy.supportEscalate}
            variant="secondary"
            onPress={() => manage.mutate({ escalate: true })}
          />
          <Button
            label={copy.adminResolveTicket}
            onPress={() =>
              manage.mutate({
                status: 'Resolved',
                resolutionDetails: note || 'Resolved by support',
                note: note || 'Resolved',
                internalNote: false,
              })
            }
          />
          <TextField label={copy.disputeSummary} value={disputeSummary} onChangeText={setDisputeSummary} />
          <Button
            label={copy.disputeOpen}
            loading={createDispute.isPending}
            onPress={() => createDispute.mutate()}
            disabled={disputeSummary.trim().length < 8}
          />
        </Card>
      ) : null}
      <Card>
        <AppText variant="label">{copy.disputeManagement}</AppText>
        {(disputes.data ?? []).map((item) => (
          <Pressable key={item.id} onPress={() => setSelectedDisputeId(item.id)}>
            <AppText>
              {item.number} · {item.kind} · {item.status}
            </AppText>
            <AppText variant="muted">{item.summary}</AppText>
          </Pressable>
        ))}
        {selectedDisputeId ? (
          <View style={{ gap: spacing.sm }}>
            <Button label={copy.disputeRequestEvidence} variant="secondary" onPress={() => actDispute.mutate('request-evidence')} />
            <Button label={copy.disputeEscalate} variant="secondary" onPress={() => actDispute.mutate('escalate')} />
            <Button label={copy.disputeResolve} onPress={() => actDispute.mutate('resolve')} />
          </View>
        ) : null}
      </Card>
      {error ? <AppText variant="muted">{problemMessage(error, copy.agentDeskTitle)}</AppText> : null}
    </AuthScaffold>
  );
}
