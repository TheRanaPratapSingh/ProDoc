import { TextInput, View, type TextInputProps } from 'react-native';

import { useActiveLanguage } from '@/i18n/copy';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { control, radius, spacing } from '@/theme/tokens';

export function SearchBar({
  placeholder,
  ...props
}: TextInputProps & { placeholder: string }) {
  const theme = useProdocTheme();
  const language = useActiveLanguage();

  return (
    <View
      style={{
        minHeight: control.height,
        borderRadius: radius.md,
        borderWidth: 1.5,
        borderColor: theme.colors.border,
        backgroundColor: theme.colors.surface,
        paddingHorizontal: spacing.lg,
        justifyContent: 'center',
      }}>
      <TextInput
        placeholder={placeholder}
        placeholderTextColor={theme.colors.textMuted}
        accessibilityLabel={placeholder}
        style={{
          color: theme.colors.text,
          fontSize: 16,
          fontFamily: language === 'hi' ? 'NotoSansDevanagari_400Regular' : 'PlusJakartaSans_400Regular',
        }}
        {...props}
      />
    </View>
  );
}
