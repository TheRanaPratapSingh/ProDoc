import { type ReactNode } from 'react';
import { Pressable, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { IconBack } from '@/components/ui/prodoc-icons';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { useCanGoBack, useSmartBack } from '@/navigation/use-smart-back';
import { useProdocTheme } from '@/theme/prodoc-theme';
import type { Href } from 'expo-router';

type ScreenHeaderProps = {
  title: string;
  subtitle?: string;
  /** Show back when stack can pop, or force with true. Default: auto. */
  showBack?: boolean | 'auto';
  onBack?: () => void;
  /** @deprecated Prefer icon header; kept for call-site compatibility. */
  backLabel?: string;
  fallbackHref?: Href;
  right?: ReactNode;
  /** Include top safe-area inset (default true). Set false inside Screen/SafeAreaView. */
  safeTop?: boolean;
  bordered?: boolean;
};

/** Compact native-style navigation header (Practo / Instagram style). */
export function ScreenHeader({
  title,
  subtitle,
  showBack = 'auto',
  onBack,
  fallbackHref,
  right,
  safeTop = false,
  bordered = true,
}: ScreenHeaderProps) {
  const theme = useProdocTheme();
  const copy = useCopy();
  const insets = useSafeAreaInsets();
  const smartBack = useSmartBack(fallbackHref);
  const canGoBack = useCanGoBack();
  const visible = showBack === true || (showBack === 'auto' && (Boolean(onBack) || canGoBack));
  const handleBack = onBack ?? smartBack;

  return (
    <View
      style={{
        backgroundColor: theme.colors.background,
        paddingTop: safeTop ? Math.max(insets.top, 0) : 0,
        borderBottomWidth: bordered ? 1 : 0,
        borderBottomColor: theme.colors.border,
      }}>
      <View
        style={{
          minHeight: 48,
          paddingHorizontal: 4,
          flexDirection: 'row',
          alignItems: 'center',
          gap: 2,
        }}>
        {visible ? (
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={copy.back}
            onPress={handleBack}
            hitSlop={10}
            style={{
              width: 44,
              height: 44,
              alignItems: 'center',
              justifyContent: 'center',
              borderRadius: 12,
            }}>
            <IconBack size={22} color={theme.colors.text} />
          </Pressable>
        ) : (
          <View style={{ width: 8 }} />
        )}
        <View style={{ flex: 1, paddingVertical: 8, paddingRight: 8, gap: 1 }}>
          <AppText
            variant="headline"
            style={{ fontSize: 17, lineHeight: 22, color: theme.colors.text, fontWeight: '600' }}
            numberOfLines={1}>
            {title}
          </AppText>
          {subtitle ? (
            <AppText variant="caption" style={{ color: theme.colors.textMuted }} numberOfLines={1}>
              {subtitle}
            </AppText>
          ) : null}
        </View>
        {right ? <View style={{ paddingRight: 8 }}>{right}</View> : <View style={{ width: visible ? 8 : 0 }} />}
      </View>
    </View>
  );
}

/** @deprecated Use ScreenHeader — kept for existing imports. */
export function ResponsiveHeader(props: ScreenHeaderProps) {
  return <ScreenHeader {...props} />;
}
