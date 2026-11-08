import { type ReactNode } from 'react';
import { Pressable, View } from 'react-native';
import Animated, { FadeInDown } from 'react-native-reanimated';

import { IconChevron } from '@/components/ui/prodoc-icons';
import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';

export function ProfileMenuCard({ children }: { children: ReactNode }) {
  const theme = useProdocTheme();
  return (
    <View
      style={{
        backgroundColor: theme.colors.surface,
        borderRadius: 16,
        borderWidth: 1,
        borderColor: theme.colors.border,
        overflow: 'hidden',
      }}>
      {children}
    </View>
  );
}

export function ProfileMenuSection({
  title,
  subtitle,
  icon,
  open,
  onToggle,
  children,
  reduceMotion,
}: {
  title: string;
  subtitle: string;
  icon: ReactNode;
  open: boolean;
  onToggle: () => void;
  children?: ReactNode;
  reduceMotion?: boolean;
}) {
  const theme = useProdocTheme();

  return (
    <View>
      <Pressable
        onPress={onToggle}
        accessibilityRole="button"
        accessibilityState={{ expanded: open }}
        style={({ pressed }) => ({
          flexDirection: 'row',
          alignItems: 'center',
          gap: 12,
          paddingHorizontal: 14,
          paddingVertical: 14,
          backgroundColor: pressed ? theme.colors.primaryLight : theme.colors.surface,
        })}>
        <View
          style={{
            width: 40,
            height: 40,
            borderRadius: 12,
            backgroundColor: theme.colors.primaryLight,
            alignItems: 'center',
            justifyContent: 'center',
          }}>
          {icon}
        </View>
        <View style={{ flex: 1, gap: 2, minWidth: 0 }}>
          <AppText variant="label" style={{ fontSize: 15, lineHeight: 20 }} numberOfLines={1}>
            {title}
          </AppText>
          <AppText variant="muted" style={{ fontSize: 12, lineHeight: 16 }} numberOfLines={1}>
            {subtitle}
          </AppText>
        </View>
        <IconChevron size={18} color={theme.colors.textMuted} direction={open ? 'down' : 'right'} />
      </Pressable>
      {open && children ? (
        <Animated.View
          entering={reduceMotion ? undefined : FadeInDown.duration(180)}
          style={{
            borderTopWidth: 1,
            borderTopColor: theme.colors.border,
            backgroundColor: theme.colors.background,
            paddingVertical: 4,
          }}>
          {children}
        </Animated.View>
      ) : null}
    </View>
  );
}

export function ProfileMenuLink({
  label,
  onPress,
  detail,
  danger,
}: {
  label: string;
  onPress: () => void;
  detail?: string;
  danger?: boolean;
}) {
  const theme = useProdocTheme();
  return (
    <Pressable
      onPress={onPress}
      style={({ pressed }) => ({
        flexDirection: 'row',
        alignItems: 'center',
        gap: spacing.sm,
        paddingHorizontal: 16,
        paddingVertical: 13,
        paddingLeft: 66,
        backgroundColor: pressed ? theme.colors.primaryLight : 'transparent',
      })}>
      <View style={{ flex: 1, gap: 2 }}>
        <AppText
          variant="body"
          style={{
            fontSize: 14,
            lineHeight: 20,
            color: danger ? theme.colors.error : theme.colors.text,
          }}>
          {label}
        </AppText>
        {detail ? (
          <AppText variant="muted" style={{ fontSize: 12, lineHeight: 16 }}>
            {detail}
          </AppText>
        ) : null}
      </View>
      <IconChevron size={16} color={theme.colors.textMuted} />
    </Pressable>
  );
}

export function ProfileMenuDivider() {
  const theme = useProdocTheme();
  return <View style={{ height: 1, backgroundColor: theme.colors.border, marginLeft: 66 }} />;
}
