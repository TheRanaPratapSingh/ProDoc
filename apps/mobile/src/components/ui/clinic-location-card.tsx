import { Pressable, View } from 'react-native';

import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';
import {
  formatClinicAddress,
  formatCompactAddress,
  hasMapDestination,
  openDirections,
  openMapsLocation,
  type MapLocation,
} from '@/utils/maps';

function MapPinIcon({ color, size = 22 }: { color: string; size?: number }) {
  // Lightweight Google-Maps-style pin without requiring a new SVG export dependency.
  return (
    <View
      style={{
        width: size,
        height: size,
        alignItems: 'center',
        justifyContent: 'center',
      }}>
      <View
        style={{
          width: size * 0.72,
          height: size * 0.72,
          borderRadius: size,
          backgroundColor: color,
          alignItems: 'center',
          justifyContent: 'center',
        }}>
        <View
          style={{
            width: size * 0.28,
            height: size * 0.28,
            borderRadius: size,
            backgroundColor: '#FFFFFF',
          }}
        />
      </View>
      <View
        style={{
          width: 0,
          height: 0,
          marginTop: -2,
          borderLeftWidth: size * 0.18,
          borderRightWidth: size * 0.18,
          borderTopWidth: size * 0.28,
          borderLeftColor: 'transparent',
          borderRightColor: 'transparent',
          borderTopColor: color,
        }}
      />
    </View>
  );
}

export function ClinicLocationCard({
  title = 'Clinic Location',
  clinicName,
  landmark,
  addressLine,
  addressLine2,
  locality,
  city,
  district,
  state,
  postalCode,
  country,
  formattedAddress,
  compact,
  latitude,
  longitude,
  googlePlaceId,
  mapsUrl,
  directionsUrl,
  locationVerified,
  unavailableLabel = 'Clinic location not available',
  viewOnMapLabel = 'View on Google Maps',
  getDirectionsLabel = 'Get Directions',
  unverifiedHint,
}: {
  title?: string;
  clinicName?: string | null;
  landmark?: string | null;
  addressLine?: string | null;
  addressLine2?: string | null;
  locality?: string | null;
  city?: string | null;
  district?: string | null;
  state?: string | null;
  postalCode?: string | null;
  country?: string | null;
  formattedAddress?: string | null;
  compact?: boolean;
  latitude?: number | null;
  longitude?: number | null;
  googlePlaceId?: string | null;
  mapsUrl?: string | null;
  directionsUrl?: string | null;
  locationVerified?: boolean;
  unavailableLabel?: string;
  viewOnMapLabel?: string;
  getDirectionsLabel?: string;
  unverifiedHint?: string;
}) {
  const theme = useProdocTheme();
  const address = compact
    ? formatCompactAddress(locality, city, postalCode) ||
      formatClinicAddress({
        addressLine,
        addressLine2,
        landmark,
        locality,
        city,
        district,
        state,
        postalCode,
        country,
        formattedAddress,
      })
    : formatClinicAddress({
        addressLine,
        addressLine2,
        landmark,
        locality,
        city,
        district,
        state,
        postalCode,
        country,
        formattedAddress,
      });

  const location: MapLocation = {
    name: clinicName,
    address: address || formattedAddress,
    latitude,
    longitude,
    googlePlaceId,
    mapsUrl,
    directionsUrl,
  };
  const canOpenMaps = hasMapDestination(location);

  if (!address && !canOpenMaps) {
    return (
      <Card elevated style={{ gap: spacing.sm }}>
        <AppText variant="label">{title}</AppText>
        {clinicName ? <AppText variant="headline">{clinicName}</AppText> : null}
        <AppText variant="muted">{unavailableLabel}</AppText>
      </Card>
    );
  }

  return (
    <Card elevated style={{ gap: spacing.sm }}>
      <View style={{ flexDirection: 'row', alignItems: 'flex-start', gap: spacing.md }}>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={viewOnMapLabel}
          disabled={!canOpenMaps}
          onPress={() => {
            if (canOpenMaps) {
              void openMapsLocation(location);
            }
          }}
          style={{
            width: 44,
            height: 44,
            borderRadius: radius.md,
            backgroundColor: theme.colors.primaryLight,
            alignItems: 'center',
            justifyContent: 'center',
            opacity: canOpenMaps ? 1 : 0.4,
          }}>
          <MapPinIcon color={theme.colors.primary} />
        </Pressable>
        <View style={{ flex: 1, gap: 4, minWidth: 0 }}>
          <AppText variant="label">{title}</AppText>
          {clinicName ? (
            <AppText variant="headline" numberOfLines={2}>
              {clinicName}
            </AppText>
          ) : null}
          {address ? (
            <AppText variant="muted" numberOfLines={compact ? 2 : 4}>
              {address}
            </AppText>
          ) : null}
          {landmark && !compact ? (
            <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
              Near {landmark}
            </AppText>
          ) : null}
          {locationVerified === false && address && unverifiedHint ? (
            <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
              {unverifiedHint}
            </AppText>
          ) : null}
        </View>
      </View>
      {canOpenMaps ? (
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
          <Button
            label={viewOnMapLabel}
            variant="secondary"
            onPress={() => void openMapsLocation(location)}
            style={{ flexGrow: 1, minWidth: 140 }}
          />
          <Button
            label={getDirectionsLabel}
            variant="ghost"
            onPress={() => void openDirections(location)}
            style={{ flexGrow: 1, minWidth: 120 }}
          />
        </View>
      ) : null}
    </Card>
  );
}

export function MapsIconButton({
  location,
  accessibilityLabel = 'Open in Google Maps',
}: {
  location: MapLocation;
  accessibilityLabel?: string;
}) {
  const theme = useProdocTheme();
  if (!hasMapDestination(location)) {
    return null;
  }

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel}
      hitSlop={8}
      onPress={() => void openMapsLocation(location)}
      style={{
        width: 36,
        height: 36,
        borderRadius: radius.md,
        backgroundColor: theme.colors.primaryLight,
        alignItems: 'center',
        justifyContent: 'center',
      }}>
      <MapPinIcon color={theme.colors.primary} size={18} />
    </Pressable>
  );
}
