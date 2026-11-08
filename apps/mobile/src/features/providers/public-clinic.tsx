import { useQueries, useQuery } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useMemo } from 'react';
import { View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import Animated, { FadeInDown } from 'react-native-reanimated';

import { Screen } from '@/components/layout/auth-scaffold';
import { FormScreenScroll } from '@/components/layout/keyboard-aware-form';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ClinicLocationCard } from '@/components/ui/clinic-location-card';
import { DoctorCard } from '@/components/ui/doctor-card';
import { EmptyState } from '@/components/ui/empty-state';
import { ErrorState } from '@/components/ui/error-state';
import { ProfileAvatar } from '@/components/ui/profile-avatar';
import { CardSkeleton } from '@/components/ui/skeleton';
import { StarRatingDisplay } from '@/components/ui/star-rating-display';
import { StatusBadge } from '@/components/ui/status-badge';
import { AppText } from '@/components/ui/text';
import { VerificationBadge } from '@/components/ui/verification-badge';
import { ClinicReviewSection } from '@/features/reviews/review-list-section';
import { clinicDateIso, patientBookingWindow } from '@/features/providers/schedule-day';
import { useCopy } from '@/i18n/copy';
import {
  discoverPlaces,
  getAvailability,
  getDiscoveredClinic,
  type DiscoveryPlace,
} from '@/services/api/provider-api';
import { getClinicRatingSummary } from '@/services/api/review-api';
import { useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { duration, layout, spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';
import { photoUriFor } from '@/utils/provider-photo';
import { formatYearsExperience } from '@/utils/years-of-experience';

function practiceHref(place: DiscoveryPlace) {
  const params = new URLSearchParams({
    clinicId: place.clinicId ?? '',
    scheduleId: place.scheduleId,
    branchId: place.branchId ?? '',
  });
  return `/practices/${place.doctorId}?${params.toString()}` as Href;
}

function registrationLabel(place: DiscoveryPlace, prefix: string) {
  const parts = [place.registrationCouncil, place.registrationNumber].filter(Boolean);
  if (parts.length === 0) return null;
  return `${prefix}: ${parts.join(' · ')}`;
}

function nextAvailableLabel(
  days: { date: string; status: string; firstSlot: string | null }[] | undefined,
  labels: { nextAvailable: string; available: string; unavailable: string },
) {
  if (!days) return null;
  const next = days.find((day) => day.status === 'available' && day.firstSlot);
  if (!next) return labels.unavailable;
  const when =
    next.date === clinicDateIso()
      ? labels.available
      : new Date(`${next.date}T00:00:00`).toLocaleDateString(undefined, {
          weekday: 'short',
          day: 'numeric',
          month: 'short',
        });
  return `${labels.nextAvailable}: ${when}${next.firstSlot ? ` · ${next.firstSlot}` : ''}`;
}

export function PublicClinic({ clinicId }: { clinicId: string }) {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const accessToken = useSessionStore((state) => state.accessToken);
  const bookingWindow = useMemo(() => patientBookingWindow(), []);
  const enter = theme.reduceMotion ? undefined : FadeInDown.duration(duration.enter);

  const ratingSummary = useQuery({
    queryKey: ['rating', 'clinic', clinicId],
    queryFn: () => getClinicRatingSummary(clinicId),
  });
  const clinic = useQuery({
    queryKey: ['discovery', 'clinic', clinicId],
    queryFn: () => getDiscoveredClinic(clinicId),
    enabled: Boolean(clinicId),
  });

  const doctors = useQuery({
    queryKey: ['discovery', 'clinic-doctors', clinicId],
    queryFn: ({ signal }) =>
      discoverPlaces({ clinicId, pageSize: 50, sort: 'recommended' }, signal),
    enabled: Boolean(clinicId),
  });

  const uniquePlaces = useMemo(() => {
    const seen = new Set<string>();
    const rows: DiscoveryPlace[] = [];
    for (const place of doctors.data ?? []) {
      // One card per doctor at this clinic; prefer the first matching practice (clinic-scoped fee/location).
      if (seen.has(place.doctorId)) continue;
      if (place.clinicId && place.clinicId !== clinicId) continue;
      seen.add(place.doctorId);
      rows.push(place);
    }
    return rows;
  }, [clinicId, doctors.data]);

  const availabilityQueries = useQueries({
    queries: uniquePlaces.map((place) => ({
      queryKey: ['availability', place.doctorId, place.branchId, bookingWindow.start, bookingWindow.end],
      queryFn: () =>
        getAvailability(place.doctorId, bookingWindow.start, bookingWindow.end, place.branchId),
      staleTime: 60_000,
      enabled: uniquePlaces.length > 0 && uniquePlaces.length <= 20,
    })),
  });

  const logo = photoUriFor(clinic.data?.logoDocumentId, accessToken);
  const totalDoctors = uniquePlaces.length;
  const doctorCountLabel =
    totalDoctors === 1 ? copy.doctorCountOne : copy.doctorsCount.replace('{count}', String(totalDoctors));

  return (
    <Screen>
      <ScreenHeader title={clinic.data?.name ?? copy.findClinicsTitle} showBack />
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
        {clinic.isLoading ? (
          <Card>
            <CardSkeleton />
          </Card>
        ) : null}

        {clinic.error ? (
          <ErrorState
            title={copy.findClinicsTitle}
            body={problemMessage(clinic.error, copy.findClinicsTitle)}
            actionLabel={copy.continue}
            onAction={() => void clinic.refetch()}
          />
        ) : null}

        {clinic.data ? (
          <Animated.View entering={enter} style={{ gap: spacing.lg }}>
            <Card elevated style={{ gap: spacing.md }}>
              <View style={{ flexDirection: 'row', gap: spacing.md, alignItems: 'center' }}>
                <ProfileAvatar
                  name={clinic.data.name}
                  uri={logo?.uri}
                  headers={logo?.headers}
                  size={72}
                />
                <View style={{ flex: 1, gap: spacing.xs, minWidth: 0 }}>
                  <AppText variant="title" style={{ fontSize: 22, lineHeight: 28 }} numberOfLines={2}>
                    {clinic.data.name}
                  </AppText>
                  <VerificationBadge label={copy.verified} />
                  {clinic.data.type ? <StatusBadge label={clinic.data.type} tone="primary" /> : null}
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
              {clinic.data.description ? <AppText>{clinic.data.description}</AppText> : null}
              <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                {doctorCountLabel}
              </AppText>
              {clinic.data.specializations.length > 0 ? (
                <AppText variant="muted">{clinic.data.specializations.join(', ')}</AppText>
              ) : null}
            </Card>

            {clinic.data.branches.map((branch) => (
              <View key={branch.id} style={{ gap: spacing.sm }}>
                <ClinicLocationCard
                  title={copy.clinicLocation}
                  clinicName={branch.name}
                  addressLine={branch.addressLine}
                  addressLine2={branch.addressLine2}
                  landmark={branch.landmark}
                  locality={branch.locality}
                  city={branch.city}
                  district={branch.district}
                  state={branch.state}
                  postalCode={branch.postalCode}
                  country={branch.country}
                  formattedAddress={branch.formattedAddress}
                  latitude={branch.latitude}
                  longitude={branch.longitude}
                  googlePlaceId={branch.googlePlaceId}
                  mapsUrl={branch.mapsUrl}
                  directionsUrl={branch.directionsUrl}
                  locationVerified={branch.locationVerified}
                  unavailableLabel={copy.clinicLocationUnavailable}
                  viewOnMapLabel={copy.viewOnGoogleMaps}
                  getDirectionsLabel={copy.getDirections}
                  unverifiedHint={copy.clinicLocationUnverified}
                />
                {branch.phoneNumber || branch.openToday ? (
                  <Card elevated style={{ gap: spacing.sm }}>
                    {branch.phoneNumber ? (
                      <>
                        <AppText variant="label">{copy.clinicContact}</AppText>
                        <AppText variant="muted">{branch.phoneNumber}</AppText>
                      </>
                    ) : null}
                    {branch.openToday ? <StatusBadge label={copy.openToday} tone="success" /> : null}
                  </Card>
                ) : null}
              </View>
            ))}

            {clinic.data.facilities.length > 0 ? (
              <Card elevated style={{ gap: spacing.sm }}>
                <AppText variant="label">{copy.facilities}</AppText>
                <AppText>{clinic.data.facilities.join(', ')}</AppText>
              </Card>
            ) : null}

            <View style={{ gap: spacing.md }}>
              <AppText variant="headline">{copy.clinicDoctors}</AppText>
              {doctors.isLoading ? (
                <Card>
                  <CardSkeleton />
                </Card>
              ) : null}
              {doctors.error ? (
                <ErrorState
                  title={copy.clinicDoctors}
                  body={problemMessage(doctors.error, copy.clinicDoctors)}
                  actionLabel={copy.continue}
                  onAction={() => void doctors.refetch()}
                />
              ) : null}
              {!doctors.isLoading && !doctors.error && uniquePlaces.length === 0 ? (
                <EmptyState title={copy.noClinicDoctors} body={copy.loginSubtitle} />
              ) : null}
              {uniquePlaces.map((place, index) => {
                const photo = photoUriFor(place.photoDocumentId, accessToken);
                const availability = nextAvailableLabel(availabilityQueries[index]?.data, {
                  nextAvailable: copy.nextAvailable,
                  available: copy.availableToday,
                  unavailable: copy.unavailable,
                });
                const location = [place.clinicName, place.branchName, place.city].filter(Boolean).join(' · ');
                return (
                  <Animated.View
                    key={place.scheduleId}
                    entering={
                      theme.reduceMotion ? undefined : FadeInDown.delay(Math.min(index, 6) * 40).duration(220)
                    }>
                    <DoctorCard
                      name={place.displayName}
                      specialization={place.primarySpecialization}
                      qualification={place.qualification}
                      registration={registrationLabel(place, copy.registrationPublic)}
                      experience={place.yearsOfExperience}
                      experienceLabel={copy.yearsExp}
                      fee={place.consultationFee}
                      feeLabel={copy.consultationFee}
                      location={location}
                      availability={availability}
                      photoUri={photo?.uri}
                      photoHeaders={photo?.headers}
                      verifiedLabel={copy.verified}
                      viewLabel={copy.viewProfile}
                      bookLabel={copy.bookAppointment}
                      onView={() => router.push(practiceHref(place))}
                      onBook={() => router.push(practiceHref(place))}
                    />
                  </Animated.View>
                );
              })}
            </View>

            <ClinicReviewSection clinicId={clinic.data.id} />
          </Animated.View>
        ) : null}

        {!clinic.data && !clinic.isLoading && !clinic.error ? (
          <EmptyState title={copy.noClinics} body={copy.loginSubtitle} />
        ) : null}
      </FormScreenScroll>
    </Screen>
  );
}
