/** ProDoc Design 1 — Clean & Trustworthy Healthcare + Earth Green */

/** Startup splash — Earth Green gradient surfaces (native + React). */
export const splashPalette = {
  deep: '#285849',
  mid: '#356F5E',
  mint: '#EAF3EF',
  onDeep: '#FFFFFF',
  markPlate: '#1F4A3E',
  glow: 'rgba(234, 243, 239, 0.16)',
  glowSoft: 'rgba(53, 111, 94, 0.55)',
  orb: 'rgba(234, 243, 239, 0.14)',
  crossPlate: 'rgba(234, 243, 239, 0.16)',
  cross: '#EAF3EF',
} as const;

export const palette = {
  light: {
    primary: '#356F5E',
    primaryPressed: '#285849',
    primaryLight: '#EAF3EF',
    onPrimary: '#FFFFFF',
    /** Informational accent — never primary actions */
    secondary: '#3B82A0',
    accent: '#4A7C6F',
    highlight: '#D99A35',
    success: '#2F855A',
    warning: '#D99A35',
    error: '#DC4444',
    background: '#F7FAFC',
    surface: '#FFFFFF',
    text: '#14243D',
    /** Brand secondary text #64748B */
    textMuted: '#64748B',
    border: '#E2E8F0',
    brandTint: '#EAF3EF',
    brandEnd: '#1B3A4B',
    softBlue: '#E8F1F8',
    overlay: 'rgba(20, 36, 61, 0.45)',
  },
  dark: {
    primary: '#4FA388',
    primaryPressed: '#3D8A72',
    primaryLight: '#1A2F2A',
    onPrimary: '#042018',
    secondary: '#7EB6C9',
    accent: '#6BB89F',
    highlight: '#E0B35A',
    success: '#68D391',
    warning: '#F0C674',
    error: '#FC8181',
    background: '#0C1524',
    surface: '#152033',
    text: '#F1F5F9',
    textMuted: '#94A3B8',
    border: '#243044',
    brandTint: '#143028',
    brandEnd: '#1E3A5F',
    softBlue: '#15263A',
    overlay: 'rgba(0, 0, 0, 0.55)',
  },
} as const;

export const spacing = {
  xs: 4,
  sm: 8,
  md: 12,
  lg: 16,
  xl: 24,
  xxl: 32,
  xxxl: 48,
} as const;

export const radius = {
  sm: 10,
  md: 14,
  lg: 18,
  xl: 24,
  pill: 999,
} as const;

export const control = {
  height: 52,
  heightSm: 40,
  icon: 22,
  iconSm: 18,
  iconLg: 28,
} as const;

export const typography = {
  display: { size: 36, lineHeight: 44, weight: '700' as const },
  title: { size: 26, lineHeight: 34, weight: '700' as const },
  headline: { size: 20, lineHeight: 28, weight: '600' as const },
  body: { size: 16, lineHeight: 24, weight: '400' as const },
  label: { size: 14, lineHeight: 20, weight: '600' as const },
  muted: { size: 14, lineHeight: 20, weight: '400' as const },
  caption: { size: 12, lineHeight: 16, weight: '500' as const },
} as const;

export const shadow = {
  light: {
    card: '0 8px 24px rgba(20, 36, 61, 0.06)',
    elevated: '0 12px 32px rgba(20, 36, 61, 0.10)',
  },
  dark: {
    card: 'none',
    elevated: 'none',
  },
} as const;

export const duration = {
  press: 140,
  enter: 260,
  modal: 220,
  skeleton: 700,
  stagger: 40,
} as const;

/** Subtle motion — no bounce; pair with reduceMotion checks. */
export const motion = {
  pressScale: 0.97,
  cardPressScale: 0.985,
  enterDistance: 12,
} as const;

export const layout = {
  phoneMax: 430,
  contentPad: 20,
  sectionGap: 18,
  listGap: 12,
} as const;

export const breakpoint = {
  tablet: 768,
  wide: 960,
} as const;

export type ColorSchemeName = keyof typeof palette;
export type ThemeColors = (typeof palette)[ColorSchemeName];
