import { useRouter, type Href } from 'expo-router';
import { useEffect } from 'react';

import { DeveloperPortal } from '@/features/partners/developer-portal';
import { useAccountPermissions, useSessionStore } from '@/stores/session-store';
import { homeHrefForAccount } from '@/utils/home-surface';

/** Developer portal route — Super Admin (partners.develop) only. */
export default function PartnersScreen() {
  const router = useRouter();
  const hydrated = useSessionStore((state) => state.hydrated);
  const accessToken = useSessionStore((state) => state.accessToken);
  const account = useSessionStore((state) => state.account);
  const activeSurface = useSessionStore((state) => state.activeSurface);
  const permissions = useAccountPermissions();
  const allowed = permissions.includes('partners.develop');

  useEffect(() => {
    if (!hydrated) {
      return;
    }
    if (!accessToken) {
      router.replace('/sign-in' as Href);
      return;
    }
    if (!allowed) {
      router.replace(homeHrefForAccount(account, activeSurface) as Href);
    }
  }, [accessToken, account, activeSurface, allowed, hydrated, router]);

  if (!hydrated || !accessToken || !allowed) {
    return null;
  }

  return <DeveloperPortal />;
}
