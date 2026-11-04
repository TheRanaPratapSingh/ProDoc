import { Text, type TextProps, type TextStyle } from 'react-native';

import { useActiveLanguage } from '@/i18n/copy';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { typography } from '@/theme/tokens';

type Variant = keyof typeof typography;

const englishFont: Record<Variant, string> = {
  display: 'PlusJakartaSans_700Bold',
  title: 'PlusJakartaSans_700Bold',
  headline: 'PlusJakartaSans_600SemiBold',
  body: 'PlusJakartaSans_400Regular',
  label: 'PlusJakartaSans_600SemiBold',
  muted: 'PlusJakartaSans_400Regular',
  caption: 'PlusJakartaSans_500Medium',
};

const hindiFont: Record<Variant, string> = {
  display: 'NotoSansDevanagari_700Bold',
  title: 'NotoSansDevanagari_700Bold',
  headline: 'NotoSansDevanagari_600SemiBold',
  body: 'NotoSansDevanagari_400Regular',
  label: 'NotoSansDevanagari_600SemiBold',
  muted: 'NotoSansDevanagari_400Regular',
  caption: 'NotoSansDevanagari_600SemiBold',
};

export function AppText({
  variant = 'body',
  style,
  ...props
}: TextProps & { variant?: Variant }) {
  const theme = useProdocTheme();
  const language = useActiveLanguage();
  const meta = typography[variant];
  const textStyle: TextStyle = {
    color: variant === 'muted' || variant === 'caption' ? theme.colors.textMuted : theme.colors.text,
    fontSize: meta.size,
    lineHeight: meta.lineHeight,
    fontFamily: language === 'hi' ? hindiFont[variant] : englishFont[variant],
  };

  return <Text {...props} style={[textStyle, style]} />;
}
