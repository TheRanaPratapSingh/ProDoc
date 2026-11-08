import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { type ReactNode, useMemo, useState } from 'react';
import { ActivityIndicator, Pressable, View } from 'react-native';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/ui/empty-state';
import { NotificationCard } from '@/components/ui/notification-card';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import {
  deleteNotification,
  listMyNotifications,
  markAllNotificationsRead,
  markNotificationRead,
  type AppNotification,
} from '@/services/api/notifications-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

type Filter = 'all' | 'unread' | 'read';

function startOfLocalDay(date: Date) {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime();
}

function groupLabel(createdAtUtc: string, now = new Date()) {
  const created = new Date(createdAtUtc);
  const day = startOfLocalDay(created);
  const today = startOfLocalDay(now);
  const yesterday = today - 86_400_000;
  if (day === today) return 'Today';
  if (day === yesterday) return 'Yesterday';
  return 'Earlier';
}

export function NotificationCenter({
  title,
  shell,
}: {
  title?: string;
  shell?: (children: ReactNode) => ReactNode;
}) {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const queryClient = useQueryClient();
  const [filter, setFilter] = useState<Filter>('all');

  const unreadOnly = filter === 'unread' ? true : filter === 'read' ? false : undefined;

  const list = useInfiniteQuery({
    queryKey: ['notifications', 'list', filter],
    initialPageParam: 0,
    queryFn: ({ pageParam }) =>
      listMyNotifications({
        unreadOnly,
        skip: pageParam,
        take: 30,
      }),
    getNextPageParam: (lastPage, pages) => (lastPage.length < 30 ? undefined : pages.length * 30),
  });

  const items = useMemo(() => list.data?.pages.flat() ?? [], [list.data]);
  const grouped = useMemo(() => {
    const map = new Map<string, AppNotification[]>();
    for (const item of items) {
      const key = groupLabel(item.createdAtUtc);
      const bucket = map.get(key) ?? [];
      bucket.push(item);
      map.set(key, bucket);
    }
    return ['Today', 'Yesterday', 'Earlier']
      .map((label) => ({ label, rows: map.get(label) ?? [] }))
      .filter((group) => group.rows.length > 0);
  }, [items]);

  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: ['notifications'] });
  };

  const markAll = useMutation({
    mutationFn: markAllNotificationsRead,
    onSuccess: invalidate,
  });

  const markOne = useMutation({
    mutationFn: markNotificationRead,
    onSuccess: invalidate,
  });

  const remove = useMutation({
    mutationFn: deleteNotification,
    onSuccess: invalidate,
  });

  const body = (
    <AuthScaffold>
      <ScreenHeader title={title ?? copy.notifications} />
      <View style={{ flexDirection: 'row', gap: spacing.sm, flexWrap: 'wrap' }}>
        {([
          ['all', 'All'],
          ['unread', 'Unread'],
          ['read', 'Read'],
        ] as const).map(([id, label]) => {
          const active = filter === id;
          return (
            <Pressable
              key={id}
              onPress={() => setFilter(id)}
              style={{
                paddingHorizontal: 12,
                paddingVertical: 8,
                borderRadius: 999,
                borderWidth: 1,
                borderColor: active ? theme.colors.primary : theme.colors.border,
                backgroundColor: active ? theme.colors.primaryLight : theme.colors.surface,
              }}>
              <AppText variant="caption" style={{ color: active ? theme.colors.primary : theme.colors.text }}>
                {label}
              </AppText>
            </Pressable>
          );
        })}
      </View>

      <Button
        label="Mark all as read"
        variant="secondary"
        onPress={() => markAll.mutate()}
        disabled={markAll.isPending || items.length === 0 || items.every((item) => item.isRead)}
      />

      {list.isLoading ? <ActivityIndicator color={theme.colors.primary} /> : null}
      {!list.isLoading && items.length === 0 ? (
        <EmptyState title="No notifications yet." body="Updates about your visits will appear here." />
      ) : null}
      {list.error ? <AppText variant="muted">{problemMessage(list.error, copy.notifications)}</AppText> : null}

      {grouped.map((group) => (
        <View key={group.label} style={{ gap: spacing.sm }}>
          <AppText variant="label">{group.label}</AppText>
          {group.rows.map((item) => (
            <View key={item.id} style={{ gap: spacing.xs }}>
              <NotificationCard
                title={item.title}
                body={item.body}
                meta={new Date(item.createdAtUtc).toLocaleString()}
                unread={!item.isRead}
                onPress={() => {
                  if (!item.isRead) {
                    markOne.mutate(item.id);
                  }
                  if (item.deepLink) {
                    router.push(item.deepLink as Href);
                  }
                }}
              />
              <View style={{ flexDirection: 'row', gap: spacing.sm }}>
                {!item.isRead ? (
                  <Button label="Mark read" variant="secondary" onPress={() => markOne.mutate(item.id)} />
                ) : null}
                <Button label="Delete" variant="secondary" onPress={() => remove.mutate(item.id)} />
              </View>
            </View>
          ))}
        </View>
      ))}

      {list.hasNextPage ? (
        <Button
          label="Load older"
          variant="secondary"
          onPress={() => list.fetchNextPage()}
          disabled={list.isFetchingNextPage}
        />
      ) : null}
    </AuthScaffold>
  );

  return shell ? <>{shell(body)}</> : body;
}
