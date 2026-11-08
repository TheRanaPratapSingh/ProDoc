import { useRouter, type Href } from 'expo-router';
import { type ReactNode, useCallback, useState } from 'react';
import { Modal, Pressable, ScrollView, useWindowDimensions, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { NetworkBanner } from '@/components/layout/network-banner';
import {
  IconAppointments,
  IconBell,
  IconCalendarPlus,
  IconClinic,
  IconHealthRecords,
  IconHome,
  IconProfile,
} from '@/components/ui/prodoc-icons';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { breakpoint, spacing } from '@/theme/tokens';

export type ReceptionTab =
  | 'dashboard'
  | 'appointments'
  | 'appointmentsQueue'
  | 'queue'
  | 'patients'
  | 'billing'
  | 'doctors'
  | 'calendar'
  | 'availability'
  | 'clinic'
  | 'reviews'
  | 'reports'
  | 'records'
  | 'notifications'
  | 'settings'
  | 'desk';

type NavItem = {
  id: ReceptionTab;
  label: string;
  href: Href;
  primary?: boolean;
};

export function ReceptionShell({ children, active }: { children: ReactNode; active: ReceptionTab }) {
  const theme = useProdocTheme();
  const copy = useCopy();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const { width } = useWindowDimensions();
  const wide = width >= breakpoint.wide;
  const [menuOpen, setMenuOpen] = useState(false);

  const items: NavItem[] = [
    { id: 'dashboard', label: copy.receptionNavDashboard, href: '/reception', primary: true },
    { id: 'appointmentsQueue', label: copy.receptionNavAppointmentsQueue, href: '/reception-queue', primary: true },
    { id: 'patients', label: copy.receptionNavPatients, href: '/reception-patients', primary: true },
    { id: 'billing', label: copy.receptionNavBilling, href: '/reception-billing', primary: true },
    { id: 'desk', label: copy.receptionNavDesk, href: '/reception-desk' },
    { id: 'appointments', label: copy.receptionNavAppointments, href: '/reception-appointments' },
    { id: 'doctors', label: copy.receptionNavDoctors, href: '/reception-doctors' },
    { id: 'calendar', label: copy.receptionNavCalendar, href: '/reception-calendar' },
    { id: 'availability', label: copy.receptionNavAvailability, href: '/reception-availability' },
    { id: 'clinic', label: copy.receptionNavClinic, href: '/reception-clinic' },
    { id: 'reviews', label: copy.receptionNavReviews, href: '/reception-reviews' },
    { id: 'reports', label: copy.receptionNavReports, href: '/reception-reports' },
    { id: 'records', label: copy.receptionNavRecords, href: '/reception-records' },
    { id: 'notifications', label: copy.receptionNavNotifications, href: '/reception-notifications' },
    { id: 'settings', label: copy.receptionNavSettings, href: '/reception-settings' },
  ];

  const primary = items.filter((item) => item.primary);
  const resolvedActive: ReceptionTab =
    active === 'queue' || active === 'calendar' || active === 'appointments' ? 'appointmentsQueue' : active;
  const onSelect = useCallback(
    (href: Href, id: ReceptionTab) => {
      setMenuOpen(false);
      if (id === active) return;
      router.navigate(href);
    },
    [active, router],
  );

  const renderIcon = (id: ReceptionTab, selected: boolean) => {
    const color = selected ? theme.colors.primary : theme.colors.textMuted;
    switch (id) {
      case 'dashboard':
        return <IconHome size={22} color={color} filled={selected} />;
      case 'appointments':
      case 'appointmentsQueue':
      case 'queue':
      case 'calendar':
        return <IconAppointments size={22} color={color} />;
      case 'availability':
        return <IconCalendarPlus size={22} color={color} />;
      case 'desk':
        return <IconClinic size={22} color={color} />;
      case 'patients':
      case 'settings':
        return <IconProfile size={22} color={color} />;
      case 'notifications':
        return <IconBell size={22} color={color} />;
      case 'records':
        return <IconHealthRecords size={22} color={color} />;
      default:
        return <IconClinic size={22} color={color} />;
    }
  };

  const navList = (
    <>
      <AppText variant="headline" style={{ color: theme.colors.primary, marginBottom: spacing.md }}>
        {copy.brand}
      </AppText>
      <AppText variant="caption" style={{ color: theme.colors.textMuted, marginBottom: spacing.sm }}>
        {copy.receptionPortalLabel}
      </AppText>
      {items.map((item) => {
        const selected = item.id === active || item.id === resolvedActive;
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
    </>
  );

  if (wide) {
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
          {navList}
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
      <View
        style={{
          paddingTop: Math.max(insets.top, 8),
          paddingHorizontal: spacing.lg,
          paddingBottom: spacing.sm,
          backgroundColor: theme.colors.surface,
          borderBottomWidth: 1,
          borderBottomColor: theme.colors.border,
          flexDirection: 'row',
          alignItems: 'center',
          justifyContent: 'space-between',
        }}>
        <AppText variant="label" style={{ color: theme.colors.primary }}>
          {copy.receptionPortalLabel}
        </AppText>
        <Pressable onPress={() => setMenuOpen(true)} hitSlop={12}>
          <AppText variant="label" style={{ color: theme.colors.primary }}>
            {copy.receptionMoreMenu}
          </AppText>
        </Pressable>
      </View>
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
          const selected = item.id === resolvedActive || item.id === active;
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
      <Modal visible={menuOpen} animationType="slide" transparent onRequestClose={() => setMenuOpen(false)}>
        <Pressable style={{ flex: 1, backgroundColor: theme.colors.overlay }} onPress={() => setMenuOpen(false)} />
        <View
          style={{
            maxHeight: '70%',
            backgroundColor: theme.colors.surface,
            borderTopLeftRadius: 20,
            borderTopRightRadius: 20,
            paddingBottom: Math.max(insets.bottom, 16),
          }}>
          <ScrollView contentContainerStyle={{ padding: spacing.lg, gap: spacing.sm }}>{navList}</ScrollView>
        </View>
      </Modal>
    </View>
  );
}
