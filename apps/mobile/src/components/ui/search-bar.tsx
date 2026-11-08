import { Pressable, TextInput, View, type TextInputProps, type StyleProp, type ViewStyle } from 'react-native';

import { IconSearch } from '@/components/ui/prodoc-icons';
import { AppText } from '@/components/ui/text';
import { useActiveLanguage } from '@/i18n/copy';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';

export function SearchBar({
  placeholder,
  containerStyle,
  pill = false,
  onClear,
  value,
  ...props
}: TextInputProps & {
  placeholder: string;
  containerStyle?: StyleProp<ViewStyle>;
  pill?: boolean;
  onClear?: () => void;
}) {
  const theme = useProdocTheme();
  const language = useActiveLanguage();
  const showClear = Boolean(onClear && typeof value === 'string' && value.length > 0);

  return (
    <View
      style={[
        {
          minHeight: 48,
          borderRadius: pill ? 999 : 14,
          borderWidth: 1,
          borderColor: theme.colors.border,
          backgroundColor: theme.colors.surface,
          paddingHorizontal: spacing.lg,
          flexDirection: 'row',
          alignItems: 'center',
          gap: spacing.sm,
        },
        containerStyle,
      ]}>
      <IconSearch size={18} color={theme.colors.textMuted} />
      <TextInput
        returnKeyType="search"
        blurOnSubmit
        placeholder={placeholder}
        placeholderTextColor={theme.colors.textMuted}
        accessibilityLabel={placeholder}
        value={value}
        style={{
          flex: 1,
          color: theme.colors.text,
          fontSize: 15,
          paddingVertical: 0,
          fontFamily: language === 'hi' ? 'NotoSansDevanagari_400Regular' : 'PlusJakartaSans_400Regular',
        }}
        {...props}
      />
      {showClear ? (
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Clear search"
          hitSlop={10}
          onPress={onClear}
          style={{
            minWidth: 28,
            minHeight: 28,
            borderRadius: 14,
            alignItems: 'center',
            justifyContent: 'center',
            backgroundColor: theme.colors.primaryLight,
          }}>
          <AppText variant="label" style={{ color: theme.colors.primary, fontSize: 14 }}>
            ×
          </AppText>
        </Pressable>
      ) : null}
    </View>
  );
}
