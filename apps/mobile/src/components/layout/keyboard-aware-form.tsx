import { forwardRef, type ReactNode } from 'react';
import {
  Platform,
  ScrollView,
  type ScrollViewProps,
  type StyleProp,
  type ViewStyle,
} from 'react-native';
import {
  KeyboardAwareScrollView,
  type KeyboardAwareScrollViewProps,
  type KeyboardAwareScrollViewRef,
  KeyboardToolbar,
} from 'react-native-keyboard-controller';

import {
  FORM_KEYBOARD_BOTTOM_OFFSET,
  useKeyboardAwareScroll,
} from '@/hooks/use-keyboard-aware-scroll';
import { useProdocTheme } from '@/theme/prodoc-theme';

export { FORM_KEYBOARD_BOTTOM_OFFSET, useKeyboardAwareScroll };

type FormScrollProps = Omit<KeyboardAwareScrollViewProps, 'bottomOffset'> & {
  /** Extra space above keyboard; defaults to shared form offset. */
  bottomOffset?: number;
  /** Earth Green prev/next/done accessory. Default true. */
  toolbar?: boolean;
  /** Keep scroll position when keyboard hides. Default true. */
  preservePositionOnHide?: boolean;
};

/**
 * Primary scroll shell for multi-field forms.
 * Uses react-native-keyboard-controller — no nested KeyboardAvoidingView needed.
 */
export const FormScreenScroll = forwardRef<KeyboardAwareScrollViewRef, FormScrollProps>(
  function FormScreenScroll(
    {
      children,
      contentContainerStyle,
      bottomOffset = FORM_KEYBOARD_BOTTOM_OFFSET,
      toolbar = true,
      preservePositionOnHide = true,
      style,
      ...rest
    },
    ref,
  ) {
    const defaults = useKeyboardAwareScroll({ bottomOffset, preservePositionOnHide });
    const theme = useProdocTheme();

    const scrollStyle = [{ flex: 1 }, style];
    const containerStyle: StyleProp<ViewStyle> = contentContainerStyle;

    // Web uses the library's JS stubs; ScrollView fallback keeps layout identical if native is unavailable.
    if (Platform.OS === 'web') {
      return (
        <ScrollView
          ref={ref as never}
          style={scrollStyle}
          contentContainerStyle={containerStyle}
          keyboardShouldPersistTaps={defaults.keyboardShouldPersistTaps}
          keyboardDismissMode={defaults.keyboardDismissMode}
          showsVerticalScrollIndicator={defaults.showsVerticalScrollIndicator}
          nestedScrollEnabled={defaults.nestedScrollEnabled}
          {...(rest as ScrollViewProps)}>
          {children}
        </ScrollView>
      );
    }

    return (
      <>
        <KeyboardAwareScrollView
          ref={ref}
          style={scrollStyle}
          contentContainerStyle={containerStyle}
          {...defaults}
          bottomOffset={bottomOffset}
          {...rest}>
          {children}
        </KeyboardAwareScrollView>
        {toolbar ? (
          <KeyboardToolbar
            theme={{
              light: {
                primary: theme.colors.primary,
                disabled: theme.colors.textMuted,
                background: theme.colors.surface,
                ripple: theme.colors.primaryLight,
              },
              dark: {
                primary: theme.colors.primary,
                disabled: theme.colors.textMuted,
                background: theme.colors.surface,
                ripple: theme.colors.primaryLight,
              },
            }}
          />
        ) : null}
      </>
    );
  },
);

type KeyboardAwareFormProps = FormScrollProps & {
  children: ReactNode;
};

/** Semantic alias for form screens — toolbar enabled by default. */
export function KeyboardAwareForm({ children, toolbar = true, ...props }: KeyboardAwareFormProps) {
  return (
    <FormScreenScroll toolbar={toolbar} {...props}>
      {children}
    </FormScreenScroll>
  );
}
