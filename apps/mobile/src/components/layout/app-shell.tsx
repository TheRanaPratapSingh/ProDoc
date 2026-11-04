import { useRouter, type Href } from 'expo-router';
import { type ReactNode } from 'react';
import { Pressable, useWindowDimensions, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { breakpoint, spacing } from '@/theme/tokens';

export type PatientTab = 'home' | 'doctors' | 'visits' | 'queue' | 'records';

export function AppShell({
  children,
  active,
}: {
  children: ReactNode;
  active: PatientTab;
}) {
  const theme = useProdocTheme();
  const copy = useCopy();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const { width } = useWindowDimensions();
  const permissions = useSessionStore((state) => state.account?.permissions ?? []);
  const canBook = permissions.includes('appointments.book.self');
  const wide = width >= breakpoint.wide;

  const tabs: { id: PatientTab; label: string; href: Href; visible: boolean }[] = [
    { id: 'home', label: copy.homeTitle, href: '/home', visible: true },
    { id: 'doctors', label: copy.findDoctor, href: '/doctors', visible: true },
    { id: 'visits', label: copy.myVisits, href: '/visits', visible: canBook },
    { id: 'queue', label: copy.myQueue, href: '/queue', visible: canBook },
    { id: 'records', label: copy.healthRecords, href: '/health-records', visible: canBook },
  ];
  const visible = tabs.filter((tab) => tab.visible);

  if (wide) {
    return (
      <View style={{ flex: 1, flexDirection: 'row', backgroundColor: theme.colors.background }}>
        <View
          style={{
            width: 240,
            paddingTop: spacing.xxl,
            paddingHorizontal: spacing.lg,
            gap: spacing.sm,
            borderRightWidth: 1,
            borderRightColor: theme.colors.border,
            backgroundColor: theme.colors.surface,
          }}>
          <AppText variant="headline" style={{ color: theme.colors.primary, marginBottom: spacing.md }}>
            {copy.brand}
          </AppText>
          {visible.map((tab) => {
            const selected = tab.id === active;
            return (
              <Pressable
                key={tab.id}
                onPress={() => router.push(tab.href)}
                style={{
                  paddingVertical: spacing.md,
                  paddingHorizontal: spacing.lg,
                  borderRadius: 14,
                  backgroundColor: selected ? theme.colors.primary : 'transparent',
                }}>
                <AppText variant="label" style={{ color: selected ? theme.colors.onPrimary : theme.colors.text }}>
                  {tab.label}
                </AppText>
              </Pressable>
            );
          })}
        </View>
        <View style={{ flex: 1 }}>{children}</View>
      </View>
    );
  }

  return (
    <View style={{ flex: 1, backgroundColor: theme.colors.background }}>
      <View style={{ flex: 1 }}>{children}</View>
      <View
        style={{
          flexDirection: 'row',
          borderTopWidth: 1,
          borderTopColor: theme.colors.border,
          backgroundColor: theme.colors.surface,
          paddingBottom: Math.max(insets.bottom, spacing.sm),
          paddingTop: spacing.sm,
        }}>
        {visible.map((tab) => {
          const selected = tab.id === active;
          return (
            <Pressable
              key={tab.id}
              onPress={() => router.push(tab.href)}
              style={{ flex: 1, alignItems: 'center', gap: 4, paddingVertical: spacing.sm }}>
              <View
                style={{
                  width: 28,
                  height: 4,
                  borderRadius: 999,
                  backgroundColor: selected ? theme.colors.primary : 'transparent',
                }}
              />
              <AppText
                variant="caption"
                style={{ color: selected ? theme.colors.primary : theme.colors.textMuted, textAlign: 'center' }}>
                {tab.label}
              </AppText>
            </Pressable>
          );
        })}
      </View>
    </View>
  );
}
