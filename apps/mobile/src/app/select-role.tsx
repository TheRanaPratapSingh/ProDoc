import { useRouter } from 'expo-router';
import { useEffect } from 'react';
import { Pressable, View } from 'react-native';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';
import {
  homeHrefForAccount,
  surfacesForAccount,
  surfaceLabel,
  type HomeSurface,
} from '@/utils/home-surface';

export default function SelectRoleScreen() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const account = useSessionStore((state) => state.account);
  const accessToken = useSessionStore((state) => state.accessToken);
  const hydrated = useSessionStore((state) => state.hydrated);
  const setActiveSurface = useSessionStore((state) => state.setActiveSurface);
  const surfaces = surfacesForAccount(account);

  useEffect(() => {
    if (!hydrated) {
      return;
    }
    if (!accessToken || !account) {
      router.replace('/sign-in');
      return;
    }
    if (surfaces.length <= 1) {
      const only = surfaces[0] ?? 'patient';
      setActiveSurface(only);
      router.replace(homeHrefForAccount(account, only));
    }
  }, [accessToken, account, hydrated, router, setActiveSurface, surfaces]);

  if (!hydrated || !accessToken || !account || surfaces.length <= 1) {
    return null;
  }

  return (
    <AuthScaffold>
      <ScreenHeader title={copy.selectRoleTitle} showBack={false} />
      <AppText variant="muted">{copy.selectRoleBody}</AppText>
      <View style={{ gap: spacing.sm }}>
        {surfaces.map((surface) => (
          <Pressable
            key={surface}
            onPress={() => {
              setActiveSurface(surface);
              router.replace(homeHrefForAccount(account, surface));
            }}
            style={{
              borderWidth: 1,
              borderColor: theme.colors.border,
              borderRadius: radius.lg,
              backgroundColor: theme.colors.surface,
              padding: spacing.lg,
              gap: 4,
            }}>
            <AppText variant="headline" style={{ color: theme.colors.primary }}>
              {surfaceLabel(surface, copy)}
            </AppText>
            <AppText variant="muted">{surfaceDescription(surface, copy)}</AppText>
          </Pressable>
        ))}
      </View>
    </AuthScaffold>
  );
}

function surfaceDescription(surface: HomeSurface, copy: ReturnType<typeof useCopy>) {
  switch (surface) {
    case 'admin':
      return copy.selectRoleAdminHint;
    case 'doctor':
      return copy.selectRoleDoctorHint;
    case 'clinic':
      return copy.selectRoleClinicHint;
    case 'reception':
      return copy.selectRoleReceptionHint;
    case 'staff':
      return copy.selectRoleStaffHint;
    default:
      return copy.selectRolePatientHint;
  }
}
