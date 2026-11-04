import { type ReactNode } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, useWindowDimensions, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { NetworkBanner } from '@/components/layout/network-banner';
import { HealthcareHero } from '@/components/ui/healthcare-hero';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { breakpoint, spacing } from '@/theme/tokens';

export function Screen({ children }: { children: ReactNode }) {
  const theme = useProdocTheme();

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: theme.colors.background }}>
      <NetworkBanner />
      <KeyboardAvoidingView
        style={{ flex: 1 }}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        keyboardVerticalOffset={Platform.OS === 'ios' ? 8 : 0}>
        {children}
      </KeyboardAvoidingView>
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
        <ScrollView
          contentContainerStyle={{ flexGrow: 1, justifyContent: 'center', padding: spacing.xl }}
          keyboardShouldPersistTaps="handled">
          <View style={{ width: '100%', maxWidth: 440, alignSelf: 'center', gap: spacing.xl }}>
            {showHero ? (
              <View style={{ gap: spacing.md, alignItems: 'center' }}>
                <AppText variant="headline" style={{ color: theme.colors.primary }}>
                  {copy.brand}
                </AppText>
                <HealthcareHero height={150} />
              </View>
            ) : null}
            {children}
          </View>
        </ScrollView>
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
        <HealthcareHero height={220} />
      </View>
      <SafeAreaView style={{ flex: 1, backgroundColor: theme.colors.background }}>
        <ScrollView
          contentContainerStyle={{ flexGrow: 1, justifyContent: 'center', padding: spacing.xxxl }}
          keyboardShouldPersistTaps="handled">
          <View style={{ width: '100%', maxWidth: 440, alignSelf: 'center' }}>{children}</View>
        </ScrollView>
      </SafeAreaView>
    </View>
  );
}
