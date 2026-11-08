import { Platform } from 'react-native';
import type { KeyboardAwareScrollViewProps } from 'react-native-keyboard-controller';

import { spacing } from '@/theme/tokens';

/** Comfortable gap between focused field / submit and the keyboard — not a screen-specific hack. */
export const FORM_KEYBOARD_BOTTOM_OFFSET = spacing.md;

export type KeyboardAwareScrollOptions = {
  /** Extra space above the keyboard for focused inputs / sticky CTAs. */
  bottomOffset?: number;
  /** When true, keeps scroll position when the keyboard hides (reduces jump). */
  preservePositionOnHide?: boolean;
};

/**
 * Shared KeyboardAwareScrollView defaults for ProDoc forms.
 * Prefer these over per-screen KeyboardAvoidingView offsets.
 */
export function useKeyboardAwareScroll(
  options: KeyboardAwareScrollOptions = {},
): Pick<
  KeyboardAwareScrollViewProps,
  | 'bottomOffset'
  | 'keyboardShouldPersistTaps'
  | 'keyboardDismissMode'
  | 'disableScrollOnKeyboardHide'
  | 'showsVerticalScrollIndicator'
  | 'automaticallyAdjustKeyboardInsets'
  | 'nestedScrollEnabled'
> {
  const {
    bottomOffset = FORM_KEYBOARD_BOTTOM_OFFSET,
    preservePositionOnHide = true,
  } = options;

  return {
    bottomOffset,
    keyboardShouldPersistTaps: 'handled',
    // Interactive dismiss on iOS; on-drag elsewhere (Android/web).
    keyboardDismissMode: Platform.OS === 'ios' ? 'interactive' : 'on-drag',
    disableScrollOnKeyboardHide: preservePositionOnHide,
    showsVerticalScrollIndicator: false,
    automaticallyAdjustKeyboardInsets: false,
    nestedScrollEnabled: true,
  };
}
