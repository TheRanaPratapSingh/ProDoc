import { View } from 'react-native';

import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ProfileAvatar } from '@/components/ui/profile-avatar';
import { StatusBadge } from '@/components/ui/status-badge';
import { AppText } from '@/components/ui/text';
import { spacing } from '@/theme/tokens';

export function PatientCard({
  name,
  meta,
  status,
  statusTone = 'primary',
  primaryAction,
  secondaryAction,
}: {
  name: string;
  meta?: string;
  status?: string;
  statusTone?: 'neutral' | 'primary' | 'success' | 'warning' | 'error';
  primaryAction?: { label: string; onPress: () => void; loading?: boolean };
  secondaryAction?: { label: string; onPress: () => void };
}) {
  return (
    <Card elevated style={{ gap: spacing.md }}>
      <View style={{ flexDirection: 'row', gap: spacing.md, alignItems: 'center' }}>
        <ProfileAvatar name={name} size={48} />
        <View style={{ flex: 1, gap: spacing.xs }}>
          <AppText variant="headline" numberOfLines={1}>
            {name}
          </AppText>
          {meta ? <AppText variant="muted">{meta}</AppText> : null}
          {status ? <StatusBadge label={status} tone={statusTone} /> : null}
        </View>
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
