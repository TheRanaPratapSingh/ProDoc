import { View, type ViewProps } from 'react-native';

import { cardElevation } from '@/theme/elevation';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';

export function Card({ style, elevated = false, ...props }: ViewProps & { elevated?: boolean }) {
  const theme = useProdocTheme();

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
        },
        cardElevation(theme.scheme, elevated ? 'elevated' : 'card'),
        style,
      ]}
    />
  );
}
