import { useRouter } from 'expo-router';
import { useEffect } from 'react';

import { NotificationCenter } from '@/features/notifications/notification-center';
import { useSessionStore } from '@/stores/session-store';

/** Shared notification center for patient, clinic, and admin surfaces. */
export default function NotificationsRoute() {
  const router = useRouter();
  const hydrated = useSessionStore((state) => state.hydrated);
  const accessToken = useSessionStore((state) => state.accessToken);

  useEffect(() => {
    if (!hydrated) {
      return;
    }
    if (!accessToken) {
      router.replace('/sign-in');
    }
  }, [accessToken, hydrated, router]);

  if (!hydrated || !accessToken) {
    return null;
  }

  return <NotificationCenter />;
}
