import { type ReactElement, type ReactNode } from 'react';
import {
  FlatList,
  type FlatListProps,
  KeyboardAvoidingView,
  Platform,
  RefreshControl,
  View,
} from 'react-native';
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
  estimatedItemSize?: number;
} & Omit<FlatListProps<T>, 'data' | 'renderItem' | 'keyExtractor' | 'ListHeaderComponent' | 'ListEmptyComponent'>;

export function ScreenList<T>({
  header,
  data,
  keyExtractor,
  renderItem,
  refreshing,
  onRefresh,
  empty,
  ...rest
}: ScreenListProps<T>) {
  const theme = useProdocTheme();

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: theme.colors.background }}>
      <NetworkBanner />
      <KeyboardAvoidingView style={{ flex: 1 }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
        <FlatList
          data={data}
          keyExtractor={keyExtractor}
          renderItem={renderItem}
          contentContainerStyle={{ padding: spacing.xl, gap: spacing.md, flexGrow: 1 }}
          keyboardShouldPersistTaps="handled"
          initialNumToRender={8}
          maxToRenderPerBatch={8}
          windowSize={7}
          removeClippedSubviews={Platform.OS === 'android'}
          refreshControl={
            onRefresh ? (
              <RefreshControl refreshing={!!refreshing} onRefresh={onRefresh} tintColor={theme.colors.primary} />
            ) : undefined
          }
          ListHeaderComponent={
            header ? <View style={{ gap: spacing.md, marginBottom: spacing.md }}>{header}</View> : undefined
          }
          ListEmptyComponent={empty ? <View style={{ paddingVertical: spacing.xl }}>{empty}</View> : undefined}
          {...rest}
        />
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}
