import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { AccessibilityInfo, useColorScheme } from 'react-native';

import { useUiStore } from '@/stores/ui-store';
import { palette, type ColorSchemeName, type ThemeColors } from '@/theme/tokens';

type ProdocTheme = {
  scheme: ColorSchemeName;
  colors: ThemeColors;
  reduceMotion: boolean;
};

const ThemeContext = createContext<ProdocTheme | null>(null);

export function ProDocThemeProvider({ children }: { children: ReactNode }) {
  const system = useColorScheme();
  const preference = useUiStore((state) => state.colorScheme);
  const [reduceMotion, setReduceMotion] = useState(false);
  const scheme: ColorSchemeName =
    preference === 'system' ? (system === 'dark' ? 'dark' : 'light') : preference;

  useEffect(() => {
    let active = true;
    void AccessibilityInfo.isReduceMotionEnabled().then((enabled) => {
      if (active) {
        setReduceMotion(enabled);
      }
    });
    const subscription = AccessibilityInfo.addEventListener('reduceMotionChanged', setReduceMotion);
    return () => {
      active = false;
      subscription.remove();
    };
  }, []);

  const value = useMemo<ProdocTheme>(
    () => ({ scheme, colors: palette[scheme], reduceMotion }),
    [reduceMotion, scheme],
  );

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useProdocTheme() {
  const theme = useContext(ThemeContext);
  if (!theme) {
    throw new Error('useProdocTheme must be used within ProDocThemeProvider.');
  }
  return theme;
}
