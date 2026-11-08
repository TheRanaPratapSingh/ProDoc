import type { ReactNode } from 'react';
import { View } from 'react-native';

import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';

/** Compact Earth Green section card for admin create forms. */
export function AdminFormSection({
  title,
  description,
  children,
}: {
  title: string;
  description?: string;
  children: ReactNode;
}) {
  const theme = useProdocTheme();

  return (
    <View
      style={{
        backgroundColor: theme.colors.surface,
        borderRadius: radius.lg,
        borderWidth: 1,
        borderColor: theme.colors.border,
        padding: spacing.md,
        gap: spacing.sm,
        overflow: 'hidden',
      }}>
      <View
        style={{
          gap: 2,
          paddingBottom: spacing.xs,
          borderBottomWidth: 1,
          borderBottomColor: theme.colors.primaryLight,
        }}>
        <AppText variant="label" style={{ color: theme.colors.primary, fontSize: 13, lineHeight: 18 }}>
          {title}
        </AppText>
        {description ? (
          <AppText variant="muted" style={{ fontSize: 12, lineHeight: 16 }}>
            {description}
          </AppText>
        ) : null}
      </View>
      <View style={{ gap: spacing.sm }}>{children}</View>
    </View>
  );
}
