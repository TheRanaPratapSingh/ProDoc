import { useRouter } from 'expo-router';
import { useEffect } from 'react';

import { AccountHome } from '@/features/account/account-home';
import { useSessionStore } from '@/stores/session-store';

export default function HomeScreen() {
  const router = useRouter();
  const hydrated = useSessionStore((state) => state.hydrated);
  const accessToken = useSessionStore((state) => state.accessToken);

  useEffect(() => {
    if (hydrated && !accessToken) {
      router.replace('/sign-in');
    }
  }, [accessToken, hydrated, router]);

  if (!accessToken) {
    return null;
  }

  return <AccountHome />;
}
