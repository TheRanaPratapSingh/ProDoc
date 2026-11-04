import { Image, type ImageProps } from 'expo-image';

import { radius } from '@/theme/tokens';

/** Shared image defaults — caching, transition, and accessible sizing. */
export function OptimizedImage({
  style,
  recyclingKey,
  ...props
}: ImageProps & { recyclingKey?: string }) {
  return (
    <Image
      recyclingKey={recyclingKey}
      cachePolicy="memory-disk"
      transition={180}
      style={[{ borderRadius: radius.md }, style]}
      {...props}
    />
  );
}
