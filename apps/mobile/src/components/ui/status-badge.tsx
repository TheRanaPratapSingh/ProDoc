import { View } from 'react-native';

import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';

type Tone = 'neutral' | 'primary' | 'success' | 'warning' | 'error';

export function StatusBadge({ label, tone = 'neutral' }: { label: string; tone?: Tone }) {
  const theme = useProdocTheme();
  const styles = {
    neutral: { bg: theme.colors.softBlue, fg: theme.colors.text },
    primary: { bg: theme.colors.primaryLight, fg: theme.colors.primary },
    success: { bg: theme.scheme === 'light' ? '#E6F4EC' : '#1A3328', fg: theme.colors.success },
    warning: { bg: theme.scheme === 'light' ? '#FBF3E6' : '#332818', fg: theme.colors.warning },
    error: { bg: theme.scheme === 'light' ? '#FCEAEA' : '#331818', fg: theme.colors.error },
  }[tone];

  return (
    <View
      style={{
        alignSelf: 'flex-start',
        backgroundColor: styles.bg,
        borderRadius: radius.pill,
        paddingHorizontal: spacing.md,
        paddingVertical: spacing.xs,
      }}>
      <AppText variant="caption" style={{ color: styles.fg }}>
        {label}
      </AppText>
    </View>
  );
}
