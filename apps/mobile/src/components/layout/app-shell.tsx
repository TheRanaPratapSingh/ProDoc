import { useRouter, type Href } from 'expo-router';
import { type ReactNode, useCallback } from 'react';
import { Pressable, useWindowDimensions, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import {
  IconAppointments,
  IconHome,
  IconProfile,
  IconRecords,
} from '@/components/ui/prodoc-icons';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { useHasPermission } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { breakpoint, spacing } from '@/theme/tokens';

export type PatientTab = 'home' | 'visits' | 'records' | 'profile' | 'doctors';

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
  const canBook = useHasPermission('appointments.book.self');
  const wide = width >= breakpoint.wide;

  const tabs: {
    id: Exclude<PatientTab, 'doctors'>;
    label: string;
    href: Href;
    visible: boolean;
  }[] = [
    { id: 'home', label: copy.navHome, href: '/home', visible: true },
    { id: 'visits', label: copy.navAppointments, href: '/visits', visible: canBook },
    { id: 'records', label: copy.navRecords, href: '/health-records', visible: true },
    { id: 'profile', label: copy.navProfile, href: '/home?tab=profile', visible: true },
  ];
  const visible = tabs.filter((tab) => tab.visible);
  const selectedId = active === 'doctors' ? 'home' : active;

  /** Skip no-op taps; use navigate so returning to a prior tab can reuse stack history. */
  const onSelectTab = useCallback(
    (tabId: Exclude<PatientTab, 'doctors'>, href: Href) => {
      if (tabId === selectedId) {
        return;
      }
      router.navigate(href);
    },
    [router, selectedId],
  );

  const renderIcon = (id: Exclude<PatientTab, 'doctors'>, selected: boolean) => {
    const color = selected ? theme.colors.primary : theme.colors.textMuted;
    switch (id) {
      case 'home':
        return <IconHome size={22} color={color} filled={selected} />;
      case 'visits':
        return <IconAppointments size={22} color={color} />;
      case 'records':
        return <IconRecords size={22} color={color} />;
      case 'profile':
        return <IconProfile size={22} color={color} />;
    }
  };

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
            const selected = tab.id === selectedId;
            return (
              <Pressable
                key={tab.id}
                onPress={() => onSelectTab(tab.id, tab.href)}
                style={{
                  paddingVertical: spacing.md,
                  paddingHorizontal: spacing.lg,
                  borderRadius: 14,
                  backgroundColor: selected ? theme.colors.primaryLight : 'transparent',
                  flexDirection: 'row',
                  alignItems: 'center',
                  gap: spacing.sm,
                }}>
                {renderIcon(tab.id, selected)}
                <AppText variant="label" style={{ color: selected ? theme.colors.primary : theme.colors.text }}>
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
          paddingBottom: Math.max(insets.bottom, 8),
          paddingTop: 10,
          minHeight: 56 + Math.max(insets.bottom, 8),
        }}>
        {visible.map((tab) => {
          const selected = tab.id === selectedId;
          return (
            <Pressable
              key={tab.id}
              accessibilityRole="button"
              accessibilityState={{ selected }}
              onPress={() => onSelectTab(tab.id, tab.href)}
              style={{ flex: 1, alignItems: 'center', gap: 4, paddingVertical: 4 }}>
              {renderIcon(tab.id, selected)}
              <AppText
                variant="caption"
                style={{
                  color: selected ? theme.colors.primary : theme.colors.textMuted,
                  textAlign: 'center',
                  fontSize: 11,
                  lineHeight: 14,
                }}>
                {tab.label}
              </AppText>
            </Pressable>
          );
        })}
      </View>
    </View>
  );
}
