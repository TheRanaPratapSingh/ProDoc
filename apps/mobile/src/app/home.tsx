import { useLocalSearchParams, useRouter } from 'expo-router';
import { useEffect } from 'react';

import { AccountHome } from '@/features/account/account-home';
import { PatientHome } from '@/features/account/patient-home';
import { useSessionStore } from '@/stores/session-store';
import {
  accountCanAccessSurface,
  homeHrefForAccount,
  needsProfileCompletion,
} from '@/utils/home-surface';

export default function HomeScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{ tab?: string }>();
  const hydrated = useSessionStore((state) => state.hydrated);
  const accessToken = useSessionStore((state) => state.accessToken);
  const account = useSessionStore((state) => state.account);
  const activeSurface = useSessionStore((state) => state.activeSurface);
  const isProfile = params.tab === 'profile';

  useEffect(() => {
    if (!hydrated) {
      return;
    }
    if (!accessToken) {
      router.replace('/sign-in');
      return;
    }
    if (!isProfile && needsProfileCompletion(account) && accountCanAccessSurface(account, 'patient')) {
      router.replace('/complete-profile');
      return;
    }
    // Profile is shared; patient home is patient-only.
    if (!isProfile && !accountCanAccessSurface(account, 'patient')) {
      router.replace(homeHrefForAccount(account, activeSurface));
    }
  }, [accessToken, account, activeSurface, hydrated, isProfile, router]);

  if (!accessToken) {
    return null;
  }

  if (isProfile) {
    return <AccountHome />;
  }

  if (!accountCanAccessSurface(account, 'patient')) {
    return null;
  }

  return <PatientHome />;
}
