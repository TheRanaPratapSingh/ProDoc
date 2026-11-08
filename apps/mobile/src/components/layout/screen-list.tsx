import { FlashList } from '@shopify/flash-list';
import { type ReactElement, type ReactNode } from 'react';
import { Platform, RefreshControl, View } from 'react-native';
import { KeyboardAvoidingView } from 'react-native-keyboard-controller';
import { SafeAreaView } from 'react-native-safe-area-context';

import { NetworkBanner } from '@/components/layout/network-banner';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';

type ScreenListProps<T> = {
  header?: ReactNode;
  data: T[];
  keyExtractor: (item: T, index: number) => string;
  renderItem: (info: { item: T; index: number }) => ReactElement | null;
  refreshing?: boolean;
  onRefresh?: () => void;
  empty?: ReactNode;
  /** Kept for API compatibility; FlashList 2 sizes automatically. */
  estimatedItemSize?: number;
  contentContainerStyle?: object;
};

export function ScreenList<T>({
  header,
  data,
  keyExtractor,
  renderItem,
  refreshing,
  onRefresh,
  empty,
  contentContainerStyle,
}: ScreenListProps<T>) {
  const theme = useProdocTheme();

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: theme.colors.background }}>
      <NetworkBanner />
      <KeyboardAvoidingView style={{ flex: 1 }} behavior="padding" automaticOffset>
        <FlashList
          data={data}
          keyExtractor={keyExtractor}
          renderItem={({ item, index }) => renderItem({ item, index })}
          contentContainerStyle={{
            padding: spacing.xl,
            paddingBottom: spacing.xxl,
            ...contentContainerStyle,
          }}
          ItemSeparatorComponent={() => <View style={{ height: spacing.md }} />}
          keyboardShouldPersistTaps="handled"
          keyboardDismissMode={Platform.OS === 'ios' ? 'interactive' : 'on-drag'}
          refreshControl={
            onRefresh ? (
              <RefreshControl refreshing={!!refreshing} onRefresh={onRefresh} tintColor={theme.colors.primary} />
            ) : undefined
          }
          ListHeaderComponent={
            header ? <View style={{ gap: spacing.md, marginBottom: spacing.md }}>{header}</View> : undefined
          }
          ListEmptyComponent={empty ? <View style={{ paddingVertical: spacing.xl }}>{empty}</View> : undefined}
        />
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}
