import { View } from 'react-native';

import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ProfileAvatar } from '@/components/ui/profile-avatar';
import { StatusBadge } from '@/components/ui/status-badge';
import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';

type Tone = 'neutral' | 'primary' | 'success' | 'warning' | 'error';

export function AppointmentCard({
  title,
  subtitle,
  meta,
  timeLabel,
  status,
  statusTone = 'primary',
  avatarName,
  photoUri,
  photoHeaders,
  primaryAction,
  secondaryAction,
}: {
  title: string;
  subtitle?: string;
  meta?: string;
  timeLabel?: string;
  status?: string;
  statusTone?: Tone;
  avatarName?: string;
  photoUri?: string | null;
  photoHeaders?: Record<string, string>;
  primaryAction?: { label: string; onPress: () => void; loading?: boolean };
  secondaryAction?: { label: string; onPress: () => void };
}) {
  const theme = useProdocTheme();
  const initialsFor = avatarName ?? title;

  return (
    <Card elevated style={{ gap: spacing.md, padding: spacing.lg }}>
      <View style={{ flexDirection: 'row', gap: spacing.md, alignItems: 'flex-start' }}>
        <ProfileAvatar name={initialsFor} uri={photoUri} headers={photoHeaders} size={52} />
        <View style={{ flex: 1, gap: spacing.xs }}>
          <View style={{ flexDirection: 'row', justifyContent: 'space-between', gap: spacing.sm }}>
            <AppText variant="headline" style={{ flex: 1 }} numberOfLines={1}>
              {title}
            </AppText>
            {timeLabel ? (
              <View
                style={{
                  backgroundColor: theme.colors.primaryLight,
                  borderRadius: radius.sm,
                  paddingHorizontal: spacing.sm,
                  paddingVertical: 4,
                }}>
                <AppText variant="caption" style={{ color: theme.colors.primary }}>
                  {timeLabel}
                </AppText>
              </View>
            ) : null}
          </View>
          {status ? <StatusBadge label={status} tone={statusTone} /> : null}
          {subtitle ? <AppText numberOfLines={2}>{subtitle}</AppText> : null}
          {meta ? <AppText variant="muted">{meta}</AppText> : null}
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
