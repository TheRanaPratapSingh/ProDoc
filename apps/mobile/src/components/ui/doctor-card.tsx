import { View } from 'react-native';

import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { MapsIconButton } from '@/components/ui/clinic-location-card';
import { PressableScale } from '@/components/ui/pressable-scale';
import { ProfileAvatar } from '@/components/ui/profile-avatar';
import { VerificationBadge } from '@/components/ui/verification-badge';
import { AppText } from '@/components/ui/text';
import { cardElevation } from '@/theme/elevation';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { motion, radius, spacing } from '@/theme/tokens';
import type { MapLocation } from '@/utils/maps';
import { formatYearsExperience } from '@/utils/years-of-experience';

export function DoctorCard({
  name,
  specialization,
  qualification,
  registration,
  experience,
  fee,
  location,
  distance,
  availability,
  photoUri,
  photoHeaders,
  verifiedLabel,
  experienceLabel,
  feeLabel,
  viewLabel,
  bookLabel,
  directionsLabel,
  onView,
  onBook,
  onDirections,
}: {
  name: string;
  specialization?: string | null;
  qualification?: string | null;
  registration?: string | null;
  experience?: string | number | null;
  fee?: string | number | null;
  location?: string | null;
  distance?: string | null;
  availability?: string | null;
  photoUri?: string | null;
  photoHeaders?: Record<string, string>;
  verifiedLabel?: string;
  experienceLabel?: string;
  feeLabel: string;
  viewLabel: string;
  bookLabel?: string;
  directionsLabel?: string;
  onView: () => void;
  onBook?: () => void;
  onDirections?: () => void;
}) {
  const theme = useProdocTheme();

  return (
    <Card elevated style={{ gap: spacing.md, padding: spacing.lg }}>
      <PressableScale onPress={onView} scaleTo={motion.cardPressScale}>
        <View style={{ flexDirection: 'row', gap: spacing.md, alignItems: 'center' }}>
          <ProfileAvatar name={name} uri={photoUri} headers={photoHeaders} size={64} />
          <View style={{ flex: 1, gap: spacing.xs, minWidth: 0 }}>
            <AppText variant="headline" numberOfLines={1}>
              {name}
            </AppText>
            <AppText variant="muted" numberOfLines={1}>
              {[specialization, qualification].filter(Boolean).join(' · ')}
            </AppText>
            {experience != null && experience !== '' ? (
              <AppText variant="caption">
                {typeof experience === 'number'
                  ? formatYearsExperience(experience, experienceLabel ?? 'Years Experience')
                  : experience}
              </AppText>
            ) : null}
            {registration ? (
              <AppText variant="caption" numberOfLines={1}>
                {registration}
              </AppText>
            ) : null}
            {verifiedLabel ? <VerificationBadge label={verifiedLabel} /> : null}
          </View>
        </View>
      </PressableScale>
      {location ? <AppText variant="muted">{location}</AppText> : null}
      {availability ? <AppText variant="caption">{availability}</AppText> : null}
      <View
        style={{
          flexDirection: 'row',
          flexWrap: 'wrap',
          gap: spacing.sm,
          justifyContent: 'space-between',
          paddingTop: spacing.sm,
          borderTopWidth: 1,
          borderTopColor: theme.colors.border,
        }}>
        <AppText variant="label" style={{ color: theme.colors.primary }}>
          {feeLabel}: ₹{fee ?? '—'}
        </AppText>
        {distance ? <AppText variant="muted">{distance}</AppText> : null}
      </View>
      <View style={{ flexDirection: 'row', gap: spacing.sm }}>
        {onBook && bookLabel ? (
          <Button label={bookLabel} onPress={onBook} style={{ flex: 1 }} />
        ) : (
          <Button label={viewLabel} onPress={onView} style={{ flex: 1 }} />
        )}
        {onBook && bookLabel ? (
          <Button label={viewLabel} variant="secondary" onPress={onView} style={{ flex: 1 }} />
        ) : null}
        {onDirections && directionsLabel ? (
          <Button label={directionsLabel} variant="ghost" onPress={onDirections} />
        ) : null}
      </View>
    </Card>
  );
}

export function ClinicCard({
  name,
  type,
  verifiedLabel,
  specializations,
  address,
  city,
  landmark,
  mapLocation,
  doctorCount,
  doctorCountLabel,
  openTodayLabel,
  logoUri,
  logoHeaders,
  viewLabel,
  onView,
}: {
  name: string;
  type?: string | null;
  verifiedLabel?: string;
  specializations?: string;
  address?: string | null;
  city?: string | null;
  landmark?: string | null;
  mapLocation?: MapLocation | null;
  doctorCount?: number | null;
  doctorCountLabel?: string;
  openTodayLabel?: string;
  logoUri?: string | null;
  logoHeaders?: Record<string, string>;
  viewLabel: string;
  onView: () => void;
}) {
  const theme = useProdocTheme();
  const meta = [type, city].filter(Boolean).join(' · ');

  return (
    <PressableScale
      onPress={onView}
      scaleTo={motion.cardPressScale}
      contentStyle={[
        {
          backgroundColor: theme.colors.surface,
          borderRadius: radius.lg,
          borderWidth: 1,
          borderColor: theme.colors.border,
          padding: spacing.lg,
          gap: spacing.md,
        },
        cardElevation(theme.scheme, 'elevated'),
      ]}>
      <View style={{ flexDirection: 'row', gap: spacing.md, alignItems: 'center' }}>
        <ProfileAvatar name={name} uri={logoUri} headers={logoHeaders} size={56} />
        <View style={{ flex: 1, gap: spacing.xs, minWidth: 0 }}>
          <AppText variant="headline" numberOfLines={1}>
            {name}
          </AppText>
          {meta ? (
            <AppText variant="muted" numberOfLines={1}>
              {meta}
            </AppText>
          ) : null}
          {verifiedLabel ? <VerificationBadge label={verifiedLabel} /> : null}
        </View>
        {mapLocation ? <MapsIconButton location={mapLocation} /> : null}
      </View>
      {address ? (
        <AppText variant="muted" numberOfLines={2}>
          {address}
        </AppText>
      ) : null}
      {landmark ? (
        <AppText variant="caption" style={{ color: theme.colors.textMuted }} numberOfLines={1}>
          Near {landmark}
        </AppText>
      ) : null}
      {specializations ? (
        <AppText variant="muted" numberOfLines={2}>
          {specializations}
        </AppText>
      ) : null}
      <View
        style={{
          flexDirection: 'row',
          flexWrap: 'wrap',
          gap: spacing.sm,
          alignItems: 'center',
          justifyContent: 'space-between',
        }}>
        {doctorCount != null ? (
          <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
            {doctorCountLabel ?? `${doctorCount} doctors`}
          </AppText>
        ) : (
          <View />
        )}
        {openTodayLabel ? <VerificationBadge label={openTodayLabel} /> : null}
      </View>
      <Button label={viewLabel} onPress={onView} />
    </PressableScale>
  );
}
