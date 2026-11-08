import { usePathname, useRouter, type Href } from 'expo-router';
import { useCallback, useRef } from 'react';

import { useSessionStore } from '@/stores/session-store';
import { homeHrefForAccount } from '@/utils/home-surface';

function normalizePath(value: string | Href | undefined | null): string {
  if (value == null) {
    return '';
  }
  const raw = String(value).split('?')[0] ?? '';
  if (!raw || raw === '/') {
    return '/';
  }
  return raw.endsWith('/') ? raw.slice(0, -1) : raw;
}

/**
 * Native-feeling back: pop the stack when possible, otherwise role-based home.
 * Never replaces to the same route (avoids Admin Overview ↔ Admin loops).
 * Guards against double-taps.
 */
export function useSmartBack(fallbackHref?: Href) {
  const router = useRouter();
  const pathname = usePathname();
  const account = useSessionStore((state) => state.account);
  const locking = useRef(false);

  return useCallback(() => {
    if (locking.current) {
      return;
    }
    locking.current = true;
    try {
      if (router.canGoBack()) {
        router.back();
        return;
      }

      const activeSurface = useSessionStore.getState().activeSurface;
      const fallback = normalizePath(fallbackHref ?? homeHrefForAccount(account, activeSurface));
      const current = normalizePath(pathname);
      if (!fallback || fallback === current) {
        return;
      }
      router.replace((fallbackHref ?? homeHrefForAccount(account, activeSurface)) as Href);
    } finally {
      setTimeout(() => {
        locking.current = false;
      }, 450);
    }
  }, [account, fallbackHref, pathname, router]);
}

export function useCanGoBack() {
  const router = useRouter();
  try {
    return router.canGoBack();
  } catch {
    return false;
  }
}
