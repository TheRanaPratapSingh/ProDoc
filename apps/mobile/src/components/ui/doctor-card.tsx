import { Pressable, View } from 'react-native';

import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { StatusBadge } from '@/components/ui/status-badge';
import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';

export function DoctorCard({
  name,
  specialization,
  experience,
  fee,
  location,
  distance,
  verifiedLabel,
  feeLabel,
  viewLabel,
  directionsLabel,
  onView,
  onDirections,
}: {
  name: string;
  specialization?: string | null;
  experience?: string | number | null;
  fee?: string | number | null;
  location?: string | null;
  distance?: string | null;
  verifiedLabel?: string;
  feeLabel: string;
  viewLabel: string;
  directionsLabel?: string;
  onView: () => void;
  onDirections?: () => void;
}) {
  const theme = useProdocTheme();
  const initials = name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('');

  return (
    <Card elevated style={{ gap: spacing.md }}>
      <View style={{ flexDirection: 'row', gap: spacing.md, alignItems: 'center' }}>
        <View
          style={{
            width: 56,
            height: 56,
            borderRadius: radius.md,
            backgroundColor: theme.colors.primaryLight,
            alignItems: 'center',
            justifyContent: 'center',
          }}>
          <AppText variant="headline" style={{ color: theme.colors.primary }}>
            {initials || 'Dr'}
          </AppText>
        </View>
        <View style={{ flex: 1, gap: spacing.xs }}>
          <AppText variant="headline">{name}</AppText>
          <AppText variant="muted">
            {[specialization, experience != null && experience !== '' ? `${experience}` : null]
              .filter(Boolean)
              .join(' · ')}
          </AppText>
          {verifiedLabel ? <StatusBadge label={verifiedLabel} tone="primary" /> : null}
        </View>
      </View>
      {location ? <AppText variant="muted">{location}</AppText> : null}
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm, justifyContent: 'space-between' }}>
        <AppText variant="label">
          {feeLabel}: ₹{fee ?? '—'}
        </AppText>
        {distance ? <AppText variant="muted">{distance}</AppText> : null}
      </View>
      <View style={{ flexDirection: 'row', gap: spacing.sm }}>
        <Button label={viewLabel} onPress={onView} style={{ flex: 1 }} />
        {onDirections && directionsLabel ? (
          <Button label={directionsLabel} variant="secondary" onPress={onDirections} style={{ flex: 1 }} />
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
  branches,
  viewLabel,
  onView,
}: {
  name: string;
  type?: string | null;
  verifiedLabel?: string;
  specializations?: string;
  branches?: string[];
  viewLabel: string;
  onView: () => void;
}) {
  const theme = useProdocTheme();

  return (
    <Pressable onPress={onView}>
      <Card elevated style={{ gap: spacing.md }}>
        <View style={{ gap: spacing.xs }}>
          <AppText variant="headline">{name}</AppText>
          <AppText variant="muted">{[verifiedLabel, type].filter(Boolean).join(' · ')}</AppText>
          {specializations ? <AppText variant="muted">{specializations}</AppText> : null}
        </View>
        {(branches ?? []).map((line) => (
          <AppText key={line} variant="muted">
            {line}
          </AppText>
        ))}
        <View
          style={{
            height: 4,
            borderRadius: radius.pill,
            backgroundColor: theme.colors.primaryLight,
          }}
        />
        <Button label={viewLabel} onPress={onView} />
      </Card>
    </Pressable>
  );
}
