import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useEffect, useMemo, useRef, useState } from 'react';
import { Linking, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import Animated, { FadeInDown } from 'react-native-reanimated';

import { Screen } from '@/components/layout/auth-scaffold';
import { FormScreenScroll } from '@/components/layout/keyboard-aware-form';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { CalendarView } from '@/components/ui/calendar-view';
import { Card } from '@/components/ui/card';
import {
  ShareAppointmentButton,
  ShareAppointmentSheet,
} from '@/components/appointments/share-appointment-sheet';
import { ClinicLocationCard } from '@/components/ui/clinic-location-card';
import { ErrorState } from '@/components/ui/error-state';
import { ProfileAvatar } from '@/components/ui/profile-avatar';
import { Segmented } from '@/components/ui/segmented';
import { BookingSuccessMark } from '@/components/ui/success-mark';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { formatHourlyRangeLabel, HourlyRangeSelector } from '@/components/ui/hourly-range-selector';
import { TimeSlotSelector } from '@/components/ui/time-slot-selector';
import { VerificationBadge } from '@/components/ui/verification-badge';
import {
  clinicDateIso,
  isValidCalendarDate,
  patientBookingWindow,
  startOfClinicWeekMonday,
} from '@/features/providers/schedule-day';
import { useCopy } from '@/i18n/copy';
import { futureIsoDateSchema, patientBookingNameSchema, timeSlotSchema } from '@/schemas/common';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { duration, layout, spacing } from '@/theme/tokens';
import {
  addFamily,
  confirmAppointment,
  getAvailability,
  getDiscoveredDoctor,
  getSlots,
  holdAppointment,
  listAppointments,
  listFamily,
  releaseAppointment,
  type AppointmentDetails,
} from '@/services/api/provider-api';
import { assertMutationAllowedOnline } from '@/services/offline/offline-queue';
import { selectIsEffectivelyOnline, useNetworkStore } from '@/stores/network-store';
import { useSessionStore } from '@/stores/session-store';
import { notifySuccess, notifyWarning } from '@/stores/feedback-store';
import { homeHrefForAccount } from '@/utils/home-surface';
import { patientHasActiveBookingWithDoctorOnDate } from '@/utils/patient-appointment-actions';
import { photoUriFor } from '@/utils/provider-photo';
import { notifyApiError, problemMessage } from '@/utils/problem';
import { validateField } from '@/utils/validation';
import { formatYearsExperience } from '@/utils/years-of-experience';

function stamp(value: string) {
  return new Date(value).toISOString().replace(/[-:]/g, '').replace(/\.\d{3}/, '');
}

function normalizeSlotStart(start: string) {
  const trimmed = start.trim();
  if (/^\d{2}:\d{2}$/.test(trimmed)) return `${trimmed}:00`;
  return trimmed;
}

type VisitTarget =
  | { kind: 'self' }
  | { kind: 'member'; id: string }
  | { kind: 'new'; relationship: 'child' | 'spouse' | 'parent' | 'other' };

export function BookVisit({
  scheduleId,
  doctorId,
  branchId,
  initialDate,
  initialStart,
  followUpId,
}: {
  scheduleId: string;
  doctorId: string;
  branchId: string;
  initialDate?: string;
  initialStart?: string;
  followUpId?: string;
}) {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const account = useSessionStore((state) => state.account);
  const accessToken = useSessionStore((state) => state.accessToken);
  const queryClient = useQueryClient();
  const online = useNetworkStore(selectIsEffectivelyOnline);
  const bookingWindow = useMemo(() => patientBookingWindow(), []);
  const seededDate = initialDate && isValidCalendarDate(initialDate) ? initialDate : clinicDateIso();
  const [date, setDate] = useState(seededDate);
  const [weekCursor, setWeekCursor] = useState(() => startOfClinicWeekMonday(seededDate));
  const [hold, setHold] = useState<AppointmentDetails | null>(null);
  const [receipt, setReceipt] = useState<AppointmentDetails | null>(null);
  const autoHoldKey = useRef<string | null>(null);
  const [skipAutoHold, setSkipAutoHold] = useState(false);
  const [visitTarget, setVisitTarget] = useState<VisitTarget>({ kind: 'self' });
  const [reason, setReason] = useState('');
  const [notes, setNotes] = useState('');
  const [patientName, setPatientName] = useState(() => account?.fullName?.trim() ?? '');
  const [patientNameError, setPatientNameError] = useState<string | null>(null);
  const [offlineError, setOfflineError] = useState<string | null>(null);
  const [shareOpen, setShareOpen] = useState(false);
  const profileName = account?.fullName?.trim() ?? '';
  const doctor = useQuery({
    queryKey: ['discovery', 'doctor', doctorId],
    queryFn: () => getDiscoveredDoctor(doctorId),
    enabled: Boolean(doctorId),
  });
  const calendar = useQuery({
    queryKey: ['availability', doctorId, branchId, bookingWindow.start, bookingWindow.end, 'book'],
    queryFn: () => getAvailability(doctorId, bookingWindow.start, bookingWindow.end, branchId || undefined),
    enabled: Boolean(doctorId),
  });
  const slots = useQuery({
    queryKey: ['slots', scheduleId, date],
    queryFn: () => getSlots(scheduleId, date),
    enabled: Boolean(scheduleId) && !hold && !receipt,
  });
  const family = useQuery({ queryKey: ['family'], queryFn: listFamily });
  const myAppointments = useQuery({
    queryKey: ['appointments', 'mine'],
    queryFn: listAppointments,
    staleTime: 30_000,
  });

  // Keep Patient Name in sync with Myself / saved member. New-family clears happen on selection.
  useEffect(() => {
    if (visitTarget.kind === 'self') {
      setPatientName(profileName);
      setPatientNameError(null);
      return;
    }
    if (visitTarget.kind === 'member') {
      const member = (family.data ?? []).find((item) => item.id === visitTarget.id);
      setPatientName(member?.fullName?.trim() ?? '');
      setPatientNameError(null);
    }
  }, [visitTarget, profileName, family.data]);
  const sameDayBooking = useMemo(
    () => patientHasActiveBookingWithDoctorOnDate(myAppointments.data, doctorId, date),
    [myAppointments.data, doctorId, date],
  );
  const place = doctor.data?.practices.find((item) => item.scheduleId === scheduleId);
  const next = useMemo(
    () => calendar.data?.find((day) => day.status === 'available' || day.status === 'partial'),
    [calendar.data],
  );
  const statusLabel: Record<string, string> = {
    available: copy.available,
    unavailable: copy.unavailable,
    full: copy.full,
    holiday: copy.holiday,
    leave: copy.onLeave,
    partial: copy.partiallyAvailable,
  };
  const photo = photoUriFor(doctor.data?.practices[0]?.photoDocumentId ?? place?.photoDocumentId, accessToken);

  const reserve = useMutation({
    mutationFn: async (start: string) => {
      assertMutationAllowedOnline('booking.hold', online);
      setOfflineError(null);
      const dateError = validateField(futureIsoDateSchema, date);
      if (dateError) {
        setOfflineError(dateError);
        notifyWarning(dateError);
        throw new Error(dateError);
      }
      const timeError = validateField(timeSlotSchema, start);
      if (timeError) {
        setOfflineError(timeError);
        notifyWarning(timeError);
        throw new Error(timeError);
      }
      if (!scheduleId) {
        const message = copy.selectTimeRequired;
        setOfflineError(message);
        notifyWarning(message);
        throw new Error(message);
      }
      const duplicate = patientHasActiveBookingWithDoctorOnDate(myAppointments.data, doctorId, date);
      if (duplicate && duplicate.id !== hold?.id) {
        const message = copy.duplicateAppointmentSameDate;
        setOfflineError(message);
        notifyWarning(message);
        throw new Error(message);
      }
      if (hold) {
        await releaseAppointment(hold.id);
      }
      return holdAppointment(scheduleId, date, normalizeSlotStart(start));
    },
    onSuccess: (nextHold) => {
      setHold(nextHold);
      void queryClient.invalidateQueries({ queryKey: ['slots', scheduleId, date] });
    },
    onError: (error) => {
      if (error instanceof Error && error.message === 'offline_action_blocked') {
        setOfflineError(copy.offlineActionBlocked);
        notifyApiError(error, copy.offlineActionBlocked);
        return;
      }
      const message = problemMessage(error, copy.selectTimeRequired);
      setOfflineError(message);
      notifyApiError(error, copy.selectTimeRequired);
    },
  });

  useEffect(() => {
    if (skipAutoHold || !initialStart || !scheduleId || hold || receipt || !online) return;
    const key = `${scheduleId}:${date}:${initialStart}`;
    if (autoHoldKey.current === key || reserve.isPending) return;
    autoHoldKey.current = key;
    reserve.mutate(initialStart);
    // Intentionally run once when landing with a preselected slot.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [initialStart, scheduleId, date, online, skipAutoHold]);
  const confirm = useMutation({
    mutationFn: async () => {
      assertMutationAllowedOnline('booking.confirm', online);
      setOfflineError(null);
      if (!hold?.id) {
        throw new Error(copy.selectTimeRequired);
      }

      if (visitTarget.kind === 'self') {
        const name = profileName || patientName.trim();
        if (!name) {
          const message = copy.patientNameMandatory;
          setPatientNameError(message);
          throw new Error(message);
        }
        return confirmAppointment(hold.id, {
          reason: reason.trim() || undefined,
          notes: notes.trim() || undefined,
          followUpId: followUpId || undefined,
        });
      }

      if (visitTarget.kind === 'member') {
        const member = (family.data ?? []).find((item) => item.id === visitTarget.id);
        if (!member?.fullName?.trim()) {
          const message = copy.patientNameMandatory;
          setPatientNameError(message);
          throw new Error(message);
        }
        return confirmAppointment(hold.id, {
          familyMemberId: visitTarget.id,
          reason: reason.trim() || undefined,
          notes: notes.trim() || undefined,
          followUpId: followUpId || undefined,
        });
      }

      // New family member for this visit — create then confirm with that record.
      const parsed = patientBookingNameSchema.safeParse(patientName);
      if (!parsed.success) {
        const message = parsed.error.issues[0]?.message ?? copy.patientNameMandatory;
        setPatientNameError(message);
        throw new Error(message);
      }
      const member = await addFamily({
        fullName: parsed.data,
        relationship: visitTarget.relationship,
      });
      await queryClient.invalidateQueries({ queryKey: ['family'] });
      setVisitTarget({ kind: 'member', id: member.id });
      return confirmAppointment(hold.id, {
        familyMemberId: member.id,
        reason: reason.trim() || undefined,
        notes: notes.trim() || undefined,
        followUpId: followUpId || undefined,
      });
    },
    onSuccess: (visit) => {
      setReceipt(visit);
      setHold(null);
      setPatientNameError(null);
      notifySuccess(copy.appointmentBookedSuccess);
      void queryClient.invalidateQueries({ queryKey: ['appointments'] });
    },
    onError: (error) => {
      if (error instanceof Error && error.message === 'offline_action_blocked') {
        setOfflineError(copy.offlineActionBlocked);
        notifyApiError(error, copy.offlineActionBlocked);
        return;
      }
      const message = problemMessage(error, copy.bookingFailed);
      if (
        message === copy.patientNameMandatory ||
        (error instanceof Error && error.message === copy.patientNameMandatory)
      ) {
        setPatientNameError(copy.patientNameMandatory);
        return;
      }
      setOfflineError(message);
      notifyApiError(error, copy.bookingFailed);
    },
  });

  const calendarUrl = receipt
    ? `https://calendar.google.com/calendar/render?action=TEMPLATE&text=${encodeURIComponent(receipt.doctorName)}&dates=${stamp(receipt.startsAtUtc)}/${stamp(receipt.endsAtUtc)}&location=${encodeURIComponent(receipt.address)}&details=${encodeURIComponent(receipt.reference ?? receipt.id)}`
    : null;
  const error = reserve.error ?? confirm.error ?? calendar.error ?? slots.error;
  const patientNameEditable = visitTarget.kind === 'new';
  const selectedRelationship =
    visitTarget.kind === 'new'
      ? visitTarget.relationship
      : visitTarget.kind === 'member'
        ? ((family.data ?? []).find((item) => item.id === visitTarget.id)?.relationship?.toLowerCase() ??
          'other')
        : 'self';
  const enter = theme.reduceMotion ? undefined : FadeInDown.duration(duration.enter);
  const needsKeyboard = Boolean(hold && !receipt);

  return (
    <Screen>
      <ScreenHeader
        title={receipt ? copy.appointmentConfirmed : (doctor.data?.displayName ?? copy.findDoctor)}
        showBack={!receipt}
      />
      <FormScreenScroll
        toolbar={needsKeyboard}
        contentContainerStyle={{
          flexGrow: 1,
          width: '100%',
          maxWidth: '100%',
          alignSelf: 'stretch',
          paddingHorizontal: layout.contentPad,
          paddingTop: spacing.md,
          paddingBottom: Math.max(insets.bottom, spacing.md) + spacing.xl,
          gap: spacing.lg,
        }}>
        {!online ? <AppText variant="muted" style={{ color: theme.colors.error }}>{copy.offlineActionBlocked}</AppText> : null}
        {offlineError ? (
          <AppText variant="muted" style={{ color: theme.colors.error }}>
            {offlineError}
          </AppText>
        ) : null}
        {doctor.error ? (
          <ErrorState
            title={copy.findDoctor}
            body={problemMessage(doctor.error, copy.findDoctor)}
            actionLabel={copy.continue}
            onAction={() => void doctor.refetch()}
          />
        ) : null}
        {doctor.data ? (
          <Animated.View entering={enter} style={{ width: '100%', maxWidth: '100%' }}>
            <Card elevated style={{ width: '100%', maxWidth: '100%', gap: spacing.md }}>
              <View style={{ flexDirection: 'row', gap: spacing.md, alignItems: 'flex-start', width: '100%' }}>
                <ProfileAvatar
                  name={doctor.data.displayName}
                  uri={photo?.uri}
                  headers={photo?.headers}
                  size={64}
                />
                <View style={{ flex: 1, minWidth: 0, gap: 4 }}>
                  <AppText variant="title" style={{ fontSize: 20, lineHeight: 26 }} numberOfLines={2}>
                    {doctor.data.displayName}
                  </AppText>
                  <VerificationBadge label={copy.verified} />
                  <AppText variant="muted" numberOfLines={2}>
                    {doctor.data.primarySpecialization}
                    {` · ${formatYearsExperience(doctor.data.yearsOfExperience, copy.yearsExp)}`}
                  </AppText>
                </View>
              </View>
              {place ? (
                <View
                  style={{
                    gap: spacing.xs,
                    paddingTop: spacing.sm,
                    borderTopWidth: 1,
                    borderTopColor: theme.colors.border,
                    width: '100%',
                  }}>
                  <AppText numberOfLines={2}>
                    {[place.clinicName, place.branchName]
                      .map((part) => part?.trim())
                      .filter(Boolean)
                      .filter((part, index, all) => index === 0 || part?.toLowerCase() !== all[0]?.toLowerCase())
                      .join(' · ')}
                  </AppText>
                  <AppText variant="muted" numberOfLines={2}>
                    {[place.addressLine, place.city].filter(Boolean).join(', ')}
                  </AppText>
                  <AppText variant="label" style={{ color: theme.colors.primary }}>
                    {copy.consultationFee}: ₹{place.consultationFee}
                    {place.distanceKm != null ? ` · ${place.distanceKm.toFixed(1)} ${copy.distance}` : ''}
                  </AppText>
                </View>
              ) : null}
            </Card>
          </Animated.View>
        ) : null}
        {receipt ? (
          <Card elevated style={{ width: '100%', maxWidth: '100%', alignItems: 'center', gap: spacing.lg }}>
            <BookingSuccessMark />
            <AppText variant="title">{copy.appointmentConfirmed}</AppText>
            <AppText variant="muted">{receipt.status === 'Confirmed' ? copy.confirmed : copy.requested}</AppText>
            <Card style={{ alignSelf: 'stretch', width: '100%', gap: spacing.sm }}>
              <AppText variant="label">{copy.bookingReference}</AppText>
              <AppText variant="headline">{receipt.reference}</AppText>
              <AppText>{receipt.doctorName}</AppText>
              <AppText>
                {[receipt.clinicName, receipt.branchName]
                  .map((part) => part?.trim())
                  .filter(Boolean)
                  .filter((part, index, all) => index === 0 || part?.toLowerCase() !== all[0]?.toLowerCase())
                  .join(' · ')}
              </AppText>
              {receipt.tokenLabel ? (
                <AppText variant="headline">{receipt.tokenLabel}</AppText>
              ) : null}
              {receipt.bookingMode === 'HourlyQueue' && receipt.tokenNumber != null ? (
                <AppText variant="muted">
                  {copy.queuePosition}: {receipt.tokenNumber}
                </AppText>
              ) : null}
              <AppText>
                {receipt.bookingMode === 'HourlyQueue'
                  ? `${new Date(receipt.startsAtUtc).toLocaleDateString()} · ${formatHourlyRangeLabel(
                      new Date(receipt.startsAtUtc).toLocaleTimeString('en-GB', {
                        hour: '2-digit',
                        minute: '2-digit',
                        hour12: false,
                      }),
                      new Date(receipt.endsAtUtc).toLocaleTimeString('en-GB', {
                        hour: '2-digit',
                        minute: '2-digit',
                        hour12: false,
                      }),
                    )}`
                  : `${new Date(receipt.startsAtUtc).toLocaleString()} – ${new Date(receipt.endsAtUtc).toLocaleTimeString()}`}
              </AppText>
              <AppText>{receipt.patientName}</AppText>
              <AppText>
                {copy.consultationFee}: ₹{receipt.consultationFee}
              </AppText>
              <AppText variant="muted">{copy.payAtClinic}</AppText>
            </Card>
            <ClinicLocationCard
              title={copy.clinicLocation}
              clinicName={
                [receipt.clinicName, receipt.branchName]
                  .map((part) => part?.trim())
                  .filter(Boolean)
                  .filter((part, index, all) => index === 0 || part?.toLowerCase() !== all[0]?.toLowerCase())
                  .join(' · ')
              }
              formattedAddress={receipt.address}
              latitude={receipt.latitude}
              longitude={receipt.longitude}
              googlePlaceId={receipt.googlePlaceId}
              mapsUrl={receipt.mapsUrl}
              directionsUrl={receipt.directionsUrl}
              locationVerified={receipt.locationVerified}
              unavailableLabel={copy.clinicLocationUnavailable}
              viewOnMapLabel={copy.viewOnGoogleMaps}
              getDirectionsLabel={copy.getDirections}
              unverifiedHint={copy.clinicLocationUnverified}
            />
            <ShareAppointmentButton
              label={copy.shareAppointment}
              onPress={() => setShareOpen(true)}
              style={{ alignSelf: 'stretch', width: '100%' }}
            />
            <Button label={copy.myVisits} onPress={() => router.push('/visits' as Href)} style={{ alignSelf: 'stretch', width: '100%' }} />
            {calendarUrl ? (
              <Button
                label={copy.addToCalendar}
                variant="secondary"
                onPress={() => void Linking.openURL(calendarUrl)}
                style={{ alignSelf: 'stretch', width: '100%' }}
              />
            ) : null}
            <Button
              label={copy.backHome}
              variant="ghost"
              onPress={() => router.replace(homeHrefForAccount(account))}
              style={{ alignSelf: 'stretch', width: '100%' }}
            />
          </Card>
        ) : null}
        <ShareAppointmentSheet visible={shareOpen} appointment={receipt} onClose={() => setShareOpen(false)} />
        {hold && !receipt ? (
          <Card elevated style={{ width: '100%', maxWidth: '100%', gap: spacing.md }}>
            <AppText variant="label">{copy.holdMinutes}</AppText>
            <AppText variant="headline">{hold.doctorName}</AppText>
            <AppText>
              {[hold.clinicName, hold.branchName]
                .map((part) => part?.trim())
                .filter(Boolean)
                .filter((part, index, all) => index === 0 || part?.toLowerCase() !== all[0]?.toLowerCase())
                .join(' · ')}
            </AppText>
            <AppText>
              {hold.bookingMode === 'HourlyQueue'
                ? `${new Date(hold.startsAtUtc).toLocaleDateString()} · ${formatHourlyRangeLabel(
                    new Date(hold.startsAtUtc).toLocaleTimeString('en-GB', {
                      hour: '2-digit',
                      minute: '2-digit',
                      hour12: false,
                    }),
                    new Date(hold.endsAtUtc).toLocaleTimeString('en-GB', {
                      hour: '2-digit',
                      minute: '2-digit',
                      hour12: false,
                    }),
                  )}`
                : new Date(hold.startsAtUtc).toLocaleString()}
            </AppText>
            <AppText variant="label" style={{ color: theme.colors.primary }}>
              {copy.consultationFee}: ₹{hold.consultationFee}
            </AppText>
            <AppText variant="muted">{hold.requiresApproval ? copy.approvalRequired : copy.instantBooking}</AppText>
            <AppText variant="muted">{copy.payAtClinic}</AppText>
            <AppText variant="muted">
              {copy.cancellationPolicy}: {hold.cancellationPolicy}
            </AppText>
            <AppText variant="label">{copy.visitFor}</AppText>
            <Button
              label={copy.myself}
              variant={visitTarget.kind === 'self' ? 'primary' : 'secondary'}
              onPress={() => setVisitTarget({ kind: 'self' })}
              style={{ alignSelf: 'stretch' }}
            />
            {(family.data ?? []).map((member) => (
              <Button
                key={member.id}
                label={`${member.fullName} · ${member.relationship}`}
                variant={visitTarget.kind === 'member' && visitTarget.id === member.id ? 'primary' : 'secondary'}
                onPress={() => setVisitTarget({ kind: 'member', id: member.id })}
                style={{ alignSelf: 'stretch' }}
              />
            ))}
            <AppText variant="muted">
              {visitTarget.kind === 'self' ? copy.patientNameSelfHint : copy.patientNameFamilyHint}
            </AppText>
            <AppText variant="label">{copy.relationship}</AppText>
            <Segmented
              value={
                visitTarget.kind === 'self'
                  ? // No relationship chip selected while booking for Myself.
                    ('' as 'child' | 'spouse' | 'parent' | 'other')
                  : visitTarget.kind === 'new'
                    ? visitTarget.relationship
                    : (['child', 'spouse', 'parent', 'other'].includes(selectedRelationship)
                        ? (selectedRelationship as 'child' | 'spouse' | 'parent' | 'other')
                        : 'other')
              }
              onChange={(value) => {
                setVisitTarget({
                  kind: 'new',
                  relationship: value as 'child' | 'spouse' | 'parent' | 'other',
                });
                setPatientName('');
                setPatientNameError(null);
              }}
              options={[
                { value: 'child', label: copy.child },
                { value: 'spouse', label: copy.spouse },
                { value: 'parent', label: copy.parent },
                { value: 'other', label: copy.other },
              ]}
            />
            <TextField
              label={copy.patientName}
              value={patientName}
              onChangeText={(value) => {
                setPatientName(value);
                if (patientNameError) {
                  setPatientNameError(null);
                }
              }}
              required
              editable={patientNameEditable}
              error={patientNameError ?? undefined}
              autoCapitalize="words"
            />
            {visitTarget.kind === 'new' ? (
              <Button
                label={copy.addFamily}
                variant="secondary"
                loading={confirm.isPending}
                disabled={confirm.isPending || !patientName.trim()}
                onPress={async () => {
                  const parsed = patientBookingNameSchema.safeParse(patientName);
                  if (!parsed.success) {
                    setPatientNameError(parsed.error.issues[0]?.message ?? copy.patientNameMandatory);
                    return;
                  }
                  try {
                    assertMutationAllowedOnline('booking.family', online);
                    const member = await addFamily({
                      fullName: parsed.data,
                      relationship: visitTarget.relationship,
                    });
                    await queryClient.invalidateQueries({ queryKey: ['family'] });
                    setVisitTarget({ kind: 'member', id: member.id });
                    setPatientName(member.fullName);
                    setPatientNameError(null);
                  } catch (error) {
                    notifyApiError(error, copy.saveFailed);
                    setPatientNameError(problemMessage(error, copy.patientNameMandatory));
                  }
                }}
                style={{ alignSelf: 'stretch' }}
              />
            ) : null}
            <TextField label={copy.reason} value={reason} onChangeText={setReason} />
            <TextField label={copy.symptoms} value={notes} onChangeText={setNotes} />
            <Button
              label={copy.confirmBooking}
              loading={confirm.isPending}
              disabled={confirm.isPending || !hold}
              onPress={() => {
                if (!patientName.trim()) {
                  setPatientNameError(copy.patientNameMandatory);
                  return;
                }
                confirm.mutate();
              }}
              style={{ alignSelf: 'stretch' }}
            />
            {confirm.error &&
            !(confirm.error instanceof Error && confirm.error.message === copy.patientNameMandatory) ? (
              <AppText variant="muted" style={{ color: theme.colors.error }}>
                {problemMessage(confirm.error, copy.bookingFailed)}
              </AppText>
            ) : null}
          </Card>
        ) : null}
        {!hold && !receipt && initialStart && !skipAutoHold && reserve.isPending ? (
          <Card elevated style={{ width: '100%', maxWidth: '100%', gap: spacing.sm }}>
            <AppText variant="label">{copy.confirmBooking}</AppText>
            <AppText variant="muted">
              {date} · {initialStart.slice(0, 5)}
            </AppText>
            <AppText variant="muted">{copy.loading}</AppText>
          </Card>
        ) : null}
        {!hold && !receipt && (!initialStart || skipAutoHold) ? (
          <Card elevated style={{ width: '100%', maxWidth: '100%', gap: spacing.md }}>
            <AppText variant="label">{copy.selectDate}</AppText>
            {next ? (
              <AppText variant="muted">
                {copy.nextAvailable}: {next.date}
              </AppText>
            ) : null}
            {calendar.isLoading ? <AppText variant="muted">{copy.loading}</AppText> : null}
            {calendar.error ? (
              <AppText variant="muted">{problemMessage(calendar.error, copy.selectDate)}</AppText>
            ) : null}
            <CalendarView
              mode="bookingWeeks"
              days={calendar.data ?? []}
              selectedDate={date}
              onSelect={setDate}
              statusLabels={statusLabel}
              weekCursor={weekCursor}
              onWeekChange={setWeekCursor}
            />
            {sameDayBooking ? (
              <AppText variant="muted">{copy.duplicateAppointmentSameDate}</AppText>
            ) : null}
            <AppText variant="label">
              {slots.data?.bookingMode === 'HourlyQueue' ? copy.selectHourlyRange : copy.selectTime}
            </AppText>
            {slots.data?.bookingMode === 'HourlyQueue' ? (
              <HourlyRangeSelector
                ranges={(slots.data.slots ?? []).map((slot) => ({
                  start: slot.start,
                  end: slot.end,
                  label: formatHourlyRangeLabel(slot.start, slot.end),
                  bookedCount: slot.bookedCount,
                  capacity: slot.capacity,
                  remaining: slot.remaining,
                  available: slot.available !== false && !sameDayBooking,
                  tokenPrefix: slot.tokenPrefix,
                }))}
                busy={reserve.isPending}
                emptyLabel={slots.isLoading ? copy.loading : copy.noSlotsOnDate}
                bookedLabel={copy.hourlyPatientsBooked}
                availableLabel={copy.hourlyAvailable}
                fullLabel={copy.hourlyFull}
                remainingLabel={copy.hourlyRemaining}
                onSelect={(start) => {
                  if (sameDayBooking) {
                    notifyWarning(copy.duplicateAppointmentSameDate);
                    return;
                  }
                  const slot = slots.data?.slots.find((item) => item.start === start);
                  if (slot?.available === false) return;
                  reserve.mutate(start);
                }}
              />
            ) : (
              <TimeSlotSelector
                slots={(slots.data?.slots ?? []).map((slot) => ({
                  start: slot.start,
                  end: slot.end,
                  disabled: slot.available === false || Boolean(sameDayBooking),
                }))}
                busy={reserve.isPending}
                emptyLabel={slots.isLoading ? copy.loading : copy.noSlotsOnDate}
                onSelect={(start) => {
                  if (sameDayBooking) {
                    notifyWarning(copy.duplicateAppointmentSameDate);
                    return;
                  }
                  const slot = slots.data?.slots.find((item) => item.start === start);
                  if (slot?.available === false) return;
                  reserve.mutate(start);
                }}
              />
            )}
          </Card>
        ) : null}
        {!hold && !receipt && initialStart && !skipAutoHold && reserve.isError ? (
          <Card elevated style={{ width: '100%', maxWidth: '100%', gap: spacing.sm }}>
            <AppText variant="muted">{problemMessage(reserve.error, copy.selectTime)}</AppText>
            <Button
              label={copy.selectTime}
              variant="secondary"
              onPress={() => {
                autoHoldKey.current = null;
                setSkipAutoHold(true);
                router.setParams({ start: undefined });
              }}
            />
          </Card>
        ) : null}
        {error ? (
          <View style={{ gap: spacing.sm }}>
            <AppText variant="muted">{problemMessage(error, copy.bookAppointment)}</AppText>
            {problemMessage(error, '').toLowerCase().includes('already have an appointment') ? (
              <>
                <AppText variant="muted">{copy.duplicateAppointmentHint}</AppText>
                <Button
                  label={copy.viewExistingAppointment}
                  variant="secondary"
                  onPress={() => router.push('/visits' as Href)}
                />
              </>
            ) : null}
          </View>
        ) : null}
      </FormScreenScroll>
    </Screen>
  );
}
