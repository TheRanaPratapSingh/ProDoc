import { useQuery } from '@tanstack/react-query';
import { useLocalSearchParams } from 'expo-router';
import { Linking, View } from 'react-native';

import { Screen } from '@/components/layout/auth-scaffold';
import { FormScreenScroll } from '@/components/layout/keyboard-aware-form';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ClinicLocationCard } from '@/components/ui/clinic-location-card';
import { ErrorState } from '@/components/ui/error-state';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { getPublicSharedAppointment } from '@/services/api/appointment-share-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { layout, spacing } from '@/theme/tokens';
import { buildAppointmentShareMessage } from '@/utils/appointment-share';
import { clinicPlaceLabel, formatVisitDate, formatVisitTime } from '@/utils/visit-format';
import { problemMessage } from '@/utils/problem';
import { formatHourlyRangeLabel } from '@/components/ui/hourly-range-selector';

export default function SharedAppointmentScreen() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const params = useLocalSearchParams<{ token?: string }>();
  const token = typeof params.token === 'string' ? params.token : '';

  const shared = useQuery({
    queryKey: ['public-appointment-share', token],
    queryFn: () => getPublicSharedAppointment(token),
    enabled: token.length >= 24,
    retry: 1,
  });

  const timeLabel = (() => {
    const data = shared.data;
    if (!data) return '—';
    if (data.bookingMode === 'HourlyQueue') {
      const start = new Date(data.startsAtUtc).toLocaleTimeString('en-GB', {
        hour: '2-digit',
        minute: '2-digit',
        hour12: false,
        timeZone: data.timeZoneId || undefined,
      });
      const end = new Date(data.endsAtUtc).toLocaleTimeString('en-GB', {
        hour: '2-digit',
        minute: '2-digit',
        hour12: false,
        timeZone: data.timeZoneId || undefined,
      });
      return formatHourlyRangeLabel(start, end);
    }
    return `${formatVisitTime(data.startsAtUtc, data.timeZoneId)} – ${formatVisitTime(data.endsAtUtc, data.timeZoneId)}`;
  })();

  return (
    <Screen>
      <ScreenHeader title={copy.sharedAppointmentTitle} showBack />
      <FormScreenScroll
        contentContainerStyle={{
          paddingHorizontal: layout.contentPad,
          paddingBottom: spacing.xxl,
          gap: spacing.lg,
        }}>
        <AppText variant="muted">{copy.sharedAppointmentBody}</AppText>
        {shared.isError ? (
          <ErrorState
            title={copy.sharedAppointmentUnavailable}
            body={problemMessage(shared.error, copy.sharedAppointmentUnavailable)}
            actionLabel={copy.continue}
            onAction={() => void shared.refetch()}
          />
        ) : null}
        {shared.data ? (
          <>
            <Card elevated style={{ gap: spacing.sm }}>
              <AppText variant="label">{copy.status}</AppText>
              <AppText variant="headline">{shared.data.status}</AppText>
              <AppText>{shared.data.patientName}</AppText>
              <AppText variant="muted">{shared.data.doctorName}</AppText>
              {shared.data.specialty ? <AppText variant="caption">{shared.data.specialty}</AppText> : null}
              <AppText>{clinicPlaceLabel(shared.data.clinicName, shared.data.branchName)}</AppText>
              <AppText variant="muted">
                {formatVisitDate(shared.data.startsAtUtc, shared.data.timeZoneId)} · {timeLabel}
              </AppText>
              {shared.data.tokenLabel ? (
                <AppText variant="headline">
                  {copy.tokenNumber}: {shared.data.tokenLabel}
                </AppText>
              ) : null}
              <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                {copy.bookingReference}: {shared.data.bookingReference}
              </AppText>
            </Card>
            <ClinicLocationCard
              title={copy.clinicLocation}
              clinicName={clinicPlaceLabel(shared.data.clinicName, shared.data.branchName)}
              formattedAddress={shared.data.address}
              mapsUrl={shared.data.mapsUrl}
              unavailableLabel={copy.clinicLocationUnavailable}
              viewOnMapLabel={copy.viewOnGoogleMaps}
              getDirectionsLabel={copy.getDirections}
            />
            {shared.data.mapsUrl ? (
              <Button
                label={copy.viewOnGoogleMaps}
                variant="secondary"
                onPress={() => void Linking.openURL(shared.data!.mapsUrl!)}
              />
            ) : null}
            <Card style={{ gap: spacing.xs }}>
              <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                {buildAppointmentShareMessage(shared.data)}
              </AppText>
            </Card>
          </>
        ) : null}
        {shared.isLoading ? <AppText variant="muted">{copy.loading}</AppText> : null}
      </FormScreenScroll>
    </Screen>
  );
}
