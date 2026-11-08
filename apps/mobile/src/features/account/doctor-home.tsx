import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useCallback, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  Image,
  Pressable,
  RefreshControl,
  ScrollView,
  Switch,
  View,
} from 'react-native';
import Animated, { FadeInDown } from 'react-native-reanimated';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { ClinicSwitcher, useSelectedDoctorClinicId } from '@/components/doctor/clinic-switcher';
import { DoctorShell } from '@/components/layout/doctor-shell';
import { Screen } from '@/components/layout/auth-scaffold';
import { NotificationBell } from '@/components/ui/notification-bell';
import {
  IconCalendarPlus,
  IconChevronRight,
  IconStethoscope,
} from '@/components/ui/prodoc-icons';
import { ProdocLogo } from '@/components/ui/prodoc-logo';
import { ProfileAvatar } from '@/components/ui/profile-avatar';
import { AppText } from '@/components/ui/text';
import { clinicDateIso, normalizeScheduleDay } from '@/features/providers/schedule-day';
import { useManualRefresh } from '@/hooks/use-manual-refresh';
import { useCopy } from '@/i18n/copy';
import { apiBaseUrl } from '@/services/api/client';
import { getDoctorDashboard } from '@/services/api/doctor-portal-api';
import {
  addScheduleClosure,
  listMySchedules,
  removeScheduleClosure,
  setSchedulePublished,
} from '@/services/api/provider-api';
import { useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { duration, spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

function greeting(copy: ReturnType<typeof useCopy>) {
  const hour = new Date().getHours();
  if (hour < 12) return copy.goodMorning;
  if (hour < 17) return copy.goodAfternoon;
  return copy.goodEvening;
}

function todayIso() {
  const now = new Date();
  return `${now.getFullYear()}-${`${now.getMonth() + 1}`.padStart(2, '0')}-${`${now.getDate()}`.padStart(2, '0')}`;
}

function KpiTile({
  label,
  value,
  accent,
}: {
  label: string;
  value: string;
  accent: string;
}) {
  const theme = useProdocTheme();
  return (
    <View
      style={{
        flex: 1,
        minWidth: '46%',
        backgroundColor: theme.colors.surface,
        borderRadius: 16,
        borderWidth: 1,
        borderColor: theme.colors.border,
        padding: spacing.md,
        gap: 6,
      }}>
      <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
        <AppText variant="caption" style={{ color: theme.colors.textMuted }} numberOfLines={1}>
          {label}
        </AppText>
        <View style={{ width: 8, height: 8, borderRadius: 4, backgroundColor: accent }} />
      </View>
      <AppText variant="headline" style={{ color: theme.colors.text, fontSize: 28, lineHeight: 34 }}>
        {value}
      </AppText>
    </View>
  );
}

export function DoctorHome() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const queryClient = useQueryClient();
  const account = useSessionStore((state) => state.account);
  const accessToken = useSessionStore((state) => state.accessToken);
  const clinicsQuery = useQuery({
    queryKey: ['doctor', 'dashboard', 'bootstrap'],
    queryFn: () => getDoctorDashboard(null),
  });
  const clinicsReady = clinicsQuery.isSuccess;
  const clinicId = useSelectedDoctorClinicId(clinicsReady ? clinicsQuery.data?.clinics ?? [] : undefined);
  const dashboard = useQuery({
    queryKey: ['doctor', 'dashboard', clinicId ?? 'all'],
    queryFn: () => getDoctorDashboard(clinicId),
    enabled: clinicsReady,
  });
  const data = dashboard.data ?? clinicsQuery.data;
  const loading = dashboard.isLoading && !data;
  const error = dashboard.error ?? clinicsQuery.error;

  const photoUri =
    data?.photoDocumentId && accessToken
      ? {
          uri: `${apiBaseUrl}/api/v1/provider-documents/${data.photoDocumentId}`,
          headers: { Authorization: `Bearer ${accessToken}` },
        }
      : null;

  const upcoming = useMemo(() => {
    const rows = data?.todayPreview ?? [];
    return rows
      .filter((item) => !['Cancelled', 'Rejected', 'Completed'].includes(item.status))
      .slice(0, 3);
  }, [data?.todayPreview]);

  const [toggleError, setToggleError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['doctor', 'dashboard', clinicId ?? 'all'] }),
      queryClient.invalidateQueries({ queryKey: ['doctor', 'dashboard', 'bootstrap'] }),
    ]);
  }, [clinicId, queryClient]);
  const { refreshing, onRefresh } = useManualRefresh(refresh);

  const openTodayTiming = () => router.push(`/doctor-today-timing?date=${clinicDateIso()}` as Href);

  const toggleAvailability = useMutation({
    mutationFn: async (nextAvailable: boolean) => {
      setToggleError(null);
      let scheduleId = data?.availability?.scheduleId ?? null;
      let closureId = data?.availability?.closureId ?? null;
      let rowVersion: string | undefined;

      if (!scheduleId) {
        const mine = await listMySchedules();
        const match =
          mine.find((item) => !clinicId || item.clinicId === clinicId) ?? mine[0] ?? null;
        scheduleId = match?.id ?? null;
        rowVersion = match?.rowVersion;
        closureId = match?.closures.find((item) => item.date.startsWith(todayIso()))?.id ?? null;
      } else {
        const mine = await listMySchedules();
        const match = mine.find((item) => item.id === scheduleId);
        rowVersion = match?.rowVersion;
        if (!closureId) {
          closureId = match?.closures.find((item) => item.date.startsWith(todayIso()))?.id ?? null;
        }
      }

      if (!scheduleId) {
        throw new Error('no_schedule');
      }

      if (nextAvailable) {
        if (closureId) {
          await removeScheduleClosure(scheduleId, closureId, rowVersion);
        }
        const mine = await listMySchedules();
        const match = mine.find((item) => item.id === scheduleId);
        if (match && !match.isPublished) {
          await setSchedulePublished(scheduleId, true, match.rowVersion);
        }
        const todayDay = new Date().getDay();
        const hasTodayHours = (match?.hours ?? []).some((item) => normalizeScheduleDay(item.day) === todayDay);
        if (!hasTodayHours) {
          throw new Error('needs_hours');
        }
        return;
      }

      await addScheduleClosure(scheduleId, todayIso(), 'Unavailable today', rowVersion);
    },
    onSuccess: async () => {
      await refresh();
    },
    onError: (error) => {
      if (error instanceof Error && error.message === 'no_schedule') {
        setToggleError(copy.doctorTimingNeedSchedule);
        openTodayTiming();
        return;
      }
      if (error instanceof Error && error.message === 'needs_hours') {
        setToggleError(copy.doctorTimingNeedSession);
        openTodayTiming();
        return;
      }
      setToggleError(problemMessage(error, copy.doctorAvailabilityTitle));
      Alert.alert(copy.doctorAvailabilityTitle, problemMessage(error, copy.doctorAvailabilityTitle));
    },
  });

  const available = data?.availability?.isAvailableToday ?? false;
  const timings =
    data?.availability?.timingsLabel ??
    (data?.availability?.startsAt && data?.availability?.endsAt
      ? `${data.availability.startsAt} – ${data.availability.endsAt}`
      : null);

  return (
    <DoctorShell active="dashboard">
      <Screen>
        <ScrollView
          contentContainerStyle={{
            gap: spacing.md,
            paddingBottom: spacing.xxl,
            paddingTop: Math.max(insets.top - 8, 0),
          }}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}>
          <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
            <ProdocLogo size={32} />
            <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.md }}>
              <NotificationBell href={'/doctor-notifications' as Href} />
              <Pressable onPress={() => router.push('/doctor')}>
                {photoUri ? (
                  <Image source={photoUri} style={{ width: 36, height: 36, borderRadius: 18 }} />
                ) : (
                  <ProfileAvatar name={data?.displayName ?? account?.fullName ?? 'Doctor'} size={36} />
                )}
              </Pressable>
            </View>
          </View>

          <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.md }}>
            {photoUri ? (
              <Image source={photoUri} style={{ width: 64, height: 64, borderRadius: 32 }} />
            ) : (
              <ProfileAvatar name={data?.displayName ?? account?.fullName ?? 'Doctor'} size={64} />
            )}
            <View style={{ flex: 1, gap: 2 }}>
              <AppText variant="title" numberOfLines={1}>
                {greeting(copy)}, {data?.displayName ?? account?.fullName ?? copy.doctorDashboardTitle}
              </AppText>
              <AppText variant="caption" style={{ color: theme.colors.textMuted }} numberOfLines={1}>
                {data?.specializationName ?? copy.doctorPortalLabel}
              </AppText>
            </View>
          </View>

          <ClinicSwitcher clinics={data?.clinics ?? []} />

          {loading ? (
            <View style={{ gap: spacing.sm }}>
              {[0, 1, 2].map((item) => (
                <View
                  key={item}
                  style={{
                    height: item === 0 ? 88 : 72,
                    borderRadius: 16,
                    backgroundColor: theme.colors.primaryLight,
                    opacity: 0.45,
                  }}
                />
              ))}
            </View>
          ) : null}

          {data ? (
            <Animated.View
              entering={theme.reduceMotion ? undefined : FadeInDown.duration(duration.enter)}
              style={{ gap: spacing.md }}>
              <View
                style={{
                  backgroundColor: theme.colors.primary,
                  borderRadius: 18,
                  padding: spacing.lg,
                  gap: spacing.sm,
                }}>
                <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
                  <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm, flex: 1 }}>
                    <IconStethoscope size={20} color="#FFFFFF" />
                    <AppText variant="label" style={{ color: '#FFFFFF' }}>
                      {available ? copy.doctorAvailableToday : copy.doctorUnavailableToday}
                    </AppText>
                    {toggleAvailability.isPending ? <ActivityIndicator color="#FFFFFF" /> : null}
                  </View>
                  <Switch
                    value={available}
                    onValueChange={(value) => toggleAvailability.mutate(value)}
                    trackColor={{ false: 'rgba(255,255,255,0.35)', true: 'rgba(255,255,255,0.55)' }}
                    thumbColor="#FFFFFF"
                    disabled={toggleAvailability.isPending}
                  />
                </View>
                {timings ? (
                  <AppText style={{ color: 'rgba(255,255,255,0.92)' }}>{timings}</AppText>
                ) : (
                  <AppText style={{ color: 'rgba(255,255,255,0.85)' }}>{copy.doctorEmptyTimingsHint}</AppText>
                )}
                {data.availability?.breakStartsAt && data.availability?.breakEndsAt ? (
                  <AppText style={{ color: 'rgba(255,255,255,0.85)' }}>
                    {copy.doctorBreakLabel}: {data.availability.breakStartsAt} – {data.availability.breakEndsAt}
                  </AppText>
                ) : null}
                <Pressable onPress={openTodayTiming} hitSlop={8}>
                  <AppText variant="label" style={{ color: '#FFFFFF', textDecorationLine: 'underline' }}>
                    {copy.doctorAvailabilityUpdate}
                  </AppText>
                </Pressable>
              </View>

              <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
                <KpiTile label={copy.doctorKpiTodayAppts} value={String(data.appointments.today)} accent={theme.colors.primary} />
                <KpiTile label={copy.doctorKpiWaiting} value={String(data.appointments.waiting)} accent="#7C6BC4" />
                <KpiTile label={copy.doctorKpiCompleted} value={String(data.appointments.completed)} accent="#2F9E6F" />
                <KpiTile label={copy.doctorKpiCancelled} value={String(data.appointments.cancelled)} accent={theme.colors.error} />
              </View>

              {data.nextPatient ? (
                <View style={{ gap: spacing.sm }}>
                  <AppText variant="label">{copy.doctorNextPatient}</AppText>
                  <Pressable
                    onPress={() => router.push(`/doctor-appointment/${data.nextPatient!.id}` as Href)}
                    style={{
                      backgroundColor: theme.colors.surface,
                      borderRadius: 16,
                      borderWidth: 1,
                      borderColor: theme.colors.border,
                      padding: spacing.md,
                      flexDirection: 'row',
                      alignItems: 'center',
                      gap: spacing.md,
                    }}>
                    <ProfileAvatar name={data.nextPatient.patientName} size={44} />
                    <View style={{ flex: 1, gap: 2 }}>
                      <AppText variant="label" numberOfLines={1}>
                        {data.nextPatient.patientName}
                      </AppText>
                      <AppText variant="caption" style={{ color: theme.colors.textMuted }} numberOfLines={1}>
                        {new Date(data.nextPatient.startsAtUtc).toLocaleTimeString([], {
                          hour: '2-digit',
                          minute: '2-digit',
                        })}
                        {data.nextPatient.reason ? ` · ${data.nextPatient.reason}` : ''}
                      </AppText>
                    </View>
                    <View
                      style={{
                        width: 36,
                        height: 36,
                        borderRadius: 18,
                        backgroundColor: theme.colors.primary,
                        alignItems: 'center',
                        justifyContent: 'center',
                      }}>
                      <IconChevronRight size={18} color="#FFFFFF" />
                    </View>
                  </Pressable>
                </View>
              ) : null}

              <View style={{ gap: spacing.sm }}>
                <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
                  <AppText variant="label">{copy.doctorUpcomingAppointments}</AppText>
                  <Pressable onPress={() => router.push('/practice')}>
                    <AppText variant="label" style={{ color: theme.colors.primary }}>
                      {copy.doctorViewAll}
                    </AppText>
                  </Pressable>
                </View>
                {upcoming.length === 0 ? (
                  <View
                    style={{
                      borderRadius: 16,
                      borderWidth: 1,
                      borderColor: theme.colors.border,
                      backgroundColor: theme.colors.surface,
                      padding: spacing.lg,
                      gap: spacing.sm,
                      alignItems: 'flex-start',
                    }}>
                    <IconCalendarPlus size={22} color={theme.colors.primary} />
                    <AppText variant="muted">{copy.doctorEmptyToday}</AppText>
                    <Pressable onPress={openTodayTiming}>
                      <AppText variant="label" style={{ color: theme.colors.primary }}>
                        {copy.doctorAvailabilityUpdate}
                      </AppText>
                    </Pressable>
                  </View>
                ) : (
                  upcoming.map((visit) => (
                    <Pressable
                      key={visit.id}
                      onPress={() => router.push(`/doctor-appointment/${visit.id}` as Href)}
                      style={{
                        backgroundColor: theme.colors.surface,
                        borderRadius: 16,
                        borderWidth: 1,
                        borderColor: theme.colors.border,
                        padding: spacing.md,
                        flexDirection: 'row',
                        alignItems: 'center',
                        gap: spacing.md,
                      }}>
                      <ProfileAvatar name={visit.patientName} size={40} />
                      <View style={{ flex: 1, gap: 2 }}>
                        <AppText variant="label" numberOfLines={1}>
                          {visit.patientName}
                        </AppText>
                        <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                          {new Date(visit.startsAtUtc).toLocaleTimeString([], {
                            hour: '2-digit',
                            minute: '2-digit',
                          })}
                          {visit.status ? ` · ${visit.status}` : ''}
                        </AppText>
                      </View>
                      <IconChevronRight size={18} color={theme.colors.textMuted} />
                    </Pressable>
                  ))
                )}
              </View>
            </Animated.View>
          ) : null}

          {error ? <AppText variant="muted">{problemMessage(error, copy.doctorDashboardTitle)}</AppText> : null}
          {toggleError ? <AppText variant="muted">{toggleError}</AppText> : null}
        </ScrollView>
      </Screen>
    </DoctorShell>
  );
}
