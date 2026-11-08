import { View } from 'react-native';

import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { useProdocTheme } from '@/theme/prodoc-theme';

type StarRatingDisplayProps = {
  average?: number | null;
  reviewCount?: number | null;
  /** Compact list style uses a shorter count label, e.g. (12). */
  compact?: boolean;
  size?: number;
};

function formatCount(count: number) {
  if (count >= 1000) {
    const value = count / 1000;
    const rounded = value >= 10 ? value.toFixed(0) : value.toFixed(1).replace(/\.0$/, '');
    return `${rounded}K`;
  }
  return String(count);
}

/** Compact read-only stars + average from real backend rating data (never fabricates scores). */
export function StarRatingDisplay({
  average,
  reviewCount,
  compact = false,
  size = 14,
}: StarRatingDisplayProps) {
  const theme = useProdocTheme();
  const copy = useCopy();
  const hasRating = average != null && average > 0 && (reviewCount == null || reviewCount > 0);
  const filled = hasRating ? Math.max(1, Math.min(5, Math.round(average!))) : 0;
  const count = reviewCount ?? 0;

  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: 4, flexWrap: 'wrap' }}>
      <View style={{ flexDirection: 'row', gap: 1 }}>
        {[1, 2, 3, 4, 5].map((star) => (
          <AppText
            key={star}
            style={{
              fontSize: size,
              lineHeight: size + 3,
              color: star <= filled ? '#E0A800' : theme.colors.border,
            }}>
            ★
          </AppText>
        ))}
      </View>
      {hasRating ? (
        <>
          <AppText
            variant="label"
            style={{ fontSize: size - 1, lineHeight: size + 2, color: theme.colors.text }}>
            {average!.toFixed(1)}
          </AppText>
          <AppText variant="muted" style={{ fontSize: size - 1, lineHeight: size + 2 }}>
            {compact
              ? `(${formatCount(count)})`
              : `(${formatCount(count)} ${copy.reviewCountLabel})`}
          </AppText>
        </>
      ) : (
        <AppText variant="muted" style={{ fontSize: size - 1, lineHeight: size + 2 }}>
          {copy.reviewNoRatingsYet}
        </AppText>
      )}
    </View>
  );
}
