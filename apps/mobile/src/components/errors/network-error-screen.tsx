import { StyleSheet, View } from 'react-native';

import { AppText } from '@/components/ui/text';
import { RetryButton } from '@/components/ui/retry-button';
import { palette, radius, spacing } from '@/theme/tokens';

export function NetworkErrorScreen({
  title = 'Connection problem',
  body = 'We could not reach the ProDoc servers. Check your network and try again.',
  actionLabel = 'Try again',
  loading = false,
  onRetry,
}: {
  title?: string;
  body?: string;
  actionLabel?: string;
  loading?: boolean;
  onRetry: () => void;
}) {
  return (
    <View style={styles.screen} accessibilityRole="alert">
      <View style={styles.card}>
        <View style={styles.badge}>
          <AppText variant="label" style={styles.badgeText}>
            Offline
          </AppText>
        </View>
        <AppText variant="title" style={styles.title}>
          {title}
        </AppText>
        <AppText variant="muted" style={styles.body}>
          {body}
        </AppText>
        <RetryButton label={actionLabel} loading={loading} onPress={onRetry} />
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  screen: {
    flex: 1,
    backgroundColor: palette.light.background,
    justifyContent: 'center',
    padding: spacing.xl,
  },
  card: {
    backgroundColor: palette.light.surface,
    borderRadius: radius.xl,
    borderWidth: 1,
    borderColor: palette.light.border,
    padding: spacing.xl,
    gap: spacing.md,
  },
  badge: {
    alignSelf: 'center',
    backgroundColor: palette.light.primaryLight,
    borderRadius: radius.pill,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.xs,
  },
  badgeText: {
    color: palette.light.primary,
  },
  title: {
    color: palette.light.text,
    textAlign: 'center',
  },
  body: {
    color: palette.light.textMuted,
    textAlign: 'center',
  },
});
