import { useQuery } from '@tanstack/react-query';
import { useWindowDimensions, View } from 'react-native';

import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { queueDisplay } from '@/services/api/queue-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

export function WaitingDisplay({ clinicId, branchId, doctorId }: { clinicId: string; branchId: string; doctorId: string }) {
  const copy = useCopy();
  const theme = useProdocTheme();
  const { width } = useWindowDimensions();
  const board = useQuery({
    queryKey: ['queue', 'display', clinicId, branchId, doctorId],
    queryFn: () => queueDisplay(clinicId, branchId, doctorId),
    refetchInterval: 8_000,
    refetchIntervalInBackground: false,
    enabled: Boolean(clinicId && branchId && doctorId),
  });
  const tokenSize = Math.min(96, width / 6);

  return (
    <View style={{ flex: 1, backgroundColor: theme.colors.background, padding: spacing.xxxl, gap: spacing.xl }}>
      <AppText variant="title">
        {board.data?.clinicName} · {board.data?.branchName}
      </AppText>
      <AppText variant="muted">{board.data?.doctorName}</AppText>
      <Card
        elevated
        style={{
          alignItems: 'center',
          gap: spacing.md,
          backgroundColor: theme.colors.primaryLight,
          borderRadius: radius.xl,
          paddingVertical: spacing.xxl,
        }}>
        <AppText variant="label" style={{ color: theme.colors.primary }}>
          {copy.nowServing}
        </AppText>
        <AppText style={{ fontSize: tokenSize, fontWeight: '700', color: theme.colors.primary, letterSpacing: 2 }}>
          {board.data?.nowConsulting ?? '—'}
        </AppText>
        <AppText variant="muted">{copy.nowConsulting}</AppText>
      </Card>
      <Card elevated style={{ gap: spacing.md }}>
        <AppText variant="label">{copy.nextInQueue}</AppText>
        <AppText style={{ fontSize: Math.min(48, width / 12), fontWeight: '600', color: theme.colors.text }}>
          {board.data?.next ?? '—'}
        </AppText>
        <AppText variant="muted">
          {copy.pleaseBeReady}: {board.data?.pleaseBeReady ?? '—'}
        </AppText>
      </Card>
      <AppText variant="muted">{board.data?.status}</AppText>
      {board.error ? <AppText variant="muted">{problemMessage(board.error, copy.waitingRoom)}</AppText> : null}
    </View>
  );
}
