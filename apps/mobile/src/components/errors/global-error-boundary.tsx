import { Component, type ErrorInfo, type ReactNode } from 'react';
import { Pressable, StyleSheet, View } from 'react-native';

import { AppText } from '@/components/ui/text';
import { RetryButton } from '@/components/ui/retry-button';
import { palette, radius, spacing } from '@/theme/tokens';

type Props = {
  children: ReactNode;
  onReset?: () => void;
};

type State = {
  hasError: boolean;
  message: string;
};

/**
 * Top-level React error boundary. Class component is required by React.
 * Keeps the app shell alive with a calm Earth Green recovery UI.
 */
export class GlobalErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false, message: '' };

  static getDerivedStateFromError(error: Error): State {
    return {
      hasError: true,
      message: error?.message?.trim() || 'Something unexpected happened.',
    };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    if (__DEV__) {
      console.error('[ProDoc] GlobalErrorBoundary', error, info.componentStack);
    }
  }

  private reset = () => {
    this.setState({ hasError: false, message: '' });
    this.props.onReset?.();
  };

  render() {
    if (!this.state.hasError) {
      return this.props.children;
    }

    return (
      <View style={styles.screen} accessibilityRole="alert">
        <View style={styles.card}>
          <AppText variant="title" style={styles.title}>
            We hit a snag
          </AppText>
          <AppText variant="muted" style={styles.body}>
            ProDoc stayed open so you can continue. Your work on this screen may need a quick refresh.
          </AppText>
          {__DEV__ && this.state.message ? (
            <AppText variant="caption" style={styles.dev}>
              {this.state.message}
            </AppText>
          ) : null}
          <RetryButton label="Reload screen" onPress={this.reset} />
          <Pressable onPress={this.reset} hitSlop={8} style={styles.linkWrap}>
            <AppText variant="caption" style={styles.link}>
              Dismiss
            </AppText>
          </Pressable>
        </View>
      </View>
    );
  }
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
  title: {
    color: palette.light.text,
    textAlign: 'center',
  },
  body: {
    color: palette.light.textMuted,
    textAlign: 'center',
  },
  dev: {
    color: palette.light.error,
    textAlign: 'center',
  },
  linkWrap: {
    alignItems: 'center',
    paddingVertical: spacing.sm,
  },
  link: {
    color: palette.light.primary,
    fontWeight: '600',
  },
});
