import { useRouter } from 'expo-router';
import { useEffect } from 'react';
import { View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { KeyboardAwareForm } from '@/components/layout/keyboard-aware-form';
import { NetworkBanner } from '@/components/layout/network-banner';
import { SignInForm } from '@/features/authentication/sign-in-form';
import { useSessionStore } from '@/stores/session-store';
import { homeHrefForAccount } from '@/utils/home-surface';

export default function SignInScreen() {
  const router = useRouter();
  const hydrated = useSessionStore((state) => state.hydrated);
  const accessToken = useSessionStore((state) => state.accessToken);
  const account = useSessionStore((state) => state.account);
  const activeSurface = useSessionStore((state) => state.activeSurface);

  useEffect(() => {
    if (hydrated && accessToken) {
      router.replace(homeHrefForAccount(account, activeSurface));
    }
  }, [accessToken, account, activeSurface, hydrated, router]);

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: '#F7FAFC' }} edges={['top', 'left', 'right', 'bottom']}>
      <NetworkBanner />
      <KeyboardAwareForm
        mode="layout"
        contentContainerStyle={{
          flexGrow: 1,
          justifyContent: 'center',
          paddingHorizontal: 20,
          paddingTop: 8,
          paddingBottom: 12,
        }}>
        <View style={{ width: '100%' }}>
          <SignInForm />
        </View>
      </KeyboardAwareForm>
    </SafeAreaView>
  );
}
