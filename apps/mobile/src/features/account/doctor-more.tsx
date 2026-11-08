import { useQuery } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { Pressable, ScrollView, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { DoctorShell } from '@/components/layout/doctor-shell';
import { Screen } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import {
  IconBell,
  IconCalendarPlus,
  IconChevronRight,
  IconClinic,
  IconHealthRecords,
  IconPeople,
  IconProfile,
  IconStar,
} from '@/components/ui/prodoc-icons';
import { AppText } from '@/components/ui/text';
import { clinicDateIso } from '@/features/providers/schedule-day';
import { useCopy } from '@/i18n/copy';
import { getDoctorDashboard } from '@/services/api/doctor-portal-api';
import { clearSession, useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';

type MoreItem = {
  key: string;
  title: string;
  subtitle: string;
  href: Href;
  icon: 'queue' | 'clinics' | 'reviews' | 'alerts' | 'profile' | 'rx' | 'fees' | 'settings' | 'help' | 'hours';
  badge?: number;
};

function MenuIcon({ kind, color }: { kind: MoreItem['icon']; color: string }) {
  switch (kind) {
    case 'queue':
      return <IconPeople size={20} color={color} />;
    case 'clinics':
      return <IconClinic size={20} color={color} />;
    case 'reviews':
      return <IconStar size={20} color={color} />;
    case 'alerts':
      return <IconBell size={20} color={color} />;
    case 'profile':
      return <IconProfile size={20} color={color} />;
    case 'rx':
      return <IconHealthRecords size={20} color={color} />;
    case 'hours':
      return <IconCalendarPlus size={20} color={color} />;
    default:
      return <IconProfile size={20} color={color} />;
  }
}

export function DoctorMoreScreen() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const dashboard = useQuery({ queryKey: ['doctor', 'dashboard', 'more'], queryFn: () => getDoctorDashboard(null) });
  const unread = dashboard.data?.unreadNotifications ?? 0;

  const items: MoreItem[] = [
    {
      key: 'queue',
      title: copy.appointmentsAndQueue,
      subtitle: copy.doctorMoreQueueHint,
      href: '/doctor-queue',
      icon: 'queue',
    },
    {
      key: 'hours',
      title: copy.doctorNavAvailability,
      subtitle: copy.doctorMoreHoursHint,
      href: `/doctor-today-timing?date=${clinicDateIso()}` as Href,
      icon: 'hours',
    },
    {
      key: 'clinics',
      title: copy.doctorMoreClinics,
      subtitle: copy.doctorMoreClinicsHint,
      href: '/doctor',
      icon: 'clinics',
    },
    {
      key: 'rx',
      title: copy.doctorNavPrescriptions,
      subtitle: copy.doctorMoreRxHint,
      href: '/doctor-prescriptions',
      icon: 'rx',
    },
    {
      key: 'followups',
      title: 'Follow-up Management',
      subtitle: 'Upcoming, due, overdue, and cancelled recommendations',
      href: '/doctor-follow-ups',
      icon: 'hours',
    },
    {
      key: 'reviews',
      title: copy.doctorNavReviews,
      subtitle: copy.doctorMoreReviewsHint,
      href: '/review-inbox',
      icon: 'reviews',
    },
    {
      key: 'alerts',
      title: copy.doctorNavNotifications,
      subtitle: copy.doctorMoreAlertsHint,
      href: '/doctor-notifications',
      icon: 'alerts',
      badge: unread,
    },
    {
      key: 'profile',
      title: copy.doctorNavProfile,
      subtitle: copy.doctorMoreProfileHint,
      href: '/doctor',
      icon: 'profile',
    },
    {
      key: 'fees',
      title: copy.doctorMoreFees,
      subtitle: copy.doctorMoreFeesHint,
      href: '/doctor-earnings',
      icon: 'fees',
    },
    {
      key: 'settings',
      title: copy.doctorNavSettings,
      subtitle: copy.doctorMoreSettingsHint,
      href: '/doctor-settings',
      icon: 'settings',
    },
    {
      key: 'help',
      title: copy.doctorNavHelp,
      subtitle: copy.doctorMoreHelpHint,
      href: '/help',
      icon: 'help',
    },
  ];

  return (
    <DoctorShell active="more">
      <Screen>
        <ScrollView
          contentContainerStyle={{
            gap: spacing.md,
            paddingBottom: spacing.xxl,
            paddingTop: Math.max(insets.top - 8, 0),
          }}>
          <ScreenHeader title={copy.doctorMoreMenu} />
          <View
            style={{
              backgroundColor: theme.colors.surface,
              borderRadius: 18,
              borderWidth: 1,
              borderColor: theme.colors.border,
              overflow: 'hidden',
            }}>
            {items.map((item, index) => (
              <Pressable
                key={item.key}
                onPress={() => router.push(item.href)}
                style={{
                  flexDirection: 'row',
                  alignItems: 'center',
                  gap: spacing.md,
                  paddingHorizontal: spacing.lg,
                  paddingVertical: spacing.md + 2,
                  borderTopWidth: index === 0 ? 0 : 1,
                  borderTopColor: theme.colors.border,
                }}>
                <View
                  style={{
                    width: 40,
                    height: 40,
                    borderRadius: 20,
                    backgroundColor: theme.colors.primaryLight,
                    alignItems: 'center',
                    justifyContent: 'center',
                  }}>
                  <MenuIcon kind={item.icon} color={theme.colors.primary} />
                </View>
                <View style={{ flex: 1, gap: 2 }}>
                  <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm }}>
                    <AppText variant="label">{item.title}</AppText>
                    {item.badge && item.badge > 0 ? (
                      <View
                        style={{
                          minWidth: 20,
                          height: 20,
                          borderRadius: 10,
                          backgroundColor: theme.colors.error,
                          alignItems: 'center',
                          justifyContent: 'center',
                          paddingHorizontal: 5,
                        }}>
                        <AppText style={{ color: '#FFFFFF', fontSize: 11, fontWeight: '700' }}>
                          {item.badge > 99 ? '99+' : item.badge}
                        </AppText>
                      </View>
                    ) : null}
                  </View>
                  <AppText variant="caption" style={{ color: theme.colors.textMuted }} numberOfLines={2}>
                    {item.subtitle}
                  </AppText>
                </View>
                <IconChevronRight size={18} color={theme.colors.textMuted} />
              </Pressable>
            ))}
          </View>

          <Pressable
            onPress={async () => {
              await clearSession();
              useSessionStore.getState();
              router.replace('/sign-in');
            }}
            style={{
              marginTop: spacing.sm,
              backgroundColor: theme.colors.surface,
              borderRadius: 16,
              borderWidth: 1,
              borderColor: theme.colors.border,
              padding: spacing.lg,
              alignItems: 'center',
            }}>
            <AppText variant="label" style={{ color: theme.colors.error }}>
              {copy.signOut}
            </AppText>
          </Pressable>
        </ScrollView>
      </Screen>
    </DoctorShell>
  );
}
