import { useQuery } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useMemo, useState } from 'react';
import { View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import Animated, { FadeInDown } from 'react-native-reanimated';

import { Screen } from '@/components/layout/auth-scaffold';
import { FormScreenScroll } from '@/components/layout/keyboard-aware-form';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { CalendarView } from '@/components/ui/calendar-view';
import { Card } from '@/components/ui/card';
import { ClinicLocationCard } from '@/components/ui/clinic-location-card';
import { ErrorState } from '@/components/ui/error-state';
import { ProfileAvatar } from '@/components/ui/profile-avatar';
import { Segmented } from '@/components/ui/segmented';
import { StarRatingDisplay } from '@/components/ui/star-rating-display';
import { TimeSlotSelector } from '@/components/ui/time-slot-selector';
import { AppText } from '@/components/ui/text';
import { VerificationBadge } from '@/components/ui/verification-badge';
import { clinicDateIso, patientBookingWindow, startOfClinicWeekMonday } from '@/features/providers/schedule-day';
import { DoctorReviewSection } from '@/features/reviews/review-list-section';
import { useCopy } from '@/i18n/copy';
import {
  addFavourite,
  getAvailability,
  getDiscoveredDoctor,
  getSlots,
  listAppointments,
} from '@/services/api/provider-api';
import { getDoctorRatingSummary } from '@/services/api/review-api';
import { useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { duration, layout, spacing } from '@/theme/tokens';
import {
  patientBookingActionKind,
  patientHasActiveBookingWithDoctor,
  patientHasActiveBookingWithDoctorOnDate,
} from '@/utils/patient-appointment-actions';
import { photoUriFor } from '@/utils/provider-photo';
import { problemMessage } from '@/utils/problem';
import { notifyWarning } from '@/stores/feedback-store';
import { formatYearsExperience } from '@/utils/years-of-experience';

export function PublicDoctor({
  doctorId,
  preferredClinicId,
  preferredScheduleId,
  preferredBranchId,
  followUpId,
}: {
  doctorId: string;
  preferredClinicId?: string | null;
  preferredScheduleId?: string | null;
  preferredBranchId?: string | null;
  followUpId?: string | null;
}) {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const accessToken = useSessionStore((state) => state.accessToken);
  const bookingWindow = useMemo(() => patientBookingWindow(), []);
  const [selectedDate, setSelectedDate] = useState(clinicDateIso());
  const [weekCursor, setWeekCursor] = useState(() => startOfClinicWeekMonday());
  const [scheduleId, setScheduleId] = useState<string | null>(preferredScheduleId ?? null);
  const [tab, setTab] = useState<'about' | 'reviews' | 'clinics' | 'fees'>('about');
  const doctor = useQuery({
    queryKey: ['discovery', 'doctor', doctorId],
    queryFn: () => getDiscoveredDoctor(doctorId),
    staleTime: 60_000,
  });
  const ratingSummary = useQuery({
    queryKey: ['rating', 'doctor', doctorId],
    queryFn: () => getDoctorRatingSummary(doctorId),
    staleTime: 60_000,
  });
  const myAppointments = useQuery({
    queryKey: ['appointments', 'mine'],
    queryFn: listAppointments,
    enabled: Boolean(accessToken),
    staleTime: 60_000,
  });
  const upcomingWithDoctor = useMemo(
    () => patientHasActiveBookingWithDoctor(myAppointments.data, doctorId),
    [doctorId, myAppointments.data],
  );
  const sameDayBooking = useMemo(
    () => patientHasActiveBookingWithDoctorOnDate(myAppointments.data, doctorId, selectedDate),
    [doctorId, myAppointments.data, selectedDate],
  );
  const bookingCtaLabel = useMemo(() => {
    // Only divert the CTA when this calendar date already has an active booking.
    if (!sameDayBooking) return copy.bookAppointment;
    const kind = patientBookingActionKind(sameDayBooking.status);
    if (kind === 'continueBooking') return copy.continueBooking;
    if (kind === 'viewBooking') return copy.viewBooking;
    return copy.viewAppointment;
  }, [copy.bookAppointment, copy.continueBooking, copy.viewAppointment, copy.viewBooking, sameDayBooking]);
  const preferredPractice = useMemo(() => {
    const practices = doctor.data?.practices ?? [];
    if (practices.length === 0) return null;
    if (preferredScheduleId) {
      const bySchedule = practices.find((place) => place.scheduleId === preferredScheduleId);
      if (bySchedule) return bySchedule;
    }
    if (preferredClinicId) {
      const byClinic = practices.find(
        (place) =>
          place.clinicId === preferredClinicId &&
          (!preferredBranchId || place.branchId === preferredBranchId),
      );
      if (byClinic) return byClinic;
      return practices.find((place) => place.clinicId === preferredClinicId) ?? null;
    }
    return practices[0] ?? null;
  }, [doctor.data?.practices, preferredBranchId, preferredClinicId, preferredScheduleId]);

  const activeSchedule =
    scheduleId ?? preferredPractice?.scheduleId ?? doctor.data?.practices[0]?.scheduleId ?? null;
  const activePractice =
    doctor.data?.practices.find((place) => place.scheduleId === activeSchedule) ?? preferredPractice;
  const calendar = useQuery({
    queryKey: [
      'availability',
      doctorId,
      'profile',
      activePractice?.branchId ?? null,
      bookingWindow.start,
      bookingWindow.end,
    ],
    queryFn: () =>
      getAvailability(doctorId, bookingWindow.start, bookingWindow.end, activePractice?.branchId),
    enabled: Boolean(doctorId),
  });
  const slots = useQuery({
    queryKey: ['slots', activeSchedule, selectedDate],
    queryFn: () => getSlots(activeSchedule!, selectedDate),
    enabled: Boolean(activeSchedule),
  });

  const primaryPractice = activePractice ?? doctor.data?.practices[0];
  const photo = useMemo(() => {
    const documentId =
      doctor.data?.photoDocumentId ??
      doctor.data?.practices.find((place) => place.photoDocumentId)?.photoDocumentId ??
      null;
    return photoUriFor(documentId, accessToken);
  }, [accessToken, doctor.data]);

  const statusLabel: Record<string, string> = {
    available: copy.available,
    unavailable: copy.unavailable,
    full: copy.full,
    holiday: copy.holiday,
    leave: copy.onLeave,
    partial: copy.partiallyAvailable,
  };
  const enter = theme.reduceMotion ? undefined : FadeInDown.duration(duration.enter);

  const bookHref = (practiceScheduleId: string, branchId: string | null, slotStart?: string) => {
    const params = new URLSearchParams({
      doctorId,
      branchId: branchId ?? '',
      date: selectedDate,
    });
    if (slotStart) params.set('start', slotStart);
    if (followUpId) params.set('followUpId', followUpId);
    return `/book/${practiceScheduleId}?${params.toString()}` as Href;
  };

  return (
    <Screen>
      <ScreenHeader title={doctor.data?.displayName ?? copy.findDoctor} showBack />
      <FormScreenScroll
        toolbar={false}
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
        {doctor.error ? (
          <ErrorState
            title={copy.findDoctor}
            body={problemMessage(doctor.error, copy.findDoctor)}
            actionLabel={copy.continue}
            onAction={() => void doctor.refetch()}
          />
        ) : null}
        {doctor.data ? (
          <Animated.View entering={enter} style={{ width: '100%', maxWidth: '100%', gap: spacing.lg }}>
            <Card elevated style={{ width: '100%', maxWidth: '100%', gap: spacing.md }}>
              <View style={{ flexDirection: 'row', gap: spacing.md, alignItems: 'flex-start', width: '100%' }}>
                <ProfileAvatar
                  name={doctor.data.displayName}
                  uri={photo?.uri}
                  headers={photo?.headers}
                  size={88}
                />
                <View style={{ flex: 1, minWidth: 0, gap: spacing.xs }}>
                  <AppText variant="title" style={{ fontSize: 22, lineHeight: 28 }} numberOfLines={2}>
                    {doctor.data.displayName}
                  </AppText>
                  <AppText variant="muted" numberOfLines={2}>
                    {doctor.data.primarySpecialization}
                    {` · ${formatYearsExperience(doctor.data.yearsOfExperience, copy.yearsExp)}`}
                  </AppText>
                  <VerificationBadge label={copy.verified} />
                  {ratingSummary.data ? (
                    <StarRatingDisplay
                      average={
                        ratingSummary.data.averageIsPublic ? ratingSummary.data.averageOverall : null
                      }
                      reviewCount={ratingSummary.data.publishedReviewCount}
                      size={15}
                    />
                  ) : null}
                </View>
              </View>
              {primaryPractice ? (
                <View style={{ gap: spacing.xs, width: '100%' }}>
                  <AppText variant="label" style={{ color: theme.colors.primary }}>
                    {copy.consultationFee}: ₹{primaryPractice.consultationFee}
                  </AppText>
                  {[primaryPractice.clinicName, primaryPractice.branchName, primaryPractice.city]
                    .filter(Boolean)
                    .length > 0 ? (
                    <AppText variant="muted" numberOfLines={2}>
                      {[primaryPractice.clinicName, primaryPractice.branchName, primaryPractice.city]
                        .filter(Boolean)
                        .join(' · ')}
                    </AppText>
                  ) : null}
                </View>
              ) : null}
              <View style={{ flexDirection: 'row', gap: spacing.sm, width: '100%' }}>
                {primaryPractice || sameDayBooking ? (
                  <Button
                    label={bookingCtaLabel}
                    onPress={() => {
                      if (sameDayBooking) {
                        notifyWarning(copy.duplicateAppointmentSameDate);
                        router.push(
                          `/visits?appointmentId=${encodeURIComponent(sameDayBooking.id)}` as Href,
                        );
                        return;
                      }
                      if (!primaryPractice) return;
                      router.push(bookHref(primaryPractice.scheduleId, primaryPractice.branchId));
                    }}
                    style={{ flex: 1 }}
                  />
                ) : null}
                <Button
                  label={copy.favourite}
                  variant="secondary"
                  onPress={() => void addFavourite(doctor.data.id)}
                  style={{ flex: primaryPractice || sameDayBooking ? 1 : undefined, alignSelf: 'stretch', width: primaryPractice || sameDayBooking ? undefined : '100%' }}
                />
              </View>
              {sameDayBooking ? (
                <AppText variant="muted">{copy.duplicateAppointmentSameDate}</AppText>
              ) : upcomingWithDoctor ? (
                <AppText variant="muted">
                  {copy.upcomingWithDoctorHint}{' '}
                  {new Date(upcomingWithDoctor.startsAtUtc).toLocaleString(undefined, {
                    weekday: 'short',
                    day: 'numeric',
                    month: 'short',
                    hour: '2-digit',
                    minute: '2-digit',
                  })}
                  {upcomingWithDoctor.clinicName ? ` · ${upcomingWithDoctor.clinicName}` : ''}
                </AppText>
              ) : null}
            </Card>

            <Segmented
              value={tab}
              options={[
                { value: 'about', label: copy.aboutTab },
                { value: 'reviews', label: copy.reviewsTab },
                { value: 'clinics', label: copy.clinicsTab },
                { value: 'fees', label: copy.feesTab },
              ]}
              onChange={setTab}
            />

            {tab === 'about' ? (
              <Card elevated style={{ width: '100%', maxWidth: '100%', gap: spacing.md }}>
                {doctor.data.biography ? <AppText>{doctor.data.biography}</AppText> : null}
                {doctor.data.qualifications.length > 0 ? (
                  <AppText variant="muted">{doctor.data.qualifications.join(', ')}</AppText>
                ) : null}
                {doctor.data.expertise.length > 0 ? (
                  <AppText variant="muted">{doctor.data.expertise.join(', ')}</AppText>
                ) : null}
                {doctor.data.languages.length > 0 ? (
                  <AppText variant="muted">{doctor.data.languages.join(', ')}</AppText>
                ) : null}
                {doctor.data.registrationCouncil ? (
                  <AppText variant="muted">
                    {copy.registrationPublic}: {doctor.data.registrationCouncil}, {doctor.data.registrationState}
                  </AppText>
                ) : null}

                <AppText variant="label">{copy.selectDate}</AppText>
                {calendar.isLoading ? <AppText variant="muted">{copy.loading}</AppText> : null}
                {calendar.error ? (
                  <AppText variant="muted">{problemMessage(calendar.error, copy.selectDate)}</AppText>
                ) : null}
                <CalendarView
                  mode="bookingWeeks"
                  days={calendar.data ?? []}
                  selectedDate={selectedDate}
                  onSelect={setSelectedDate}
                  statusLabels={statusLabel}
                  weekCursor={weekCursor}
                  onWeekChange={setWeekCursor}
                />
                <AppText variant="label">{copy.selectTime}</AppText>
                <TimeSlotSelector
                  slots={(slots.data?.slots ?? []).map((slot) => ({
                    start: slot.start,
                    end: slot.end,
                    disabled: slot.available === false,
                  }))}
                  emptyLabel={slots.isLoading ? copy.loading : copy.noSlotsOnDate}
                  onSelect={(start) => {
                    if (sameDayBooking) {
                      notifyWarning(copy.duplicateAppointmentSameDate);
                      router.push(
                        `/visits?appointmentId=${encodeURIComponent(sameDayBooking.id)}` as Href,
                      );
                      return;
                    }
                    if (!activeSchedule) return;
                    const practice =
                      doctor.data.practices.find((item) => item.scheduleId === activeSchedule) ?? primaryPractice;
                    if (!practice) return;
                    router.push(bookHref(practice.scheduleId, practice.branchId, start));
                  }}
                />
                {slots.isLoading ? <AppText variant="muted">{copy.loading}</AppText> : null}
                {slots.error ? <AppText variant="muted">{problemMessage(slots.error, copy.openTimes)}</AppText> : null}
              </Card>
            ) : null}

            {tab === 'reviews' ? <DoctorReviewSection doctorId={doctor.data.id} /> : null}

            {tab === 'clinics'
              ? doctor.data.practices.map((place) => (
                  <View key={place.scheduleId} style={{ width: '100%', maxWidth: '100%', gap: spacing.sm }}>
                    <ClinicLocationCard
                      title={copy.clinicLocation}
                      clinicName={
                        [place.clinicName, place.branchName]
                          .map((part) => part?.trim())
                          .filter(Boolean)
                          .filter((part, index, all) => index === 0 || part?.toLowerCase() !== all[0]?.toLowerCase())
                          .join(' · ') || copy.independent
                      }
                      addressLine={place.addressLine}
                      addressLine2={place.addressLine2}
                      landmark={place.landmark}
                      locality={place.locality}
                      city={place.city}
                      district={place.district}
                      state={place.state}
                      postalCode={place.postalCode}
                      country={place.country}
                      formattedAddress={place.formattedAddress}
                      latitude={place.latitude}
                      longitude={place.longitude}
                      googlePlaceId={place.googlePlaceId}
                      mapsUrl={place.mapsUrl}
                      directionsUrl={place.directionsUrl}
                      locationVerified={place.locationVerified}
                      unavailableLabel={copy.clinicLocationUnavailable}
                      viewOnMapLabel={copy.viewOnGoogleMaps}
                      getDirectionsLabel={copy.getDirections}
                      unverifiedHint={copy.clinicLocationUnverified}
                    />
                    <Card elevated style={{ width: '100%', gap: spacing.sm }}>
                      <Button
                        label={copy.openTimes}
                        variant={activeSchedule === place.scheduleId ? 'primary' : 'secondary'}
                        onPress={() => setScheduleId(place.scheduleId)}
                        style={{ alignSelf: 'stretch' }}
                      />
                      <Button
                        label={bookingCtaLabel}
                        onPress={() => {
                          if (sameDayBooking) {
                            notifyWarning(copy.duplicateAppointmentSameDate);
                            router.push(
                              `/visits?appointmentId=${encodeURIComponent(sameDayBooking.id)}` as Href,
                            );
                            return;
                          }
                          router.push(bookHref(place.scheduleId, place.branchId));
                        }}
                        style={{ alignSelf: 'stretch' }}
                      />
                    </Card>
                  </View>
                ))
              : null}

            {tab === 'fees'
              ? doctor.data.practices.map((place) => (
                  <Card key={`fee-${place.scheduleId}`} elevated style={{ width: '100%', maxWidth: '100%', gap: spacing.sm }}>
                    <AppText variant="headline">
                      {[place.clinicName, place.branchName].filter(Boolean).join(' · ') || copy.independent}
                    </AppText>
                    <AppText>
                      {copy.consultationFee}: ₹{place.consultationFee}
                    </AppText>
                    <AppText variant="muted">{place.slotDurationMinutes} min</AppText>
                  </Card>
                ))
              : null}
          </Animated.View>
        ) : null}
      </FormScreenScroll>
    </Screen>
  );
}
