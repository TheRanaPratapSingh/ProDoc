import { useMemo, useState } from 'react';
import { Alert, Pressable, ScrollView, View } from 'react-native';

import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { EmptyState } from '@/components/ui/empty-state';
import { ProfileAvatar } from '@/components/ui/profile-avatar';
import { SearchBar } from '@/components/ui/search-bar';
import { StatusBadge } from '@/components/ui/status-badge';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import type { QueueEntry, QueueSummary } from '@/services/api/queue-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';
import { formatVisitTime, paymentStatusLabel } from '@/utils/visit-format';

export type QueueFilter =
  | 'all'
  | 'waiting'
  | 'inConsultation'
  | 'completed'
  | 'skipped'
  | 'paid'
  | 'unpaid';

function queueTone(status: string): 'neutral' | 'primary' | 'success' | 'warning' | 'error' {
  const value = status.toLowerCase();
  if (value.includes('complete')) return 'success';
  if (value.includes('consult') || value.includes('called')) return 'primary';
  if (value.includes('wait') || value.includes('hold')) return 'warning';
  if (value.includes('skip') || value.includes('cancel')) return 'error';
  return 'neutral';
}

function isPaid(status?: string | null) {
  return (status ?? '').toLowerCase() === 'paid';
}

function matchesFilter(entry: QueueEntry, filter: QueueFilter) {
  const status = entry.status.toLowerCase();
  switch (filter) {
    case 'waiting':
      return status === 'waiting' || status === 'called' || status === 'onhold';
    case 'inConsultation':
      return status === 'inconsultation';
    case 'completed':
      return status === 'completed';
    case 'skipped':
      return status === 'skipped';
    case 'paid':
      return isPaid(entry.paymentStatus);
    case 'unpaid':
      return !isPaid(entry.paymentStatus);
    default:
      return true;
  }
}

function matchesSearch(entry: QueueEntry, query: string) {
  if (!query.trim()) return true;
  const q = query.trim().toLowerCase();
  return (
    entry.displayName.toLowerCase().includes(q) ||
    entry.tokenLabel.toLowerCase().includes(q) ||
    (entry.appointmentReference ?? '').toLowerCase().includes(q) ||
    (entry.appointmentId ?? '').toLowerCase().includes(q)
  );
}

export function DailyQueueBoard({
  summary,
  entries,
  busy,
  canManage,
  onCallNext,
  onAction,
  onOpenConsultation,
}: {
  summary: QueueSummary | undefined;
  entries: QueueEntry[];
  busy?: boolean;
  canManage: boolean;
  onCallNext: () => void;
  onAction: (entryId: string, action: string) => void;
  onOpenConsultation?: (appointmentId: string) => void;
}) {
  const copy = useCopy();
  const theme = useProdocTheme();
  const [filter, setFilter] = useState<QueueFilter>('all');
  const [search, setSearch] = useState('');

  const sorted = useMemo(
    () =>
      [...entries].sort((a, b) => {
        const aTime = a.appointmentStartsAtUtc ? new Date(a.appointmentStartsAtUtc).getTime() : new Date(a.checkInAtUtc).getTime();
        const bTime = b.appointmentStartsAtUtc ? new Date(b.appointmentStartsAtUtc).getTime() : new Date(b.checkInAtUtc).getTime();
        if (aTime !== bTime) return aTime - bTime;
        return a.tokenNumber - b.tokenNumber;
      }),
    [entries],
  );

  const visible = useMemo(
    () => sorted.filter((entry) => matchesFilter(entry, filter) && matchesSearch(entry, search)),
    [filter, search, sorted],
  );

  const grouped = useMemo(() => {
    const buckets = new Map<string, QueueEntry[]>();
    for (const entry of visible) {
      const start = entry.appointmentStartsAtUtc;
      const key = start
        ? new Date(start).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })
        : copy.walkIn;
      const list = buckets.get(key) ?? [];
      list.push(entry);
      buckets.set(key, list);
    }
    return [...buckets.entries()];
  }, [copy.walkIn, visible]);

  const cards: { key: QueueFilter; label: string; value: number }[] = [
    { key: 'all', label: copy.aqTotalAppointments, value: summary?.totalToday ?? entries.length },
    { key: 'waiting', label: copy.aqWaitingPatients, value: summary?.waiting ?? 0 },
    { key: 'inConsultation', label: copy.aqInConsultation, value: summary?.inConsultation ?? 0 },
    { key: 'completed', label: copy.aqConsultationsCompleted, value: summary?.completed ?? 0 },
    { key: 'skipped', label: copy.aqSkippedNoShow, value: summary?.skipped ?? 0 },
    { key: 'paid', label: copy.aqPaidAppointments, value: summary?.paid ?? 0 },
    { key: 'unpaid', label: copy.aqUnpaidAppointments, value: summary?.unpaid ?? 0 },
  ];

  const confirmSkip = (entryId: string) => {
    Alert.alert(copy.aqSkipConfirmTitle, copy.aqSkipConfirmBody, [
      { text: copy.cancel, style: 'cancel' },
      { text: copy.aqSkipNoShow, style: 'destructive', onPress: () => onAction(entryId, 'skip') },
    ]);
  };

  const confirmComplete = (entryId: string) => {
    Alert.alert(copy.aqMarkConsulted, copy.aqMarkConsultedConfirm, [
      { text: copy.cancel, style: 'cancel' },
      { text: copy.aqMarkConsulted, onPress: () => onAction(entryId, 'complete-consultation') },
    ]);
  };

  return (
    <View style={{ gap: spacing.lg }}>
      <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: spacing.sm }}>
        {cards.map((card) => {
          const selected = filter === card.key;
          return (
            <Pressable
              key={card.key}
              onPress={() => setFilter(card.key)}
              style={{
                minWidth: 118,
                borderRadius: radius.lg,
                borderWidth: selected ? 1.5 : 1,
                borderColor: selected ? theme.colors.primary : theme.colors.border,
                backgroundColor: selected ? theme.colors.primaryLight : theme.colors.surface,
                padding: spacing.md,
                gap: spacing.xs,
              }}>
              <AppText variant="headline">{card.value}</AppText>
              <AppText variant="caption" numberOfLines={2}>
                {card.label}
              </AppText>
            </Pressable>
          );
        })}
      </ScrollView>

      <View style={{ flexDirection: 'row', gap: spacing.md }}>
        <Card elevated style={{ flex: 1, gap: spacing.xs, backgroundColor: theme.colors.primaryLight }}>
          <AppText variant="caption" style={{ color: theme.colors.primaryPressed }}>
            {copy.aqNowServing}
          </AppText>
          {summary?.current ? (
            <>
              <AppText variant="headline">{summary.current.tokenLabel}</AppText>
              <AppText>{summary.current.displayName}</AppText>
              <AppText variant="muted">
                {summary.current.appointmentStartsAtUtc
                  ? formatVisitTime(summary.current.appointmentStartsAtUtc)
                  : '—'}{' '}
                · {summary.current.status}
              </AppText>
            </>
          ) : (
            <AppText variant="muted">{copy.aqNobodyServing}</AppText>
          )}
        </Card>
        <Card elevated style={{ flex: 1, gap: spacing.xs }}>
          <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
            {copy.aqNextPatient}
          </AppText>
          {summary?.next ? (
            <>
              <AppText variant="headline">{summary.next.tokenLabel}</AppText>
              <AppText>{summary.next.displayName}</AppText>
              <AppText variant="muted">
                {summary.next.appointmentStartsAtUtc ? formatVisitTime(summary.next.appointmentStartsAtUtc) : '—'}
              </AppText>
            </>
          ) : (
            <AppText variant="muted">{copy.aqNoNextPatient}</AppText>
          )}
        </Card>
      </View>

      {canManage ? <Button label={copy.callNext} loading={busy} onPress={onCallNext} /> : null}

      <SearchBar value={search} onChangeText={setSearch} placeholder={copy.aqSearchQueue} />

      {visible.length === 0 ? <EmptyState title={copy.aqEmptyQueue} body={copy.aqEmptyQueueBody} /> : null}

      {grouped.map(([rangeLabel, rangeEntries]) => (
        <View key={rangeLabel} style={{ gap: spacing.sm }}>
          <AppText variant="label">
            {rangeLabel} · {rangeEntries.length}
          </AppText>
          {rangeEntries.map((entry) => {
            const serving = summary?.current?.id === entry.id;
            const nextUp = summary?.next?.id === entry.id;
            const done = entry.status.toLowerCase() === 'completed';
            const skipped = entry.status.toLowerCase() === 'skipped';
            const payment = paymentStatusLabel(entry.paymentStatus ?? 'Unpaid', entry.paymentMethod ?? 'PayAtClinic', {
              paid: copy.paid,
              payAtClinic: copy.payAtClinic,
              paymentPending: copy.paymentPending,
            });
            return (
              <Card
                key={entry.id}
                elevated
                style={{
                  gap: spacing.sm,
                  borderWidth: serving || nextUp ? 1.5 : 1,
                  borderColor: serving ? theme.colors.primary : nextUp ? theme.colors.accent : theme.colors.border,
                  backgroundColor: serving ? theme.colors.primaryLight : theme.colors.surface,
                }}>
                <View style={{ flexDirection: 'row', gap: spacing.md, alignItems: 'center' }}>
                  <View
                    style={{
                      minWidth: 52,
                      paddingVertical: spacing.sm,
                      borderRadius: radius.md,
                      backgroundColor: theme.colors.primaryPressed,
                      alignItems: 'center',
                    }}>
                    <AppText style={{ color: theme.colors.onPrimary, fontWeight: '700' }}>{entry.tokenLabel}</AppText>
                  </View>
                  <ProfileAvatar name={entry.displayName} size={44} />
                  <View style={{ flex: 1, gap: 2 }}>
                    <AppText variant="headline">{entry.displayName}</AppText>
                    <AppText variant="muted">
                      {entry.appointmentStartsAtUtc
                        ? formatVisitTime(entry.appointmentStartsAtUtc)
                        : formatVisitTime(entry.checkInAtUtc)}
                      {entry.appointmentReference ? ` · ${entry.appointmentReference}` : ''}
                    </AppText>
                    <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                      {payment}
                    </AppText>
                  </View>
                  <StatusBadge label={done ? copy.aqConsultationCompleted : entry.status} tone={queueTone(entry.status)} />
                </View>

                {serving ? (
                  <AppText variant="caption" style={{ color: theme.colors.primaryPressed }}>
                    {copy.aqNowServing}
                  </AppText>
                ) : null}
                {nextUp && !serving ? (
                  <AppText variant="caption" style={{ color: theme.colors.accent }}>
                    {copy.aqNextPatient}
                  </AppText>
                ) : null}

                {canManage && !done && !skipped ? (
                  <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
                    {entry.status.toLowerCase() === 'waiting' || entry.status.toLowerCase() === 'onhold' ? (
                      <Button label={copy.recall} variant="secondary" onPress={() => onAction(entry.id, 'call')} />
                    ) : null}
                    {entry.status.toLowerCase() !== 'inconsultation' ? (
                      <Button
                        label={copy.startConsult}
                        variant="secondary"
                        onPress={() => onAction(entry.id, 'start-consultation')}
                      />
                    ) : null}
                    <Button label={copy.aqMarkConsulted} onPress={() => confirmComplete(entry.id)} />
                    <Button label={copy.aqSkipNoShow} variant="secondary" onPress={() => confirmSkip(entry.id)} />
                    {entry.appointmentId && onOpenConsultation ? (
                      <Button
                        label={copy.openConsultation}
                        variant="secondary"
                        onPress={() => onOpenConsultation(entry.appointmentId!)}
                      />
                    ) : null}
                  </View>
                ) : null}
              </Card>
            );
          })}
        </View>
      ))}
    </View>
  );
}
