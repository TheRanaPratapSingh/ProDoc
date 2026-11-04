import { useQuery } from '@tanstack/react-query';
import { useWindowDimensions, View } from 'react-native';

import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { queueDisplay } from '@/services/api/queue-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

export function WaitingDisplay({ clinicId, branchId, doctorId }: { clinicId: string; branchId: string; doctorId: string }) {
  const copy = useCopy();
  const theme = useProdocTheme();
  const { width } = useWindowDimensions();
  const board = useQuery({
    queryKey: ['queue', 'display', clinicId, branchId, doctorId],
    queryFn: () => queueDisplay(clinicId, branchId, doctorId),
    refetchInterval: 5000,
    enabled: Boolean(clinicId && branchId && doctorId),
  });

  return (
    <View style={{ flex: 1, backgroundColor: theme.colors.background, padding: spacing.xxxl, gap: spacing.xl }}>
      <AppText variant="title">
        {board.data?.clinicName} · {board.data?.branchName}
      </AppText>
      <AppText variant="muted">{board.data?.doctorName}</AppText>
      <AppText style={{ fontSize: Math.min(72, width / 8), fontWeight: '700' }}>
        {copy.nowConsulting}: {board.data?.nowConsulting ?? '—'}
      </AppText>
      <AppText style={{ fontSize: Math.min(48, width / 12), fontWeight: '600' }}>
        {copy.openTimes}: {board.data?.next ?? '—'}
      </AppText>
      <AppText style={{ fontSize: Math.min(36, width / 16) }}>
        {copy.pleaseBeReady}: {board.data?.pleaseBeReady ?? '—'}
      </AppText>
      <AppText variant="muted">{board.data?.status}</AppText>
      {board.error ? <AppText variant="muted">{problemMessage(board.error, copy.waitingRoom)}</AppText> : null}
    </View>
  );
}
