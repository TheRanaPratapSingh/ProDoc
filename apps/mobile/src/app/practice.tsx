import { PracticeCalendar } from '@/features/appointments/practice-calendar';
import { useRouter } from 'expo-router';
import { useEffect } from 'react';

import { useSessionStore } from '@/stores/session-store';
import { accountCanAccessSurface, homeHrefForAccount } from '@/utils/home-surface';

/** Clinic calendar tool — not a default landing page. */
export default function PracticeScreen() {
  const router = useRouter();
  const hydrated = useSessionStore((state) => state.hydrated);
  const accessToken = useSessionStore((state) => state.accessToken);
  const account = useSessionStore((state) => state.account);
  const activeSurface = useSessionStore((state) => state.activeSurface);
  const canOpen =
    accountCanAccessSurface(account, 'doctor') ||
    accountCanAccessSurface(account, 'clinic') ||
    accountCanAccessSurface(account, 'reception') ||
    accountCanAccessSurface(account, 'staff');

  useEffect(() => {
    if (!hydrated) return;
    if (!accessToken) {
      router.replace('/sign-in');
      return;
    }
    if (!canOpen) {
      router.replace(homeHrefForAccount(account, activeSurface));
    }
  }, [accessToken, account, activeSurface, canOpen, hydrated, router]);

  if (!accessToken || !canOpen) {
    return null;
  }

  return <PracticeCalendar />;
}
