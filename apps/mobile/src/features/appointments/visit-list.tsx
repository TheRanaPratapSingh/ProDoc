import { ScreenHeader } from '@/components/layout/responsive-header';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useLocalSearchParams, useRouter, type Href } from 'expo-router';
import { memo, useCallback, useEffect, useMemo, useState } from 'react';
import { Alert, Pressable, View } from 'react-native';
import Animated, { FadeInDown, Layout } from 'react-native-reanimated';

import { AppShell } from '@/components/layout/app-shell';
import { ScreenList } from '@/components/layout/screen-list';
import { Button } from '@/components/ui/button';
import { CalendarView } from '@/components/ui/calendar-view';
import { Card } from '@/components/ui/card';
import {
  ShareAppointmentButton,
  ShareAppointmentSheet,
} from '@/components/appointments/share-appointment-sheet';
import { ClinicLocationCard } from '@/components/ui/clinic-location-card';
import { EmptyState } from '@/components/ui/empty-state';
import { FollowUpCard } from '@/components/ui/follow-up-card';
import { ProfileAvatar } from '@/components/ui/profile-avatar';
import { Segmented } from '@/components/ui/segmented';
import { StatusBadge } from '@/components/ui/status-badge';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { TimeSlotSelector } from '@/components/ui/time-slot-selector';
import { VerificationBadge } from '@/components/ui/verification-badge';
import { clinicDateIso, patientBookingWindow, startOfClinicWeekMonday } from '@/features/providers/schedule-day';
import { useManualRefresh } from '@/hooks/use-manual-refresh';
import { useCopy } from '@/i18n/copy';
import { createAppointmentPayment, verifyPayment } from '@/services/api/billing-api';
import { listMyFollowUps } from '@/services/api/follow-up-api';
import {
  cancelAppointment,
  getAvailability,
  getSlots,
  listAppointments,
  rescheduleAppointment,
  type AppointmentDetails,
} from '@/services/api/provider-api';
import { listEligibleReviews, listMyReviews, type ReviewDetails } from '@/services/api/review-api';
import { assertMutationAllowedOnline } from '@/services/offline/offline-queue';
import { selectIsEffectivelyOnline, useNetworkStore } from '@/stores/network-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { duration, radius, spacing } from '@/theme/tokens';
import {
  canPatientCancelVisit,
  canPatientRescheduleVisit,
  canPatientSameDayReschedule,
  classifyPatientVisitTab,
  nextPatientModifyRefreshAt,
} from '@/utils/patient-appointment-actions';
import { problemMessage } from '@/utils/problem';
import {
  clinicPlaceLabel,
  formatDoctorDisplayName,
  formatVisitDate,
  formatVisitTime,
  paymentStatusLabel,
} from '@/utils/visit-format';

function label(copy: ReturnType<typeof useCopy>, status: string) {
  if (status === 'Confirmed') return copy.confirmed;
  if (status === 'Requested') return copy.requested;
  if (status === 'Rejected') return copy.rejected;
  if (status === 'Cancelled') return copy.cancelled;
  if (status === 'Expired') return copy.expired;
  if (status === 'CheckedIn') return copy.checkedIn;
  if (status === 'Completed') return copy.completedVisit;
  if (status === 'Held') return copy.requested;
  return status;
}

function statusTone(status: string): 'neutral' | 'primary' | 'success' | 'warning' | 'error' {
  if (status === 'Confirmed' || status === 'CheckedIn' || status === 'Completed') return 'success';
  if (status === 'Requested' || status === 'Held') return 'warning';
  if (status === 'Rejected' || status === 'Cancelled' || status === 'Expired') return 'error';
  return 'neutral';
}

function MetaRow({ labelText, value }: { labelText: string; value: string }) {
  const theme = useProdocTheme();
  return (
    <View style={{ gap: 2 }}>
      <AppText variant="caption" style={{ color: theme.colors.textMuted, letterSpacing: 0.4 }}>
        {labelText.toUpperCase()}
      </AppText>
      <AppText variant="body" style={{ color: theme.colors.text }}>
        {value}
      </AppText>
    </View>
  );
}

function monthRange(monthStartYmd: string) {
  const [y, m] = monthStartYmd.split('-').map(Number);
  const start = `${y}-${`${m}`.padStart(2, '0')}-01`;
  const endDate = new Date(y, m, 0);
  const end = `${endDate.getFullYear()}-${`${endDate.getMonth() + 1}`.padStart(2, '0')}-${`${endDate.getDate()}`.padStart(2, '0')}`;
  const today = todayYmd();
  return {
    start: start < today ? today : start,
    end: end < today ? today : end,
  };
}

function todayYmd() {
  const date = new Date();
  return `${date.getFullYear()}-${`${date.getMonth() + 1}`.padStart(2, '0')}-${`${date.getDate()}`.padStart(2, '0')}`;
}

function normalizeSlotStart(start: string) {
  const trimmed = start.trim();
  if (/^\d{2}:\d{2}$/.test(trimmed)) return `${trimmed}:00`;
  return trimmed;
}

export function VisitList() {
  const copy = useCopy();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { appointmentId: focusAppointmentId } = useLocalSearchParams<{ appointmentId?: string }>();
  const online = useNetworkStore(selectIsEffectivelyOnline);
  const visits = useQuery({
    queryKey: ['appointments', 'mine'],
    queryFn: listAppointments,
    // Refresh after booking/cancel/reschedule mutations — not on a timer.
    staleTime: 60_000,
  });
  const followUps = useQuery({
    queryKey: ['follow-ups', 'mine'],
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
  const eligibleReviews = useQuery({
    queryKey: ['reviews', 'eligible'],
    queryFn: listEligibleReviews,
    staleTime: 60_000,
  });
  const myReviews = useQuery({
    queryKey: ['reviews', 'mine'],
    queryFn: listMyReviews,
    staleTime: 60_000,
  });
  const reviewableIds = useMemo(() => {
    const ids = new Set<string>();
    for (const item of eligibleReviews.data ?? []) {
      if (item.canReviewDoctor || item.canReviewClinic) {
        ids.add(item.appointmentId);
      }
    }
    return ids;
  }, [eligibleReviews.data]);
  const myReviewByAppointment = useMemo(() => {
    const map = new Map<string, ReviewDetails>();
    for (const review of myReviews.data ?? []) {
      const existing = map.get(review.appointmentId);
      // Prefer doctor reviews for visit CTAs when both exist.
      if (!existing || (existing.targetKind !== 'Doctor' && review.targetKind === 'Doctor')) {
        map.set(review.appointmentId, review);
      }
    }
    return map;
  }, [myReviews.data]);
  const [reschedulingId, setReschedulingId] = useState<string | null>(null);
  const [cancelReasonId, setCancelReasonId] = useState<string | null>(null);
  const [date, setDate] = useState(todayYmd());
  const [time, setTime] = useState('');
  const [reason, setReason] = useState('');
  const [guardError, setGuardError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [tab, setTab] = useState<'upcoming' | 'past' | 'cancelled'>('upcoming');
  const [nowMs, setNowMs] = useState(() => Date.now());

  useEffect(() => {
    const tick = () => setNowMs(Date.now());
    const baseline = setInterval(tick, 30_000);
    const nextBoundary = nextPatientModifyRefreshAt(visits.data ?? [], Date.now());
    let precise: ReturnType<typeof setTimeout> | undefined;
    if (nextBoundary != null) {
      const delay = Math.max(250, Math.min(nextBoundary - Date.now() + 50, 60_000));
      precise = setTimeout(tick, delay);
    }
    return () => {
      clearInterval(baseline);
      if (precise) clearTimeout(precise);
    };
  }, [visits.data, nowMs]);

  useEffect(() => {
    if (!focusAppointmentId || !visits.data) return;
    const focused = visits.data.find((item) => item.id === focusAppointmentId);
    if (!focused) return;
    setTab(classifyPatientVisitTab(focused, nowMs));
  }, [focusAppointmentId, visits.data, nowMs]);

  useEffect(() => {
    if (reschedulingId) {
      const visit = visits.data?.find((item) => item.id === reschedulingId);
      if (visit && !canPatientRescheduleVisit(visit, nowMs)) {
        setReschedulingId(null);
        setTime('');
      }
    }
    if (cancelReasonId) {
      const visit = visits.data?.find((item) => item.id === cancelReasonId);
      if (visit && !canPatientCancelVisit(visit, nowMs)) {
        setCancelReasonId(null);
        setReason('');
      }
    }
  }, [cancelReasonId, nowMs, reschedulingId, visits.data]);

  const filtered = useMemo(() => {
    const rows = visits.data ?? [];
    return rows
      .filter((item) => classifyPatientVisitTab(item, nowMs) === tab)
      .sort((a, b) => new Date(a.startsAtUtc).getTime() - new Date(b.startsAtUtc).getTime());
  }, [tab, visits.data, nowMs]);

  const refresh = useCallback(async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['appointments', 'mine'] }),
      queryClient.invalidateQueries({ queryKey: ['reviews', 'eligible'] }),
      queryClient.invalidateQueries({ queryKey: ['reviews', 'mine'] }),
    ]);
  }, [queryClient]);
  const { refreshing, onRefresh } = useManualRefresh(refresh);

  const withOnlineGuard = async <T,>(action: string, run: () => Promise<T>) => {
    try {
      assertMutationAllowedOnline(action, online);
      setGuardError(null);
      return await run();
    } catch (error) {
      if (error instanceof Error && error.message === 'offline_action_blocked') {
        setGuardError(copy.offlineActionBlocked);
        throw error;
      }
      throw error;
    }
  };

  const cancel = useMutation({
    mutationFn: ({ id, cancelReason }: { id: string; cancelReason: string }) =>
      withOnlineGuard('booking.confirm', () => cancelAppointment(id, cancelReason)),
    onSuccess: async () => {
      setCancelReasonId(null);
      setReason('');
      setActionError(null);
      await refresh();
      Alert.alert(copy.cancelVisit, copy.appointmentCancelledSuccess);
    },
    onError: (error) => {
      const message = problemMessage(error, copy.cancelVisit);
      setActionError(message);
      Alert.alert(copy.cancelVisit, message);
    },
  });
  const move = useMutation({
    mutationFn: ({ id, nextDate, nextTime, moveReason }: { id: string; nextDate: string; nextTime: string; moveReason: string }) =>
      withOnlineGuard('booking.confirm', () =>
        rescheduleAppointment(id, nextDate, normalizeSlotStart(nextTime), moveReason || undefined),
      ),
    onSuccess: async () => {
      setReschedulingId(null);
      setTime('');
      setReason('');
      setActionError(null);
      await refresh();
      Alert.alert(copy.reschedule, copy.appointmentRescheduledSuccess);
    },
    onError: (error) => {
      const message = problemMessage(error, copy.reschedule);
      setActionError(message);
      Alert.alert(copy.reschedule, message);
    },
  });
  const pay = useMutation({
    mutationFn: async (input: { id: string; couponCode?: string; useRewardCredit?: boolean }) =>
      withOnlineGuard('payment.create', async () => {
        const created = await createAppointmentPayment(input.id, 'PayOnline', `visit-${input.id}-${Date.now()}`, {
          couponCode: input.couponCode,
          useRewardCredit: input.useRewardCredit,
        });
        return verifyPayment(created.id);
      }),
    onSuccess: async () => {
      setActionError(null);
      await refresh();
      Alert.alert(copy.payAtClinic, copy.paymentSuccess);
    },
    onError: (error) => {
      const message = problemMessage(error, copy.paymentFailed);
      setActionError(message);
      Alert.alert(copy.payAtClinic, message);
    },
  });

  const listError = visits.error ?? pay.error;
  const cachedNote = !online && (visits.data?.length ?? 0) > 0 ? copy.offlineCachedData : null;

  return (
    <AppShell active="visits">
      <ScreenList
        data={filtered}
        keyExtractor={(item) => item.id}
        refreshing={refreshing}
        onRefresh={onRefresh}
        header={
          <>
            <ScreenHeader title={copy.myVisits} />
            <Segmented
              value={tab}
              options={[
                { value: 'upcoming', label: copy.upcoming },
                { value: 'past', label: copy.past },
                { value: 'cancelled', label: copy.cancelled },
              ]}
              onChange={setTab}
            />
            {tab === 'upcoming' && openFollowUps.length > 0 ? (
              <View style={{ gap: spacing.md, marginBottom: spacing.sm }}>
                <AppText variant="label">Follow-up Reminders</AppText>
                {openFollowUps.map((item) => (
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
              </View>
            ) : null}
            {cachedNote ? <AppText variant="muted">{cachedNote}</AppText> : null}
            {guardError ? <AppText variant="muted">{guardError}</AppText> : null}
            {actionError ? <AppText variant="muted">{actionError}</AppText> : null}
            {listError ? <AppText variant="muted">{problemMessage(listError, copy.myVisits)}</AppText> : null}
          </>
        }
        empty={<EmptyState title={copy.noVisits} body={copy.loginSubtitle} />}
        renderItem={({ item, index }) => (
          <VisitCard
            visit={item}
            index={index}
            rescheduling={reschedulingId === item.id}
            cancelling={cancelReasonId === item.id}
            date={date}
            time={time}
            reason={reason}
            onDate={setDate}
            onTime={setTime}
            onReason={setReason}
            nowMs={nowMs}
            onStartReschedule={() => {
              if (!canPatientRescheduleVisit(item, nowMs)) {
                Alert.alert(copy.reschedule, item.cancellationPolicy || copy.changeNotAllowed);
                return;
              }
              setActionError(null);
              setCancelReasonId(null);
              cancel.reset();
              setReschedulingId(item.id);
              const sameDay = canPatientSameDayReschedule(item, nowMs);
              setDate(sameDay ? clinicDateIso() : todayYmd());
              setTime('');
              setReason('');
            }}
            onCloseReschedule={() => {
              setReschedulingId(null);
              setTime('');
              move.reset();
              setActionError(null);
            }}
            onStartCancel={() => {
              if (!canPatientCancelVisit(item, nowMs)) {
                Alert.alert(copy.cancelVisit, item.cancellationPolicy || copy.changeNotAllowed);
                return;
              }
              setActionError(null);
              setReschedulingId(null);
              move.reset();
              setCancelReasonId(item.id);
              setReason('');
            }}
            onCloseCancel={() => {
              setCancelReasonId(null);
              setReason('');
              cancel.reset();
              setActionError(null);
            }}
            onCancel={() => {
              if (!reason.trim()) {
                setActionError(copy.cancelReasonRequired || 'This field is required.');
                Alert.alert(copy.cancelVisit, copy.cancelReasonRequired || 'This field is required.');
                return;
              }
              if (!canPatientCancelVisit(item, nowMs)) {
                Alert.alert(copy.cancelVisit, item.cancellationPolicy || copy.changeNotAllowed);
                return;
              }
              setActionError(null);
              Alert.alert(copy.cancelVisit, copy.confirmCancelVisit, [
                { text: copy.closeReschedule, style: 'cancel' },
                {
                  text: copy.cancelVisit,
                  style: 'destructive',
                  onPress: () => cancel.mutate({ id: item.id, cancelReason: reason.trim() }),
                },
              ]);
            }}
            onMove={() => {
              if (!time) {
                setActionError(copy.selectTimeRequired);
                Alert.alert(copy.reschedule, copy.selectTimeRequired);
                return;
              }
              if (!canPatientRescheduleVisit(item, nowMs)) {
                Alert.alert(copy.reschedule, item.cancellationPolicy || copy.changeNotAllowed);
                return;
              }
              setActionError(null);
              const sameDay = canPatientSameDayReschedule(item, nowMs);
              Alert.alert(copy.reschedule, copy.confirmReschedule, [
                { text: copy.closeReschedule, style: 'cancel' },
                {
                  text: copy.confirmReschedule,
                  onPress: () =>
                    move.mutate({
                      id: item.id,
                      nextDate: sameDay ? clinicDateIso() : date,
                      nextTime: time,
                      moveReason: reason.trim(),
                    }),
                },
              ]);
            }}
            onPay={(options) => pay.mutate({ id: item.id, ...options })}
            paying={pay.isPending && pay.variables?.id === item.id}
            cancelPending={cancel.isPending && cancel.variables?.id === item.id}
            movePending={move.isPending && move.variables?.id === item.id}
            online={online}
            existingReview={myReviewByAppointment.get(item.id) ?? null}
            canWriteReview={!myReviewByAppointment.has(item.id) && reviewableIds.has(item.id)}
            onWriteReview={() =>
              router.push(`/write-review?appointmentId=${encodeURIComponent(item.id)}` as Href)
            }
            onViewReview={(review) =>
              router.push(
                `/write-review?appointmentId=${encodeURIComponent(review.appointmentId)}&targetKind=${encodeURIComponent(review.targetKind)}&reviewId=${encodeURIComponent(review.id)}&mode=view` as Href,
              )
            }
            onEditReview={(review) =>
              router.push(
                `/write-review?appointmentId=${encodeURIComponent(review.appointmentId)}&targetKind=${encodeURIComponent(review.targetKind)}&reviewId=${encodeURIComponent(review.id)}&mode=edit` as Href,
              )
            }
            onShowQr={() => router.push(`/check-in/${item.id}` as Href)}
          />
        )}
      />
    </AppShell>
  );
}

const VisitCard = memo(function VisitCard({
  visit,
  index,
  rescheduling,
  cancelling,
  date,
  time,
  reason,
  nowMs,
  onDate,
  onTime,
  onReason,
  onStartReschedule,
  onCloseReschedule,
  onStartCancel,
  onCloseCancel,
  onCancel,
  onMove,
  onPay,
  paying,
  cancelPending,
  movePending,
  online,
  existingReview,
  canWriteReview,
  onWriteReview,
  onViewReview,
  onEditReview,
  onShowQr,
}: {
  visit: AppointmentDetails;
  index: number;
  rescheduling: boolean;
  cancelling: boolean;
  date: string;
  time: string;
  reason: string;
  nowMs: number;
  onDate: (value: string) => void;
  onTime: (value: string) => void;
  onReason: (value: string) => void;
  onStartReschedule: () => void;
  onCloseReschedule: () => void;
  onStartCancel: () => void;
  onCloseCancel: () => void;
  onCancel: () => void;
  onMove: () => void;
  onPay: (options?: { couponCode?: string; useRewardCredit?: boolean }) => void;
  paying: boolean;
  cancelPending: boolean;
  movePending: boolean;
  online: boolean;
  existingReview: ReviewDetails | null;
  canWriteReview: boolean;
  onWriteReview: () => void;
  onViewReview: (review: ReviewDetails) => void;
  onEditReview: (review: ReviewDetails) => void;
  onShowQr: () => void;
}) {
  const copy = useCopy();
  const theme = useProdocTheme();
  const [shareOpen, setShareOpen] = useState(false);
  const [couponCode, setCouponCode] = useState('');
  const [useRewardCredit, setUseRewardCredit] = useState(false);
  const canCancel = canPatientCancelVisit(visit, nowMs);
  const canReschedule = canPatientRescheduleVisit(visit, nowMs);
  const sameDayReschedule = canPatientSameDayReschedule(visit, nowMs);
  const canPayOnline =
    visit.paymentStatus !== 'Paid' &&
    (visit.paymentMethod === 'PayOnline' || visit.paymentMethod === 'PartialOnline') &&
    (visit.status === 'Confirmed' || visit.status === 'Requested' || visit.status === 'CheckedIn');
  const showQr = visit.status === 'Confirmed';
  const doctorName = formatDoctorDisplayName(visit.doctorName);
  const clinicName = clinicPlaceLabel(visit.clinicName, visit.branchName);
  const visitDate = formatVisitDate(visit.startsAtUtc, visit.timeZoneId);
  const visitTime = formatVisitTime(visit.startsAtUtc, visit.timeZoneId);
  const paymentLabel = paymentStatusLabel(visit.paymentStatus, visit.paymentMethod, {
    paid: copy.paid,
    payAtClinic: copy.payAtClinic,
    paymentPending: copy.paymentPending,
  });
  const bookingWindow = useMemo(() => patientBookingWindow(), []);
  const [weekCursor, setWeekCursor] = useState(() => startOfClinicWeekMonday());

  const availability = useQuery({
    queryKey: ['availability', visit.doctorId, visit.branchId, bookingWindow.start, bookingWindow.end, 'reschedule'],
    queryFn: () => getAvailability(visit.doctorId, bookingWindow.start, bookingWindow.end, visit.branchId || undefined),
    enabled: rescheduling && Boolean(visit.doctorId),
    retry: 1,
    staleTime: 30_000,
  });

  const scheduleId = useMemo(() => {
    if (visit.scheduleId) return visit.scheduleId;
    return availability.data?.find((item) => item.date === date)?.scheduleId ?? null;
  }, [availability.data, date, visit.scheduleId]);

  const daySlots = useQuery({
    queryKey: ['slots', scheduleId, date, visit.id, 'reschedule'],
    queryFn: () => getSlots(scheduleId!, date, visit.id),
    enabled: rescheduling && Boolean(scheduleId) && Boolean(date),
    retry: 1,
  });

  const statusLabel: Record<string, string> = {
    available: copy.available,
    unavailable: copy.unavailable,
    full: copy.full,
    holiday: copy.holiday,
    leave: copy.onLeave,
    partial: copy.partiallyAvailable,
  };

  const availabilityLoading = availability.isPending && !availability.data && !availability.isError;
  const slotsLoading = daySlots.isPending && !daySlots.data && !daySlots.isError;

  return (
    <Animated.View
      layout={theme.reduceMotion ? undefined : Layout.springify()}
      entering={theme.reduceMotion ? undefined : FadeInDown.delay(Math.min(index, 6) * 40).duration(duration.enter)}>
      <Card elevated style={{ gap: spacing.md, padding: spacing.lg }}>
        <View style={{ gap: spacing.md }}>
          <View style={{ flexDirection: 'row', gap: spacing.md, alignItems: 'flex-start' }}>
            <ProfileAvatar name={doctorName} size={56} />
            <View style={{ flex: 1, gap: spacing.xs }}>
              <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                {copy.doctorLabel}
              </AppText>
              <AppText variant="headline" style={{ color: theme.colors.text }}>
                {doctorName}
              </AppText>
              {visit.doctorSpecialization ? (
                <AppText variant="muted">{visit.doctorSpecialization}</AppText>
              ) : null}
              <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm, alignItems: 'center' }}>
                {visit.doctorVerified ? <VerificationBadge label={copy.verified} /> : null}
                <StatusBadge label={label(copy, visit.status)} tone={statusTone(visit.status)} />
              </View>
            </View>
          </View>

          {visit.tokenLabel || visit.status === 'Confirmed' || visit.status === 'CheckedIn' || visit.status === 'Completed' ? (
            <View
              style={{
                backgroundColor: theme.colors.primaryLight,
                borderRadius: radius.lg,
                paddingVertical: spacing.md,
                paddingHorizontal: spacing.lg,
                alignItems: 'center',
                gap: 2,
              }}>
              <AppText variant="caption" style={{ color: theme.colors.primaryPressed, letterSpacing: 0.8 }}>
                {copy.tokenNumber.toUpperCase()}
              </AppText>
              <AppText
                variant="display"
                style={{ color: theme.colors.primaryPressed, fontSize: 32, lineHeight: 38, fontWeight: '700' }}>
                {visit.tokenLabel ?? '—'}
              </AppText>
            </View>
          ) : null}

          <View style={{ gap: spacing.sm }}>
            <MetaRow labelText={copy.clinicLabel} value={clinicName || '—'} />
            {visit.address ? (
              <ClinicLocationCard
                title={copy.clinicLocation}
                clinicName={clinicName}
                formattedAddress={visit.address}
                latitude={visit.latitude}
                longitude={visit.longitude}
                googlePlaceId={visit.googlePlaceId}
                mapsUrl={visit.mapsUrl}
                directionsUrl={visit.directionsUrl}
                locationVerified={visit.locationVerified}
                unavailableLabel={copy.clinicLocationUnavailable}
                viewOnMapLabel={copy.viewOnGoogleMaps}
                getDirectionsLabel={copy.getDirections}
                unverifiedHint={copy.clinicLocationUnverified}
              />
            ) : null}
            <MetaRow labelText={copy.appointmentId} value={visit.reference ?? visit.id} />
            <View style={{ flexDirection: 'row', gap: spacing.lg }}>
              <View style={{ flex: 1 }}>
                <MetaRow labelText={copy.appointmentDate} value={visitDate} />
              </View>
              <View style={{ flex: 1 }}>
                <MetaRow labelText={copy.appointmentTime} value={visitTime} />
              </View>
            </View>
            <MetaRow labelText={copy.patientLabel} value={visit.patientName || '—'} />
            <View style={{ flexDirection: 'row', gap: spacing.lg }}>
              <View style={{ flex: 1 }}>
                <MetaRow
                  labelText={copy.consultationFee}
                  value={typeof visit.consultationFee === 'number' ? `₹${visit.consultationFee}` : '—'}
                />
              </View>
              <View style={{ flex: 1 }}>
                <MetaRow labelText={copy.paymentStatus} value={paymentLabel} />
              </View>
            </View>
          </View>
        </View>

        {canPayOnline ? (
          <View style={{ gap: spacing.sm }}>
            <TextField
              label="Coupon code (optional)"
              value={couponCode}
              onChangeText={setCouponCode}
              autoCapitalize="characters"
            />
            <Pressable
              onPress={() => setUseRewardCredit((value) => !value)}
              style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm }}>
              <View
                style={{
                  width: 20,
                  height: 20,
                  borderRadius: 6,
                  borderWidth: 1,
                  borderColor: theme.colors.primary,
                  backgroundColor: useRewardCredit ? theme.colors.primary : 'transparent',
                }}
              />
              <AppText variant="body">Apply ProDoc reward credit</AppText>
            </Pressable>
            <Button
              label={copy.payOnline}
              loading={paying}
              disabled={!online}
              onPress={() =>
                onPay({
                  couponCode: couponCode.trim() || undefined,
                  useRewardCredit,
                })
              }
            />
          </View>
        ) : null}
        {existingReview ? (
          <View style={{ flexDirection: 'row', gap: spacing.sm }}>
            <Button
              label={copy.reviewViewCta}
              variant="secondary"
              style={{ flex: 1 }}
              onPress={() => onViewReview(existingReview)}
            />
            {existingReview.canEdit ? (
              <Button
                label={copy.reviewEditCta}
                variant="secondary"
                style={{ flex: 1 }}
                disabled={!online}
                onPress={() => onEditReview(existingReview)}
              />
            ) : null}
          </View>
        ) : canWriteReview ? (
          <Button label={copy.reviewWriteCta} variant="secondary" disabled={!online} onPress={onWriteReview} />
        ) : null}
        {showQr ? <Button label={copy.showCheckInQr} variant="secondary" onPress={onShowQr} /> : null}
        {visit.status !== 'Held' && visit.status !== 'Expired' ? (
          <ShareAppointmentButton label={copy.shareAppointment} onPress={() => setShareOpen(true)} />
        ) : null}
        <ShareAppointmentSheet visible={shareOpen} appointment={visit} onClose={() => setShareOpen(false)} />
        {canReschedule && !rescheduling && !cancelling ? (
          <Button label={copy.reschedule} variant="secondary" disabled={!online} onPress={onStartReschedule} />
        ) : null}
        {canCancel && !cancelling && !rescheduling ? (
          <Button label={copy.cancelVisit} variant="danger" disabled={!online} onPress={onStartCancel} />
        ) : null}

        {rescheduling && canReschedule ? (
          <Animated.View
            entering={theme.reduceMotion ? undefined : FadeInDown.duration(duration.enter)}
            style={{ gap: spacing.md }}>
            {sameDayReschedule ? (
              <AppText variant="muted">
                Same-day reschedule for {clinicDateIso()}. Choose a remaining open slot today.
              </AppText>
            ) : (
              <>
                <AppText variant="label">{copy.selectDate}</AppText>
                {availabilityLoading ? <AppText variant="muted">{copy.loading}</AppText> : null}
                {availability.isError ? (
                  <View style={{ gap: spacing.sm }}>
                    <AppText variant="muted">{problemMessage(availability.error, copy.selectDate)}</AppText>
                    <Button label={copy.retryAvailability} variant="secondary" onPress={() => void availability.refetch()} />
                  </View>
                ) : null}
                <CalendarView
                  mode="bookingWeeks"
                  days={availability.data ?? []}
                  selectedDate={date}
                  onSelect={(next) => {
                    onDate(next);
                    onTime('');
                  }}
                  statusLabels={statusLabel}
                  weekCursor={weekCursor}
                  onWeekChange={setWeekCursor}
                />
              </>
            )}
            <AppText variant="label">{copy.selectTime}</AppText>
            {slotsLoading ? <AppText variant="muted">{copy.loading}</AppText> : null}
            {daySlots.isError ? (
              <View style={{ gap: spacing.sm }}>
                <AppText variant="muted">{problemMessage(daySlots.error, copy.selectTime)}</AppText>
                <Button label={copy.retryAvailability} variant="secondary" onPress={() => void daySlots.refetch()} />
              </View>
            ) : null}
            {!scheduleId && !availabilityLoading && availability.isSuccess ? (
              <AppText variant="muted">{copy.noSlotsOnDate}</AppText>
            ) : null}
            <TimeSlotSelector
              slots={(daySlots.data?.slots ?? []).map((slot) => ({
                start: slot.start,
                end: slot.end,
                disabled: slot.available === false,
              }))}
              selectedStart={time}
              busy={movePending || slotsLoading}
              emptyLabel={!slotsLoading && !daySlots.isError ? copy.noSlotsOnDate : undefined}
              onSelect={(start) => {
                const slot = daySlots.data?.slots.find((item) => item.start === start);
                if (slot?.available === false) return;
                onTime(start);
              }}
            />
            <TextField label={copy.reason} value={reason} onChangeText={onReason} placeholder={copy.reason} />
            <Button
              label={copy.confirmReschedule}
              loading={movePending}
              disabled={!online || !time || movePending}
              onPress={onMove}
            />
            <Button label={copy.closeReschedule} variant="secondary" disabled={movePending} onPress={onCloseReschedule} />
            {visit.cancellationPolicy ? <AppText variant="muted">{visit.cancellationPolicy}</AppText> : null}
          </Animated.View>
        ) : null}

        {cancelling && canCancel ? (
          <Animated.View
            entering={theme.reduceMotion ? undefined : FadeInDown.duration(duration.enter)}
            style={{ gap: spacing.md }}>
            <TextField label={copy.reason} value={reason} onChangeText={onReason} />
            <Button
              label={copy.cancelVisit}
              variant="danger"
              loading={cancelPending}
              disabled={!online || cancelPending}
              onPress={onCancel}
            />
            <Button label={copy.closeReschedule} variant="secondary" disabled={cancelPending} onPress={onCloseCancel} />
            {visit.cancellationPolicy ? <AppText variant="muted">{visit.cancellationPolicy}</AppText> : null}
          </Animated.View>
        ) : null}
      </Card>
    </Animated.View>
  );
});
