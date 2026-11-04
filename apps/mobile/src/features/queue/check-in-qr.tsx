import { useMutation } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { View } from 'react-native';
import QRCode from 'react-native-qrcode-svg';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ResponsiveHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { mintCheckInQr } from '@/services/api/queue-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

export function CheckInQr({ appointmentId }: { appointmentId: string }) {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const [token, setToken] = useState<string | null>(null);
  const [expires, setExpires] = useState<string | null>(null);
  const mint = useMutation({
    mutationFn: () => mintCheckInQr(appointmentId),
    onSuccess: (result) => {
      setToken(result.token);
      setExpires(result.expiresAtUtc);
    },
  });

  return (
    <AuthScaffold>
      <ResponsiveHeader title={copy.showCheckInQr} onBack={() => router.back()} backLabel={copy.back} />
      <Card elevated>
        <Button label={copy.showCheckInQr} loading={mint.isPending} onPress={() => mint.mutate()} />
        {token ? (
          <View
            style={{
              alignItems: 'center',
              gap: spacing.md,
              padding: spacing.xl,
              backgroundColor: theme.colors.primaryLight,
              borderRadius: 18,
            }}>
            <QRCode value={token} size={220} color={theme.colors.text} backgroundColor={theme.colors.surface} />
            <AppText variant="label">{copy.checkInCode}</AppText>
            <AppText variant="headline">{token}</AppText>
            {expires ? <AppText variant="muted">{new Date(expires).toLocaleString()}</AppText> : null}
          </View>
        ) : null}
        {mint.error ? <AppText variant="muted">{problemMessage(mint.error, copy.showCheckInQr)}</AppText> : null}
      </Card>
    </AuthScaffold>
  );
}
