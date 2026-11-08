import { type ReactNode } from 'react';
import { View } from 'react-native';

import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';

export function AdminKpiCard({
  label,
  value,
  hint,
  icon,
  width,
}: {
  label: string;
  value: string | number;
  hint?: string;
  icon?: ReactNode;
  width?: number | `${number}%`;
}) {
  const theme = useProdocTheme();

  return (
    <View
      style={{
        backgroundColor: theme.colors.surface,
        borderRadius: radius.lg,
        borderWidth: 1,
        borderColor: theme.colors.border,
        padding: spacing.lg,
        gap: spacing.sm,
        minWidth: 148,
        flexGrow: 1,
        flexBasis: 148,
        maxWidth: width,
        shadowColor: '#14243D',
        shadowOffset: { width: 0, height: 4 },
        shadowOpacity: theme.scheme === 'light' ? 0.04 : 0,
        shadowRadius: 12,
        elevation: theme.scheme === 'light' ? 1 : 0,
      }}>
      <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: spacing.sm }}>
        <AppText variant="caption" style={{ color: theme.colors.textMuted, flex: 1 }} numberOfLines={2}>
          {label}
        </AppText>
        {icon ? (
          <View
            style={{
              width: 32,
              height: 32,
              borderRadius: 10,
              backgroundColor: theme.colors.primaryLight,
              alignItems: 'center',
              justifyContent: 'center',
            }}>
            {icon}
          </View>
        ) : null}
      </View>
      <AppText variant="title" style={{ color: theme.colors.text, fontSize: 24, lineHeight: 30 }}>
        {value}
      </AppText>
      {hint ? (
        <AppText variant="caption" style={{ color: theme.colors.textMuted }} numberOfLines={2}>
          {hint}
        </AppText>
      ) : null}
    </View>
  );
}
