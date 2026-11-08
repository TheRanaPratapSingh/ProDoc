import { View } from 'react-native';

import { OptimizedImage } from '@/components/ui/optimized-image';
import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';

/** Circular avatar with optional photo URL; falls back to Earth Green initials. */
export function ProfileAvatar({
  name,
  uri,
  headers,
  size = 56,
}: {
  name: string;
  uri?: string | null;
  headers?: Record<string, string>;
  size?: number;
}) {
  const theme = useProdocTheme();
  const initials = name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('');

  if (uri) {
    return (
      <OptimizedImage
        source={{ uri, headers }}
        recyclingKey={uri}
        style={{ width: size, height: size, borderRadius: size / 2 }}
        accessibilityLabel={name}
      />
    );
  }

  return (
    <View
      accessibilityLabel={name}
      style={{
        width: size,
        height: size,
        borderRadius: size / 2,
        backgroundColor: theme.colors.primaryLight,
        alignItems: 'center',
        justifyContent: 'center',
        borderWidth: 2,
        borderColor: theme.colors.primary,
      }}>
      <AppText
        variant={size >= 64 ? 'headline' : 'label'}
        style={{ color: theme.colors.primary, fontSize: Math.round(size * 0.32) }}>
        {initials || '•'}
      </AppText>
    </View>
  );
}
