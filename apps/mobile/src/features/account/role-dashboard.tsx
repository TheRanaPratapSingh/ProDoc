import { useRouter, type Href } from 'expo-router';
import { Pressable, View } from 'react-native';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { clearSession, useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';
import { needsRoleSelection, type HomeSurface } from '@/utils/home-surface';
import { logout } from '@/services/api/auth-api';

type DashboardLink = {
  label: string;
  href: Href;
  description?: string;
};

type RoleDashboardProps = {
  surface: HomeSurface;
  title: string;
  subtitle: string;
  links: DashboardLink[];
};

/** Shared landing shell for doctor / reception / staff / clinic dashboards. */
export function RoleDashboard({ surface, title, subtitle, links }: RoleDashboardProps) {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const account = useSessionStore((state) => state.account);
  const refreshToken = useSessionStore((state) => state.refreshToken);
  const setActiveSurface = useSessionStore((state) => state.setActiveSurface);
  const multiRole = needsRoleSelection(account);

  return (
    <AuthScaffold>
      <ScreenHeader title={title} showBack={false} />
      <Card style={{ gap: spacing.sm }}>
        <AppText variant="headline">{account?.fullName?.trim() || title}</AppText>
        <AppText variant="muted">{subtitle}</AppText>
        <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
          {account?.phoneNumber}
        </AppText>
      </Card>

      <View style={{ gap: spacing.sm }}>
        {links.map((link) => (
          <Pressable
            key={String(link.href)}
            onPress={() => router.push(link.href)}
            style={{
              borderWidth: 1,
              borderColor: theme.colors.border,
              borderRadius: radius.lg,
              backgroundColor: theme.colors.surface,
              padding: spacing.lg,
              gap: 4,
            }}>
            <AppText variant="label" style={{ color: theme.colors.primary }}>
              {link.label}
            </AppText>
            {link.description ? <AppText variant="muted">{link.description}</AppText> : null}
          </Pressable>
        ))}
      </View>

      <View style={{ gap: spacing.sm, marginTop: spacing.md }}>
        {multiRole ? (
          <Button
            label={copy.switchRole}
            variant="secondary"
            onPress={() => {
              setActiveSurface(null);
              router.replace('/select-role');
            }}
          />
        ) : null}
        <Button
          label={copy.signOut}
          variant="danger"
          onPress={async () => {
            if (refreshToken) {
              try {
                await logout(refreshToken);
              } catch {
                // still clear local session
              }
            }
            await clearSession();
            router.replace(surface === 'admin' ? '/admin-sign-in' : '/sign-in');
          }}
        />
      </View>
    </AuthScaffold>
  );
}
