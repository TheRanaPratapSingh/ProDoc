import { useRouter, type Href } from 'expo-router';
import { type ReactNode, useCallback } from 'react';
import { Pressable, ScrollView, useWindowDimensions, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { NetworkBanner } from '@/components/layout/network-banner';
import {
  IconAppointments,
  IconCalendarPlus,
  IconHome,
  IconMoreGrid,
  IconPeople,
} from '@/components/ui/prodoc-icons';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { breakpoint, spacing } from '@/theme/tokens';

export type DoctorTab =
  | 'dashboard'
  | 'appointments'
  | 'appointmentsQueue'
  | 'schedule'
  | 'patients'
  | 'more'
  | 'calendar'
  | 'availability'
  | 'queue'
  | 'prescriptions'
  | 'reviews'
  | 'earnings'
  | 'profile'
  | 'notifications'
  | 'settings'
  | 'help';

type NavItem = {
  id: DoctorTab;
  label: string;
  href: Href;
  primary?: boolean;
};

function resolvePrimary(active: DoctorTab): DoctorTab {
  if (
    active === 'dashboard' ||
    active === 'appointmentsQueue' ||
    active === 'schedule' ||
    active === 'patients' ||
    active === 'more'
  ) {
    return active;
  }
  if (active === 'appointments' || active === 'queue' || active === 'calendar') return 'appointmentsQueue';
  if (active === 'availability') return 'schedule';
  return 'more';
}

export function DoctorShell({ children, active }: { children: ReactNode; active: DoctorTab }) {
  const theme = useProdocTheme();
  const copy = useCopy();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const { width } = useWindowDimensions();
  const wide = width >= breakpoint.wide;
  const selectedPrimary = resolvePrimary(active);

  const primary: NavItem[] = [
    { id: 'dashboard', label: copy.doctorNavDashboard, href: '/doctor-home', primary: true },
    { id: 'appointmentsQueue', label: copy.doctorNavAppointmentsQueue, href: '/doctor-queue', primary: true },
    { id: 'schedule', label: copy.doctorNavSchedule, href: '/doctor-schedule', primary: true },
    { id: 'patients', label: copy.doctorNavPatients, href: '/doctor-patients', primary: true },
    { id: 'more', label: copy.doctorMoreMenu, href: '/doctor-more', primary: true },
  ];

  const sidebarExtras: NavItem[] = [
    { id: 'availability', label: copy.doctorNavAvailability, href: '/schedule' },
    { id: 'appointments', label: copy.doctorNavAppointments, href: '/practice' },
    { id: 'prescriptions', label: copy.doctorNavPrescriptions, href: '/doctor-prescriptions' },
    { id: 'reviews', label: copy.doctorNavReviews, href: '/review-inbox' },
    { id: 'earnings', label: copy.doctorNavEarnings, href: '/doctor-earnings' },
    { id: 'notifications', label: copy.doctorNavNotifications, href: '/doctor-notifications' },
    { id: 'profile', label: copy.doctorNavProfile, href: '/doctor' },
    { id: 'settings', label: copy.doctorNavSettings, href: '/doctor-settings' },
    { id: 'help', label: copy.doctorNavHelp, href: '/help' },
  ];

  const onSelect = useCallback(
    (href: Href, id: DoctorTab) => {
      if (id === active) return;
      if (id === selectedPrimary && resolvePrimary(active) === id) {
        const currentPrimary = primary.find((item) => item.id === id);
        if (currentPrimary && currentPrimary.href === href) return;
      }
      router.navigate(href);
    },
    [active, primary, router, selectedPrimary],
  );

  const renderIcon = (id: DoctorTab, selected: boolean) => {
    const color = selected ? theme.colors.primary : theme.colors.textMuted;
    switch (id) {
      case 'dashboard':
        return <IconHome size={22} color={color} filled={selected} />;
      case 'appointments':
      case 'appointmentsQueue':
      case 'queue':
        return <IconAppointments size={22} color={color} />;
      case 'schedule':
      case 'calendar':
      case 'availability':
        return <IconCalendarPlus size={22} color={color} />;
      case 'patients':
        return <IconPeople size={22} color={color} />;
      case 'more':
        return <IconMoreGrid size={22} color={color} />;
      default:
        return <IconMoreGrid size={22} color={color} />;
    }
  };

  if (wide) {
    const allItems = [...primary, ...sidebarExtras];
    return (
      <View style={{ flex: 1, flexDirection: 'row', backgroundColor: theme.colors.background }}>
        <ScrollView
          style={{
            width: 260,
            borderRightWidth: 1,
            borderRightColor: theme.colors.border,
            backgroundColor: theme.colors.surface,
          }}
          contentContainerStyle={{
            paddingTop: spacing.xxl,
            paddingHorizontal: spacing.lg,
            gap: spacing.sm,
            paddingBottom: spacing.xxl,
          }}>
          <AppText variant="headline" style={{ color: theme.colors.primary, marginBottom: spacing.md }}>
            {copy.brand}
          </AppText>
          <AppText variant="caption" style={{ color: theme.colors.textMuted, marginBottom: spacing.sm }}>
            {copy.doctorPortalLabel}
          </AppText>
          {allItems.map((item) => {
            const selected = item.id === active || Boolean(item.primary && item.id === selectedPrimary);
            return (
              <Pressable
                key={item.id}
                onPress={() => onSelect(item.href, item.id)}
                style={{
                  paddingVertical: spacing.md,
                  paddingHorizontal: spacing.lg,
                  borderRadius: 14,
                  backgroundColor: selected ? theme.colors.primaryLight : 'transparent',
                  flexDirection: 'row',
                  alignItems: 'center',
                  gap: spacing.sm,
                }}>
                {renderIcon(item.id, selected)}
                <AppText variant="label" style={{ color: selected ? theme.colors.primary : theme.colors.text }}>
                  {item.label}
                </AppText>
              </Pressable>
            );
          })}
        </ScrollView>
        <View style={{ flex: 1 }}>
          <NetworkBanner />
          {children}
        </View>
      </View>
    );
  }

  return (
    <View style={{ flex: 1, backgroundColor: theme.colors.background }}>
      <NetworkBanner />
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
        {primary.map((item) => {
          const selected = item.id === selectedPrimary;
          return (
            <Pressable
              key={item.id}
              accessibilityRole="button"
              accessibilityState={{ selected }}
              onPress={() => onSelect(item.href, item.id)}
              style={{ flex: 1, alignItems: 'center', gap: 4, paddingVertical: 4 }}>
              {renderIcon(item.id, selected)}
              <AppText
                variant="caption"
                style={{
                  color: selected ? theme.colors.primary : theme.colors.textMuted,
                  textAlign: 'center',
                  fontSize: 10,
                  lineHeight: 12,
                }}>
                {item.label}
              </AppText>
            </Pressable>
          );
        })}
      </View>
    </View>
  );
}
