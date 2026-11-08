import { useQuery } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useMemo, useState, type ReactNode } from 'react';
import { Pressable, ScrollView, useWindowDimensions, View } from 'react-native';
import Animated, { FadeInDown } from 'react-native-reanimated';

import { AppShell } from '@/components/layout/app-shell';
import { Screen } from '@/components/layout/auth-scaffold';
import { AppointmentCard } from '@/components/ui/appointment-card';
import { DoctorSearchAutocomplete } from '@/components/ui/doctor-search-autocomplete';
import { EmptyState } from '@/components/ui/empty-state';
import { ErrorState } from '@/components/ui/error-state';
import { FollowUpCard } from '@/components/ui/follow-up-card';
import { ImageBanner } from '@/components/ui/image-banner';
import { NotificationBell } from '@/components/ui/notification-bell';
import {
  IconCalendarPlus,
  IconClinic,
  IconHealthRecords,
  IconOnlineConsult,
} from '@/components/ui/prodoc-icons';
import { PressableScale } from '@/components/ui/pressable-scale';
import { SectionHeader } from '@/components/ui/section-header';
import { CardSkeleton } from '@/components/ui/skeleton';
import { SpecialtyCard, specialtyCardMetrics } from '@/components/ui/specialty-card';
import { AppText } from '@/components/ui/text';
import {
  buildSpecialtyCards,
  type SpecialtyCareGroup,
} from '@/features/account/specialty-images';
import { VaidyaAssistantHost } from '@/features/assistant/vaidya-assistant';
import { useActiveLanguage, useCopy } from '@/i18n/copy';
import { listMyFollowUps } from '@/services/api/follow-up-api';
import {
  getSpecializations,
  listAppointments,
} from '@/services/api/provider-api';
import { useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { duration, layout, motion, radius, spacing } from '@/theme/tokens';
import { needsProfileCompletion } from '@/utils/home-surface';
import {
  findNextPatientAppointment,
  patientBookingActionKind,
} from '@/utils/patient-appointment-actions';
import { problemMessage } from '@/utils/problem';

const doctorBanner = require('../../../assets/images/home-doctor-banner.jpg');

function timeGreeting(copy: ReturnType<typeof useCopy>) {
  const hour = new Date().getHours();
  if (hour < 12) return copy.goodMorning;
  if (hour < 17) return copy.goodAfternoon;
  return copy.goodEvening;
}

function QuickAction({
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
        minHeight: 96,
        backgroundColor: theme.colors.surface,
        borderRadius: 16,
        borderWidth: 1,
        borderColor: theme.colors.border,
        paddingHorizontal: 14,
        paddingVertical: 14,
        gap: 12,
        justifyContent: 'space-between',
      }}>
      {icon}
      <AppText variant="label" style={{ fontSize: 14, lineHeight: 18, color: theme.colors.text }}>
        {label}
      </AppText>
    </PressableScale>
  );
}

/** Patient home — Practo / Apple Health inspired, Earth Green, real APIs only. */
export function PatientHome() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const { width } = useWindowDimensions();
  const account = useSessionStore((state) => state.account);
  const permissions = account?.permissions ?? [];
  const canBook = permissions.includes('appointments.book.self');
  const language = useActiveLanguage();
  const [careGroup, setCareGroup] = useState<SpecialtyCareGroup>('general');
  const reduceMotion = theme.reduceMotion;
  const contentWidth = Math.min(width, layout.phoneMax);
  const horizontalPad = layout.contentPad;
  const gridGap = layout.listGap;
  const tileWidth = (contentWidth - horizontalPad * 2 - gridGap) / 2;
  const patientName = account?.fullName?.trim() || copy.welcome;
  const greeting = timeGreeting(copy);

  const specializations = useQuery({
    queryKey: ['specializations'],
    queryFn: getSpecializations,
    staleTime: 5 * 60_000,
  });
  const appointments = useQuery({
    queryKey: ['appointments', 'mine', 'home'],
    queryFn: listAppointments,
    staleTime: 60_000,
  });
  const followUps = useQuery({
    queryKey: ['follow-ups', 'mine', 'home'],
    queryFn: listMyFollowUps,
    staleTime: 60_000,
  });
  const openFollowUps = useMemo(
    () =>
      (followUps.data ?? []).filter(
        (item) => item.status === 'Recommended' || item.status === 'AppointmentBooked',
      ),
    [followUps.data],
  );

  const specialtyCards = useMemo(
    () => buildSpecialtyCards(specializations.data ?? [], language),
    [language, specializations.data],
  );
  const visibleSpecialties = useMemo(
    () => specialtyCards.filter((item) => item.group === careGroup),
    [careGroup, specialtyCards],
  );
  const specialtyColumns = Math.max(1, Math.ceil(visibleSpecialties.length / 2));
  const specialtyGridWidth =
    specialtyColumns * specialtyCardMetrics.width +
    Math.max(0, specialtyColumns - 1) * specialtyCardMetrics.gap;

  const upcoming = useMemo(() => findNextPatientAppointment(appointments.data), [appointments.data]);

  const upcomingActionLabel = useMemo(() => {
    if (!upcoming) return copy.homeBookCta;
    const kind = patientBookingActionKind(upcoming.status);
    if (kind === 'continueBooking') return copy.continueBooking;
    if (kind === 'viewBooking') return copy.viewBooking;
    return copy.viewAppointment;
  }, [copy.continueBooking, copy.homeBookCta, copy.viewAppointment, copy.viewBooking, upcoming]);

  const openDoctors = (opts?: { clinics?: boolean; specializationId?: string }) => {
    if (opts?.clinics) {
      router.push('/clinics' as Href);
      return;
    }
    if (opts?.specializationId) {
      router.push(`/doctors?specializationId=${encodeURIComponent(opts.specializationId)}` as Href);
      return;
    }
    router.push('/doctors' as Href);
  };

  const enter = (delay: number) =>
    reduceMotion ? undefined : FadeInDown.delay(delay).duration(duration.enter);

  return (
    <AppShell active="home">
      <Screen>
        <ScrollView
          showsVerticalScrollIndicator={false}
          contentContainerStyle={{
            paddingHorizontal: horizontalPad,
            paddingTop: 8,
            paddingBottom: 28,
            gap: layout.sectionGap,
            width: contentWidth,
            maxWidth: '100%',
            alignSelf: 'center',
          }}>
          {needsProfileCompletion(account) ? (
            <Pressable
              onPress={() => router.replace('/complete-profile' as Href)}
              style={{
                backgroundColor: theme.colors.primaryLight,
                borderRadius: 16,
                padding: 14,
                gap: 6,
              }}>
              <AppText variant="label" style={{ color: theme.colors.primary }}>
                {copy.profileCompletePrompt}
              </AppText>
              <AppText variant="muted" style={{ fontSize: 13, lineHeight: 18 }}>
                {copy.profileCompleteBody}
              </AppText>
              <AppText variant="label" style={{ color: theme.colors.primary, marginTop: 4 }}>
                {copy.profileCompleteCta}
              </AppText>
            </Pressable>
          ) : null}

          <Animated.View
            entering={enter(0)}
            style={{ flexDirection: 'row', alignItems: 'flex-start', justifyContent: 'space-between' }}>
            <View style={{ flex: 1, gap: 2, paddingRight: 12 }}>
              <AppText variant="muted" style={{ fontSize: 15, lineHeight: 20 }}>
                {greeting}
              </AppText>
              <AppText
                variant="title"
                style={{ fontSize: 24, lineHeight: 30, color: theme.colors.text }}
                numberOfLines={1}>
                {patientName}
              </AppText>
            </View>
            <NotificationBell href={'/notifications' as Href} accessibilityLabel={copy.notifications} />
          </Animated.View>

          <Animated.View entering={enter(40)}>
            <DoctorSearchAutocomplete placeholder={copy.homeSearchPlaceholder} />
          </Animated.View>

          <Animated.View
            entering={enter(80)}
            style={{ flexDirection: 'row', flexWrap: 'wrap', gap: gridGap }}>
            <QuickAction
              width={tileWidth}
              label={copy.homeQuickBook}
              icon={<IconCalendarPlus size={24} color={theme.colors.primary} />}
              onPress={() => (canBook ? openDoctors() : router.push('/doctors' as Href))}
            />
            <QuickAction
              width={tileWidth}
              label={copy.homeQuickOnline}
              icon={<IconOnlineConsult size={24} color={theme.colors.primary} />}
              onPress={() => openDoctors()}
            />
            <QuickAction
              width={tileWidth}
              label={copy.homeQuickClinics}
              icon={<IconClinic size={24} color={theme.colors.primary} />}
              onPress={() => openDoctors({ clinics: true })}
            />
            <QuickAction
              width={tileWidth}
              label={copy.homeQuickRecords}
              icon={<IconHealthRecords size={24} color={theme.colors.primary} />}
              onPress={() => router.push('/health-records' as Href)}
            />
          </Animated.View>

          <Animated.View entering={enter(120)}>
            <ImageBanner image={doctorBanner} onPress={() => openDoctors()}>
              <AppText
                variant="headline"
                style={{ fontSize: 18, lineHeight: 24, color: theme.colors.text, maxWidth: 180 }}>
                {copy.homeConsultBanner}
              </AppText>
              <AppText variant="muted" style={{ fontSize: 13, lineHeight: 18 }}>
                {copy.homeConsultBannerBody}
              </AppText>
            </ImageBanner>
          </Animated.View>

          <Animated.View entering={enter(160)} style={{ gap: spacing.md }}>
            <SectionHeader
              title={copy.homeUpcoming}
              actionLabel={copy.homeSeeAll}
              onAction={() => router.push('/visits' as Href)}
            />
            {appointments.isLoading && !appointments.data ? <CardSkeleton /> : null}
            {appointments.error ? (
              <ErrorState
                title={copy.homeUpcoming}
                body={problemMessage(appointments.error, copy.homeUpcoming)}
                actionLabel={copy.continue}
                onAction={() => void appointments.refetch()}
              />
            ) : null}
            {!appointments.error && upcoming ? (
              <AppointmentCard
                title={upcoming.doctorName}
                subtitle={
                  [upcoming.clinicName, upcoming.branchName]
                    .map((part) => part?.trim())
                    .filter(Boolean)
                    .filter((part, index, all) => index === 0 || part?.toLowerCase() !== all[0]?.toLowerCase())
                    .join(' · ') || undefined
                }
                meta={upcoming.address}
                timeLabel={new Date(upcoming.startsAtUtc).toLocaleString(undefined, {
                  weekday: 'short',
                  day: 'numeric',
                  month: 'short',
                  hour: '2-digit',
                  minute: '2-digit',
                })}
                status={upcoming.status}
                statusTone={
                  upcoming.status === 'Confirmed' || upcoming.status === 'CheckedIn'
                    ? 'success'
                    : upcoming.status === 'Requested' || upcoming.status === 'Held'
                      ? 'warning'
                      : 'primary'
                }
                avatarName={upcoming.doctorName}
                primaryAction={{
                  label: upcomingActionLabel,
                  onPress: () =>
                    router.push(`/visits?appointmentId=${encodeURIComponent(upcoming.id)}` as Href),
                }}
              />
            ) : null}
            {!appointments.error && !upcoming && !(appointments.isLoading && !appointments.data) ? (
              <EmptyState
                compact
                title={copy.homeNoUpcoming}
                actionLabel={copy.homeBookCta}
                onAction={() => openDoctors()}
                icon={<IconCalendarPlus size={28} color={theme.colors.primary} />}
              />
            ) : null}
          </Animated.View>

          {openFollowUps.length > 0 ? (
            <Animated.View entering={enter(180)} style={{ gap: spacing.md }}>
              <SectionHeader title="Follow-up Reminders" actionLabel={copy.homeSeeAll} onAction={() => router.push('/visits' as Href)} />
              {openFollowUps.slice(0, 2).map((item) => (
                <FollowUpCard
                  key={item.id}
                  item={item}
                  bookLabel="Book Follow-up Appointment"
                  onBook={() =>
                    router.push(
                      `/practices/${item.doctorId}?clinicId=${encodeURIComponent(item.clinicId)}&branchId=${encodeURIComponent(item.branchId)}&followUpId=${encodeURIComponent(item.id)}` as Href,
                    )
                  }
                />
              ))}
            </Animated.View>
          ) : null}

          <Animated.View entering={enter(200)} style={{ gap: spacing.md }}>
            <SectionHeader title={copy.homeSpecialties} />
            <View
              style={{
                flexDirection: 'row',
                backgroundColor: theme.scheme === 'light' ? '#EEF2F6' : theme.colors.softBlue,
                borderRadius: radius.pill,
                padding: 4,
              }}>
              {(
                [
                  { id: 'general' as const, label: copy.homeSpecialtyGeneralCare },
                  { id: 'advanced' as const, label: copy.homeSpecialtyAdvancedCare },
                ] as const
              ).map((tab) => {
                const active = careGroup === tab.id;
                return (
                  <Pressable
                    key={tab.id}
                    accessibilityRole="button"
                    accessibilityState={{ selected: active }}
                    onPress={() => setCareGroup(tab.id)}
                    style={{
                      flex: 1,
                      minHeight: 36,
                      borderRadius: radius.pill,
                      alignItems: 'center',
                      justifyContent: 'center',
                      backgroundColor: active ? theme.colors.surface : 'transparent',
                      borderWidth: active ? 1 : 0,
                      borderColor: active ? `${theme.colors.primary}33` : 'transparent',
                    }}>
                    <AppText
                      variant="label"
                      style={{
                        fontSize: 13,
                        lineHeight: 18,
                        color: active ? theme.colors.primary : theme.colors.textMuted,
                        fontWeight: active ? '600' : '500',
                      }}>
                      {tab.label}
                    </AppText>
                  </Pressable>
                );
              })}
            </View>
            <ScrollView
              horizontal
              showsHorizontalScrollIndicator={false}
              decelerationRate="fast"
              contentContainerStyle={{ paddingRight: 4 }}>
              <View
                style={{
                  width: specialtyGridWidth,
                  flexDirection: 'row',
                  flexWrap: 'wrap',
                  gap: specialtyCardMetrics.gap,
                }}>
                {visibleSpecialties.map((item) => (
                  <SpecialtyCard
                    key={item.id}
                    label={item.label}
                    image={item.image}
                    recyclingKey={item.code}
                    onPress={() => openDoctors({ specializationId: item.id })}
                  />
                ))}
              </View>
            </ScrollView>
          </Animated.View>
        </ScrollView>
        <VaidyaAssistantHost />
      </Screen>
    </AppShell>
  );
}
