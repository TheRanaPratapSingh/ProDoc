import { type ReactNode } from 'react';
import { Pressable, View } from 'react-native';

import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';

export function NotificationCard({
  title,
  body,
  meta,
  unread = false,
  icon,
  onPress,
}: {
  title: string;
  body?: string;
  meta?: string;
  unread?: boolean;
  icon?: ReactNode;
  onPress?: () => void;
}) {
  const theme = useProdocTheme();

  const content = (
    <Card
      elevated
      style={{
        gap: spacing.sm,
        padding: spacing.lg,
        borderLeftWidth: unread ? 3 : 1,
        borderLeftColor: unread ? theme.colors.primary : theme.colors.border,
        backgroundColor: unread ? theme.colors.primaryLight : theme.colors.surface,
      }}>
      <View style={{ flexDirection: 'row', gap: spacing.md, alignItems: 'flex-start' }}>
        {icon ? (
          <View
            style={{
              width: 40,
              height: 40,
              borderRadius: radius.md,
              backgroundColor: theme.colors.surface,
              alignItems: 'center',
              justifyContent: 'center',
            }}>
            {icon}
          </View>
        ) : null}
        <View style={{ flex: 1, gap: 4 }}>
          <AppText variant="label">{title}</AppText>
          {body ? <AppText variant="muted">{body}</AppText> : null}
          {meta ? (
            <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
              {meta}
            </AppText>
          ) : null}
        </View>
      </View>
    </Card>
  );

  if (!onPress) {
    return content;
  }

  return <Pressable onPress={onPress}>{content}</Pressable>;
}
