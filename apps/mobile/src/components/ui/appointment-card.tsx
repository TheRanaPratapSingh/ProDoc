import { View } from 'react-native';

import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { StatusBadge } from '@/components/ui/status-badge';
import { AppText } from '@/components/ui/text';
import { spacing } from '@/theme/tokens';

type Tone = 'neutral' | 'primary' | 'success' | 'warning' | 'error';

export function AppointmentCard({
  title,
  subtitle,
  meta,
  status,
  statusTone = 'primary',
  primaryAction,
  secondaryAction,
}: {
  title: string;
  subtitle?: string;
  meta?: string;
  status?: string;
  statusTone?: Tone;
  primaryAction?: { label: string; onPress: () => void; loading?: boolean };
  secondaryAction?: { label: string; onPress: () => void };
}) {
  return (
    <Card elevated style={{ gap: spacing.md }}>
      <View style={{ gap: spacing.xs }}>
        <AppText variant="headline">{title}</AppText>
        {status ? <StatusBadge label={status} tone={statusTone} /> : null}
        {subtitle ? <AppText>{subtitle}</AppText> : null}
        {meta ? <AppText variant="muted">{meta}</AppText> : null}
      </View>
      {primaryAction || secondaryAction ? (
        <View style={{ flexDirection: 'row', gap: spacing.sm }}>
          {primaryAction ? (
            <Button
              label={primaryAction.label}
              loading={primaryAction.loading}
              onPress={primaryAction.onPress}
              style={{ flex: 1 }}
            />
          ) : null}
          {secondaryAction ? (
            <Button
              label={secondaryAction.label}
              variant="secondary"
              onPress={secondaryAction.onPress}
              style={{ flex: 1 }}
            />
          ) : null}
        </View>
      ) : null}
    </Card>
  );
}
