import { Pressable, View } from 'react-native';
import Animated, { useAnimatedStyle, useSharedValue, withSequence, withTiming } from 'react-native-reanimated';

import { MapsIconButton } from '@/components/ui/clinic-location-card';
import { IconHeart } from '@/components/ui/prodoc-icons';
import { ProfileAvatar } from '@/components/ui/profile-avatar';
import { StarRatingDisplay } from '@/components/ui/star-rating-display';
import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { duration, spacing } from '@/theme/tokens';
import type { MapLocation } from '@/utils/maps';

/** Compact discovery card matching Find Doctors reference. */
export function FindDoctorCard({
  name,
  specialization,
  experience,
  clinicName,
  locationLabel,
  mapLocation,
  photoUri,
  photoHeaders,
  rating,
  reviewCount,
  favourited,
  onPress,
  onToggleFavourite,
}: {
  name: string;
  specialization?: string | null;
  experience?: string | null;
  clinicName?: string | null;
  locationLabel?: string | null;
  mapLocation?: MapLocation | null;
  photoUri?: string | null;
  photoHeaders?: Record<string, string>;
  rating?: number | null;
  reviewCount?: number | null;
  favourited?: boolean;
  onPress: () => void;
  onToggleFavourite?: () => void;
}) {
  const theme = useProdocTheme();
  const heartScale = useSharedValue(1);
  const heartStyle = useAnimatedStyle(() => ({ transform: [{ scale: heartScale.value }] }));

  return (
    <Pressable
      onPress={onPress}
      style={({ pressed }) => ({
        backgroundColor: theme.colors.surface,
        borderRadius: 18,
        borderWidth: 1,
        borderColor: theme.colors.border,
        padding: 14,
        flexDirection: 'row',
        alignItems: 'center',
        gap: spacing.md,
        opacity: pressed ? 0.96 : 1,
        transform: [{ scale: pressed ? 0.99 : 1 }],
        boxShadow: theme.scheme === 'light' ? '0 6px 18px rgba(20, 36, 61, 0.06)' : undefined,
      })}>
      <ProfileAvatar name={name} uri={photoUri} headers={photoHeaders} size={56} />
      <View style={{ flex: 1, gap: 4, minWidth: 0 }}>
        <AppText variant="headline" style={{ fontSize: 16, lineHeight: 22 }} numberOfLines={1}>
          {name}
        </AppText>
        <AppText variant="muted" style={{ fontSize: 13, lineHeight: 18 }} numberOfLines={1}>
          {specialization || '—'}
        </AppText>
        {experience ? (
          <AppText variant="muted" style={{ fontSize: 12, lineHeight: 16 }} numberOfLines={1}>
            {experience}
          </AppText>
        ) : null}
        {clinicName || locationLabel ? (
          <AppText variant="muted" style={{ fontSize: 12, lineHeight: 16 }} numberOfLines={1}>
            {[clinicName, locationLabel].filter(Boolean).join(' · ')}
          </AppText>
        ) : null}
        {rating !== undefined || reviewCount !== undefined ? (
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8, marginTop: 2 }}>
            <StarRatingDisplay average={rating} reviewCount={reviewCount} compact size={13} />
            {mapLocation ? <MapsIconButton location={mapLocation} /> : null}
          </View>
        ) : mapLocation ? (
          <View style={{ marginTop: 2 }}>
            <MapsIconButton location={mapLocation} />
          </View>
        ) : null}
      </View>
      {onToggleFavourite ? (
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Favourite"
          hitSlop={10}
          onPress={() => {
            heartScale.value = withSequence(
              withTiming(1.25, { duration: duration.press }),
              withTiming(1, { duration: duration.press }),
            );
            onToggleFavourite();
          }}
          style={{ padding: 4 }}>
          <Animated.View style={heartStyle}>
            <IconHeart size={22} color="#E25555" filled={Boolean(favourited)} />
          </Animated.View>
        </Pressable>
      ) : null}
    </Pressable>
  );
}
