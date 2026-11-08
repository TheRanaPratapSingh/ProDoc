import { useQuery } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { Pressable, View } from 'react-native';

import { IconBell } from '@/components/ui/prodoc-icons';
import { AppText } from '@/components/ui/text';
import { getNotificationUnreadCount } from '@/services/api/notifications-api';
import { useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';

export function NotificationBell({
  href = '/notifications' as Href,
  accessibilityLabel = 'Notifications',
}: {
  href?: Href;
  accessibilityLabel?: string;
}) {
  const theme = useProdocTheme();
  const router = useRouter();
  const accessToken = useSessionStore((state) => state.accessToken);
  const unread = useQuery({
    queryKey: ['notifications', 'unread-count'],
    queryFn: getNotificationUnreadCount,
    enabled: Boolean(accessToken),
    refetchInterval: 60_000,
  });

  const count = unread.data ?? 0;
  const badge = count > 9 ? '9+' : String(count);

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel}
      onPress={() => router.push(href)}
      hitSlop={12}
      style={{
        width: 40,
        height: 40,
        borderRadius: 20,
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: theme.colors.surface,
        borderWidth: 1,
        borderColor: theme.colors.border,
        position: 'relative',
      }}>
      <IconBell size={22} color={theme.colors.text} />
      {count > 0 ? (
        <View
          style={{
            position: 'absolute',
            top: -2,
            right: -2,
            minWidth: 18,
            height: 18,
            borderRadius: 9,
            paddingHorizontal: 4,
            alignItems: 'center',
            justifyContent: 'center',
            backgroundColor: theme.colors.primary,
          }}>
          <AppText style={{ color: '#fff', fontSize: 10, fontWeight: '700', lineHeight: 12 }}>{badge}</AppText>
        </View>
      ) : null}
    </Pressable>
  );
}
