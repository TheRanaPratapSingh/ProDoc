import { type ReactNode } from 'react';
import { useWindowDimensions, View } from 'react-native';
import { SafeAreaView, type Edge } from 'react-native-safe-area-context';

import { FormScreenScroll } from '@/components/layout/keyboard-aware-form';
import { NetworkBanner } from '@/components/layout/network-banner';
import { HealthcareHero } from '@/components/ui/healthcare-hero';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { breakpoint, spacing } from '@/theme/tokens';

export function Screen({
  children,
  edges = ['top', 'left', 'right'],
}: {
  children: ReactNode;
  /** Default excludes bottom so AppShell tab bar owns the home-indicator inset. */
  edges?: Edge[];
}) {
  const theme = useProdocTheme();

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: theme.colors.background }} edges={edges}>
      <NetworkBanner />
      {/* Keyboard avoidance lives in FormScreenScroll / KeyboardAwareForm — avoid nesting avoiders here. */}
      <View style={{ flex: 1 }}>{children}</View>
    </SafeAreaView>
  );
}

export function AuthScaffold({
  children,
  showHero = false,
}: {
  children: ReactNode;
  showHero?: boolean;
}) {
  const theme = useProdocTheme();
  const copy = useCopy();
  const { width } = useWindowDimensions();
  const wide = width >= breakpoint.wide;

  if (!wide) {
    return (
      <Screen>
        <FormScreenScroll
          contentContainerStyle={{ flexGrow: 1, justifyContent: 'center', padding: spacing.xl }}
          mode="layout">
          <View style={{ width: '100%', maxWidth: 440, alignSelf: 'center', gap: spacing.xl }}>
            {showHero ? (
              <View style={{ gap: spacing.md, alignItems: 'center' }}>
                <AppText variant="headline" style={{ color: theme.colors.primary }}>
                  {copy.brand}
                </AppText>
                <HealthcareHero height={132} variant="illustration" />
              </View>
            ) : null}
            {children}
          </View>
        </FormScreenScroll>
      </Screen>
    );
  }

  return (
    <View style={{ flex: 1, flexDirection: 'row', backgroundColor: theme.colors.background }}>
      <View
        style={{
          flex: 1.05,
          backgroundColor: theme.colors.softBlue,
          justifyContent: 'center',
          padding: spacing.xxxl,
          gap: spacing.xl,
        }}>
        <AppText variant="display" style={{ color: theme.colors.primary }}>
          {copy.brand}
        </AppText>
        <AppText variant="title">{copy.loginHeadline}</AppText>
        <AppText variant="muted">{copy.loginSubtitle}</AppText>
        <HealthcareHero height={200} variant="illustration" />
      </View>
      <SafeAreaView style={{ flex: 1, backgroundColor: theme.colors.background }}>
        <FormScreenScroll
          contentContainerStyle={{ flexGrow: 1, justifyContent: 'center', padding: spacing.xxxl }}
          mode="layout">
          <View style={{ width: '100%', maxWidth: 440, alignSelf: 'center' }}>{children}</View>
        </FormScreenScroll>
      </SafeAreaView>
    </View>
  );
}
