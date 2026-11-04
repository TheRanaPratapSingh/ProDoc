import { useRouter } from 'expo-router';
import { useEffect } from 'react';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { SignInForm } from '@/features/authentication/sign-in-form';
import { useSessionStore } from '@/stores/session-store';

export default function SignInScreen() {
  const router = useRouter();
  const hydrated = useSessionStore((state) => state.hydrated);
  const accessToken = useSessionStore((state) => state.accessToken);

  useEffect(() => {
    if (hydrated && accessToken) {
      router.replace('/home');
    }
  }, [accessToken, hydrated, router]);

  return (
    <AuthScaffold showHero>
      <SignInForm />
    </AuthScaffold>
  );
}
