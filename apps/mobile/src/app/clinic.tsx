import { useRouter, type Href } from 'expo-router';
import { useEffect } from 'react';

import { ClinicOnboarding } from '@/features/providers/clinic-onboarding';
import { useAccountPermissions, useSessionStore } from '@/stores/session-store';
import { canCreateClinic, canManageProviderApplications } from '@/utils/admin-permissions';
import { homeHrefForAccount } from '@/utils/home-surface';

/** Clinic application — create is Super Admin only; others may view owned clinics. */
export default function ClinicScreen() {
  const router = useRouter();
  const hydrated = useSessionStore((state) => state.hydrated);
  const accessToken = useSessionStore((state) => state.accessToken);
  const account = useSessionStore((state) => state.account);
  const activeSurface = useSessionStore((state) => state.activeSurface);
  const permissions = useAccountPermissions();
  const allowed = canCreateClinic(permissions) || canManageProviderApplications(permissions);

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

  return <ClinicOnboarding />;
}
