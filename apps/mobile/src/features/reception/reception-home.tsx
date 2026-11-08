import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { type ReactNode, useCallback, useMemo } from 'react';
import { Pressable, RefreshControl, ScrollView, useWindowDimensions, View } from 'react-native';
import Animated, { FadeInDown } from 'react-native-reanimated';

import { ReceptionClinicSwitcher, useReceptionDeskSelection } from '@/components/reception/clinic-switcher';
import { ReceptionShell } from '@/components/layout/reception-shell';
import { Screen } from '@/components/layout/auth-scaffold';
import { AppointmentCard } from '@/components/ui/appointment-card';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { EmptyState } from '@/components/ui/empty-state';
import { NotificationBell } from '@/components/ui/notification-bell';
import {
  IconAppointments,
  IconCalendarPlus,
  IconClinic,
  IconProfile,
} from '@/components/ui/prodoc-icons';
import { PressableScale } from '@/components/ui/pressable-scale';
import { ProfileAvatar } from '@/components/ui/profile-avatar';
import { SectionHeader } from '@/components/ui/section-header';
import { AppText } from '@/components/ui/text';
import { useManualRefresh } from '@/hooks/use-manual-refresh';
import { useCopy } from '@/i18n/copy';
import { getReceptionDashboard, type ReceptionAppointment } from '@/services/api/reception-api';
import { checkInAppointment } from '@/services/api/queue-api';
import { useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { duration, layout, motion, radius, spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

function greeting(copy: ReturnType<typeof useCopy>) {
  const hour = new Date().getHours();
  if (hour < 12) return copy.goodMorning;
  if (hour < 17) return copy.goodAfternoon;
  return copy.goodEvening;
}

function formatTime(iso: string) {
  return new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}

function statusTone(status: string): 'neutral' | 'primary' | 'success' | 'warning' | 'error' {
  if (status === 'Completed') return 'success';
  if (status === 'CheckedIn') return 'primary';
  if (status === 'Cancelled' || status === 'Rejected' || status === 'NoShow') return 'error';
  if (status === 'Requested') return 'warning';
  return 'neutral';
}

function canCheckIn(status: string) {
  return status === 'Confirmed' || status === 'Requested';
}

function CompactStat({ label, value }: { label: string; value: string }) {
  const theme = useProdocTheme();
  return (
    <View
      style={{
        flexGrow: 1,
        flexBasis: '30%',
        minWidth: 96,
        backgroundColor: theme.colors.surface,
        borderRadius: radius.md,
        borderWidth: 1,
        borderColor: theme.colors.border,
        borderLeftWidth: 3,
        borderLeftColor: theme.colors.primary,
        paddingHorizontal: spacing.md,
        paddingVertical: spacing.sm + 2,
        gap: 2,
      }}>
      <AppText variant="caption" style={{ color: theme.colors.textMuted }} numberOfLines={1}>
        {label}
      </AppText>
      <AppText variant="headline" style={{ color: theme.colors.primary, fontSize: 22, lineHeight: 28 }}>
        {value}
      </AppText>
    </View>
  );
}

function QuickActionTile({
  label,
  icon,
  onPress,
  width,
}: {
  label: string;
  icon: ReactNode;
  onPress: () => void;
  width: number;
}) {
  const theme = useProdocTheme();
  return (
    <PressableScale
      onPress={onPress}
      scaleTo={motion.pressScale}
      contentStyle={{
        width,
        minHeight: 88,
        backgroundColor: theme.colors.surface,
        borderRadius: 16,
        borderWidth: 1,
        borderColor: theme.colors.border,
        paddingHorizontal: 12,
        paddingVertical: 12,
        gap: 10,
        justifyContent: 'space-between',
      }}>
      <View
        style={{
          width: 36,
          height: 36,
          borderRadius: 12,
          backgroundColor: theme.colors.primaryLight,
          alignItems: 'center',
          justifyContent: 'center',
        }}>
        {icon}
      </View>
      <AppText variant="label" style={{ fontSize: 13, lineHeight: 17, color: theme.colors.text }} numberOfLines={2}>
        {label}
      </AppText>
    </PressableScale>
  );
}

function StatSkeleton() {
  const theme = useProdocTheme();
  return (
    <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
      {[0, 1, 2, 3].map((item) => (
        <View
          key={item}
          style={{
            flexGrow: 1,
            flexBasis: '30%',
            minWidth: 96,
            minHeight: 64,
            borderRadius: radius.md,
            backgroundColor: theme.colors.primaryLight,
            opacity: 0.55,
          }}
        />
      ))}
    </View>
  );
}

export function ReceptionHome() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { width } = useWindowDimensions();
  const account = useSessionStore((state) => state.account);
  const { clinicId, branchId, clinic, branch, clinicsQuery } = useReceptionDeskSelection();
  const reduceMotion = theme.reduceMotion;
  const contentWidth = Math.min(width, layout.phoneMax);
  const horizontalPad = layout.contentPad;
  const tileGap = spacing.sm;
  const tileWidth = (contentWidth - horizontalPad * 2 - tileGap * 2) / 3;

  const dashboard = useQuery({
    queryKey: ['reception', 'dashboard', clinicId, branchId],
    queryFn: () => getReceptionDashboard(clinicId, branchId),
    enabled: Boolean(clinicId),
    // Soft background sync — not a live queue board.
    refetchInterval: 60_000,
    refetchIntervalInBackground: false,
  });

  const data = dashboard.data;
  const loading = dashboard.isLoading && !data;
  const error = dashboard.error ?? clinicsQuery.error;
  const receptionistName = account?.fullName?.trim() || copy.receptionPortalLabel;
  const clinicLabel = [data?.clinicName ?? clinic?.name, data?.branchName ?? branch?.name]
    .filter(Boolean)
    .join(' · ');

  const upcoming = useMemo(() => {
    const cutoff = Date.now() - 45 * 60 * 1000;
    return (data?.timeline ?? [])
      .filter((visit) => ['Confirmed', 'Requested', 'CheckedIn'].includes(visit.status))
      .filter((visit) => new Date(visit.startsAtUtc).getTime() >= cutoff)
      .sort((a, b) => new Date(a.startsAtUtc).getTime() - new Date(b.startsAtUtc).getTime())
      .slice(0, 5);
  }, [data?.timeline]);

  const consultingDoctors = useMemo(
    () => (data?.doctors ?? []).filter((doctor) => doctor.inConsultation > 0),
    [data?.doctors],
  );

  const availableDoctors = data?.doctors.length ?? 0;
  const showCollection = (data?.payments.collectedToday ?? 0) > 0;

  const checkIn = useMutation({
    mutationFn: (appointmentId: string) => checkInAppointment(appointmentId),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['reception', 'dashboard', clinicId, branchId] }),
        queryClient.invalidateQueries({ queryKey: ['queue'] }),
      ]);
    },
  });

  const quickActions = useMemo(
    () =>
      [
        {
          label: copy.receptionQuickBook,
          href: '/reception-appointments' as Href,
          icon: <IconCalendarPlus size={20} color={theme.colors.primary} />,
        },
        {
          label: copy.receptionQuickRegister,
          href: '/reception-patients' as Href,
          icon: <IconProfile size={20} color={theme.colors.primary} />,
        },
        {
          label: copy.receptionQuickCheckIn,
          href: '/reception-desk' as Href,
          icon: <IconAppointments size={20} color={theme.colors.primary} />,
        },
        {
          label: copy.receptionQuickWalkIn,
          href: '/reception-desk' as Href,
          icon: <IconClinic size={20} color={theme.colors.primary} />,
        },
        {
          label: copy.receptionNavQueue,
          href: '/reception-queue' as Href,
          icon: <IconClinic size={20} color={theme.colors.primary} />,
        },
        {
          label: 'Follow-ups',
          href: '/reception-follow-ups' as Href,
          icon: <IconCalendarPlus size={20} color={theme.colors.primary} />,
        },
      ] as const,
    [copy, theme.colors.primary],
  );

  const refresh = useCallback(async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['reception', 'dashboard', clinicId, branchId] }),
      queryClient.invalidateQueries({ queryKey: ['clinics', 'desk'] }),
    ]);
  }, [branchId, clinicId, queryClient]);
  const { refreshing, onRefresh } = useManualRefresh(refresh);

  const enter = (delay: number) =>
    reduceMotion ? undefined : FadeInDown.delay(delay).duration(duration.enter);

  const visitActions = (visit: ReceptionAppointment) => {
    const view = {
      label: copy.receptionViewVisit,
      onPress: () => router.push('/reception-appointments' as Href),
    };
    if (canCheckIn(visit.status)) {
      return {
        primaryAction: {
          label: copy.receptionCheckInAction,
          loading: checkIn.isPending && checkIn.variables === visit.id,
          onPress: () => checkIn.mutate(visit.id),
        },
        secondaryAction: view,
      };
    }
    return { primaryAction: view };
  };

  return (
    <ReceptionShell active="dashboard">
      <Screen edges={['left', 'right']}>
        <ScrollView
          showsVerticalScrollIndicator={false}
          contentContainerStyle={{
            paddingHorizontal: horizontalPad,
            paddingTop: spacing.sm,
            paddingBottom: spacing.xxl,
            gap: layout.sectionGap,
            width: contentWidth,
            maxWidth: '100%',
            alignSelf: 'center',
          }}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}>
          <Animated.View entering={enter(0)} style={{ gap: spacing.md }}>
            <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.md }}>
              <Pressable
                onPress={() => router.push('/reception-settings' as Href)}
                hitSlop={8}
                accessibilityRole="button"
                accessibilityLabel={copy.receptionNavSettings}>
                <ProfileAvatar name={receptionistName} size={48} />
              </Pressable>
              <View style={{ flex: 1, gap: 2, minWidth: 0 }}>
                <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                  {greeting(copy)}
                </AppText>
                <AppText variant="headline" style={{ fontSize: 20, lineHeight: 26 }} numberOfLines={1}>
                  {receptionistName}
                </AppText>
                {clinicLabel ? (
                  <AppText variant="caption" style={{ color: theme.colors.primary }} numberOfLines={1}>
                    {clinicLabel}
                  </AppText>
                ) : null}
              </View>
              <NotificationBell
                href={'/reception-notifications' as Href}
                accessibilityLabel={copy.notifications}
              />
              <Pressable
                onPress={() => router.push('/reception-settings' as Href)}
                hitSlop={10}
                accessibilityRole="button"
                accessibilityLabel={copy.receptionNavSettings}
                style={{
                  width: 40,
                  height: 40,
                  borderRadius: 12,
                  backgroundColor: theme.colors.surface,
                  borderWidth: 1,
                  borderColor: theme.colors.border,
                  alignItems: 'center',
                  justifyContent: 'center',
                }}>
                <IconProfile size={20} color={theme.colors.text} />
              </Pressable>
            </View>
            <ReceptionClinicSwitcher />
          </Animated.View>

          {!clinicId && !clinicsQuery.isLoading ? <EmptyState title={copy.receptionNoClinics} /> : null}

          {error ? (
            <Card style={{ gap: spacing.sm }}>
              <AppText style={{ color: theme.colors.error }}>
                {problemMessage(error, copy.receptionDashboardTitle)}
              </AppText>
              <Button label={copy.continue} onPress={refresh} />
            </Card>
          ) : null}

          {loading ? <StatSkeleton /> : null}

          {data ? (
            <Animated.View entering={enter(40)} style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
              <CompactStat label={copy.receptionTodayVisits} value={String(data.appointments.today)} />
              <CompactStat label={copy.receptionWaiting} value={String(data.queue.waiting)} />
              <CompactStat label={copy.receptionCompleted} value={String(data.appointments.completed)} />
              <CompactStat label={copy.receptionAvailableDoctors} value={String(availableDoctors)} />
              {showCollection ? (
                <CompactStat
                  label={copy.receptionCollectedToday}
                  value={`₹${Math.round(data.payments.collectedToday)}`}
                />
              ) : null}
            </Animated.View>
          ) : null}

          <Animated.View entering={enter(80)} style={{ gap: spacing.sm }}>
            <SectionHeader title={copy.receptionQuickActions} />
            <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: tileGap }}>
              {quickActions.map((action) => (
                <QuickActionTile
                  key={action.label}
                  label={action.label}
                  icon={action.icon}
                  width={tileWidth}
                  onPress={() => router.push(action.href)}
                />
              ))}
            </View>
          </Animated.View>

          <Animated.View entering={enter(120)} style={{ gap: spacing.sm }}>
            <SectionHeader
              title={copy.receptionTodayVisits}
              actionLabel={copy.receptionViewAll}
              onAction={() => router.push('/reception-appointments' as Href)}
            />
            {upcoming.length === 0 && !loading ? <EmptyState title={copy.receptionEmptyAppointments} /> : null}
            {upcoming.map((visit) => {
              const actions = visitActions(visit);
              return (
                <AppointmentCard
                  key={visit.id}
                  title={visit.patientName}
                  subtitle={visit.doctorName}
                  status={visit.status}
                  statusTone={statusTone(visit.status)}
                  timeLabel={formatTime(visit.startsAtUtc)}
                  primaryAction={actions.primaryAction}
                  secondaryAction={actions.secondaryAction}
                />
              );
            })}
          </Animated.View>

          <Animated.View entering={enter(160)} style={{ gap: spacing.sm }}>
            <SectionHeader
              title={copy.receptionLiveQueue}
              actionLabel={copy.receptionViewFullQueue}
              onAction={() => router.push('/reception-queue' as Href)}
            />
            {(data?.queue.waiting ?? 0) === 0 &&
            (data?.queue.inConsultation ?? 0) === 0 &&
            !data?.nextPatient &&
            !loading ? (
              <EmptyState title={copy.receptionEmptyQueue} />
            ) : data ? (
              <Card elevated style={{ gap: spacing.md, padding: spacing.lg }}>
                <View style={{ flexDirection: 'row', gap: spacing.md }}>
                  <View style={{ flex: 1, gap: 2 }}>
                    <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                      {copy.receptionWaiting}
                    </AppText>
                    <AppText variant="headline" style={{ color: theme.colors.primary }}>
                      {data.queue.waiting}
                    </AppText>
                  </View>
                  <View style={{ flex: 1, gap: 2 }}>
                    <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                      {copy.receptionInConsult}
                    </AppText>
                    <AppText variant="headline" style={{ color: theme.colors.primary }}>
                      {data.queue.inConsultation}
                    </AppText>
                  </View>
                </View>

                {data.nextPatient ? (
                  <View style={{ gap: 4 }}>
                    <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                      {copy.receptionNextPatient}
                    </AppText>
                    <AppText variant="label">{data.nextPatient.patientName}</AppText>
                    <AppText variant="muted">
                      {data.nextPatient.doctorName} · {formatTime(data.nextPatient.startsAtUtc)}
                    </AppText>
                  </View>
                ) : null}

                {consultingDoctors.length > 0 ? (
                  <View style={{ gap: 4 }}>
                    <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                      {copy.receptionCurrentConsult}
                    </AppText>
                    <AppText variant="label">
                      {consultingDoctors.map((doctor) => doctor.displayName).join(', ')}
                    </AppText>
                  </View>
                ) : null}

                <Button
                  label={copy.receptionViewFullQueue}
                  variant="secondary"
                  onPress={() => router.push('/reception-queue' as Href)}
                />
              </Card>
            ) : null}
          </Animated.View>
        </ScrollView>
      </Screen>
    </ReceptionShell>
  );
}
