import { View, type ViewProps } from 'react-native';

import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, shadow, spacing } from '@/theme/tokens';

export function Card({ style, elevated = false, ...props }: ViewProps & { elevated?: boolean }) {
  const theme = useProdocTheme();
  const shadows = shadow[theme.scheme];

  return (
    <View
      {...props}
      style={[
        {
          backgroundColor: theme.colors.surface,
          borderRadius: radius.lg,
          borderWidth: 1,
          borderColor: theme.colors.border,
          padding: spacing.xl,
          gap: spacing.lg,
          boxShadow: elevated ? shadows.elevated : shadows.card,
        },
        style,
      ]}
    />
  );
}
