import { useRouter } from 'expo-router';
import { useEffect } from 'react';
import { View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { KeyboardAwareForm } from '@/components/layout/keyboard-aware-form';
import { NetworkBanner } from '@/components/layout/network-banner';
import { AdminSignInForm } from '@/features/authentication/admin-sign-in-form';
import { useSessionStore } from '@/stores/session-store';
import { homeHrefForAccount } from '@/utils/home-surface';

export default function AdminSignInScreen() {
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
          paddingTop: 12,
          paddingBottom: 16,
        }}>
        <View style={{ width: '100%' }}>
          <AdminSignInForm />
        </View>
      </KeyboardAwareForm>
    </SafeAreaView>
  );
}
