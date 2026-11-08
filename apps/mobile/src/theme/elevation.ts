import { Platform, type ViewStyle } from 'react-native';

import type { ColorSchemeName } from '@/theme/tokens';

/** Native-friendly elevation. Prefer over CSS boxShadow on native lists. */
export function cardElevation(scheme: ColorSchemeName, level: 'card' | 'elevated' = 'card'): ViewStyle {
  if (scheme === 'dark') {
    return Platform.select({
      ios: { shadowOpacity: 0 },
      android: { elevation: 0 },
      default: {},
    }) as ViewStyle;
  }

  if (level === 'elevated') {
    return Platform.select({
      ios: {
        shadowColor: '#14243D',
        shadowOffset: { width: 0, height: 8 },
        shadowOpacity: 0.1,
        shadowRadius: 16,
      },
      android: { elevation: 4 },
      web: { boxShadow: '0 12px 32px rgba(20, 36, 61, 0.10)' },
      default: {},
    }) as ViewStyle;
  }

  return Platform.select({
    ios: {
      shadowColor: '#14243D',
      shadowOffset: { width: 0, height: 4 },
      shadowOpacity: 0.06,
      shadowRadius: 12,
    },
    android: { elevation: 2 },
    web: { boxShadow: '0 8px 24px rgba(20, 36, 61, 0.06)' },
    default: {},
  }) as ViewStyle;
}
