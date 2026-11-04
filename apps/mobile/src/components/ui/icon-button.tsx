import { Pressable, type PressableProps } from 'react-native';

import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { control, radius } from '@/theme/tokens';

export function IconButton({
  label,
  selected = false,
  ...props
}: PressableProps & { label: string; selected?: boolean }) {
  const theme = useProdocTheme();

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      hitSlop={8}
      style={{
        minWidth: control.heightSm,
        minHeight: control.heightSm,
        borderRadius: radius.pill,
        alignItems: 'center',
        justifyContent: 'center',
        paddingHorizontal: 12,
        backgroundColor: selected ? theme.colors.primaryLight : 'transparent',
      }}
      {...props}>
      <AppText variant="label" style={{ color: selected ? theme.colors.primary : theme.colors.text }}>
        {label}
      </AppText>
    </Pressable>
  );
}
