import { useRouter, type Href } from 'expo-router';
import { useEffect } from 'react';

import { useSessionStore } from '@/stores/session-store';
import {
  accountCanAccessSurface,
  homeHrefForAccount,
  type HomeSurface,
} from '@/utils/home-surface';

/** Redirects unauthenticated or wrong-role users away from a role-gated screen. */
export function useRequireSurface(surface: HomeSurface, signInHref: Href = '/sign-in') {
  const router = useRouter();
  const hydrated = useSessionStore((state) => state.hydrated);
  const accessToken = useSessionStore((state) => state.accessToken);
  const account = useSessionStore((state) => state.account);
  const activeSurface = useSessionStore((state) => state.activeSurface);
  const allowed = Boolean(accessToken && accountCanAccessSurface(account, surface));

  useEffect(() => {
    if (!hydrated) {
      return;
    }
    if (!accessToken) {
      router.replace(signInHref);
      return;
    }
    if (!accountCanAccessSurface(account, surface)) {
      router.replace(homeHrefForAccount(account, activeSurface));
    }
  }, [accessToken, account, activeSurface, hydrated, router, signInHref, surface]);

  return { ready: hydrated && allowed, account };
}
