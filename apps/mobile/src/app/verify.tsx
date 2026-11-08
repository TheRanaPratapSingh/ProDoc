import { useRouter } from 'expo-router';
import { useEffect } from 'react';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { VerifyForm } from '@/features/authentication/verify-form';
import { useSessionStore } from '@/stores/session-store';
import { homeHrefForAccount } from '@/utils/home-surface';

export default function VerifyScreen() {
  const router = useRouter();
  const hydrated = useSessionStore((state) => state.hydrated);
  const accessToken = useSessionStore((state) => state.accessToken);
  const account = useSessionStore((state) => state.account);
  const activeSurface = useSessionStore((state) => state.activeSurface);
  const pending = useSessionStore((state) => state.pending);

  useEffect(() => {
    if (!hydrated) {
      return;
    }

    if (accessToken) {
      router.replace(homeHrefForAccount(account, activeSurface));
      return;
    }

    if (!pending) {
      router.replace('/sign-in');
    }
  }, [accessToken, account, activeSurface, hydrated, pending, router]);

  return (
    <AuthScaffold showHero>
      <VerifyForm />
    </AuthScaffold>
  );
}
