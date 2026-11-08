import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { View } from 'react-native';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { FormScreenScroll } from '@/components/layout/keyboard-aware-form';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import { spacing } from '@/theme/tokens';
import {
  createPartnerApp,
  getMyPartnerOrganization,
  getPartnerCatalog,
  getPartnerUsage,
  issuePartnerCredential,
  listPartnerApps,
  registerPartner,
} from '@/services/api/partners-api';
import { useAccountPermissions } from '@/stores/session-store';

export function DeveloperPortal() {
  const copy = useCopy();
  const queryClient = useQueryClient();
  const permissions = useAccountPermissions();
  const canDevelop = permissions.includes('partners.develop');
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [appName, setAppName] = useState('Sandbox App');
  const [issuedSecret, setIssuedSecret] = useState<string | null>(null);

  const org = useQuery({
    queryKey: ['partners', 'me'],
    queryFn: getMyPartnerOrganization,
    enabled: canDevelop,
  });
  const catalog = useQuery({
    queryKey: ['partners', 'catalog'],
    queryFn: getPartnerCatalog,
    enabled: canDevelop,
  });
  const apps = useQuery({
    queryKey: ['partners', 'apps', org.data?.id],
    queryFn: () => listPartnerApps(org.data!.id),
    enabled: canDevelop && !!org.data?.id,
  });

  const register = useMutation({
    mutationFn: () => registerPartner({ name, contactEmail: email }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['partners'] });
    },
  });

  const createApp = useMutation({
    mutationFn: () =>
      createPartnerApp(org.data!.id, {
        name: appName,
        environment: 'Sandbox',
        scopes: catalog.data?.scopes ?? [
          'discovery.read',
          'availability.read',
          'appointments.read',
          'appointments.write',
          'webhooks.manage',
        ],
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['partners', 'apps'] });
    },
  });

  const issue = useMutation({
    mutationFn: (appId: string) => issuePartnerCredential(appId),
    onSuccess: (data) => {
      setIssuedSecret(data.clientSecret);
    },
  });

  if (!canDevelop) {
    return (
      <AuthScaffold>
        <View style={{ gap: spacing.md }}>
          <ScreenHeader title={copy.partnersPortalTitle} showBack />
          <Card>
            <AppText variant="label">{copy.partnersPortalTitle}</AppText>
            <AppText variant="muted">{copy.partnersPortalDenied}</AppText>
          </Card>
        </View>
      </AuthScaffold>
    );
  }

  return (
    <AuthScaffold>
      <FormScreenScroll contentContainerStyle={{ gap: spacing.md, paddingBottom: spacing.xxl }}>
        <ScreenHeader title={copy.partnersPortalTitle} showBack />
      <Card>
        <AppText variant="label">{copy.partnersPortalTitle}</AppText>
        <AppText variant="muted">{copy.partnersPortalBody}</AppText>
      </Card>

      {!org.data ? (
        <Card>
          <AppText variant="label">{copy.partnersRegister}</AppText>
          <TextField label={copy.partnersOrgName} value={name} onChangeText={setName} />
          <TextField label={copy.partnersContactEmail} value={email} onChangeText={setEmail} />
          <Button label={copy.partnersRegister} loading={register.isPending} onPress={() => register.mutate()} />
        </Card>
      ) : (
        <Card>
          <AppText variant="label">{org.data.name}</AppText>
          <AppText variant="muted">
            {org.data.status} · {org.data.contactEmail}
          </AppText>
        </Card>
      )}

      {org.data ? (
        <Card>
          <AppText variant="label">{copy.partnersApps}</AppText>
          <TextField label={copy.partnersAppName} value={appName} onChangeText={setAppName} />
          <Button label={copy.partnersCreateSandboxApp} loading={createApp.isPending} onPress={() => createApp.mutate()} />
          {(apps.data ?? []).map((app) => (
            <View key={app.id} style={{ gap: spacing.sm, marginTop: spacing.md }}>
              <AppText>
                {app.name} · {app.environment}
              </AppText>
              <AppText variant="muted">client_id: {app.clientId}</AppText>
              <AppText variant="muted">scopes: {app.scopes.join(', ')}</AppText>
              <Button
                label={copy.partnersIssueCredential}
                variant="secondary"
                loading={issue.isPending}
                onPress={() => issue.mutate(app.id)}
              />
              <UsagePeek appId={app.id} />
            </View>
          ))}
          {issuedSecret ? (
            <AppText variant="muted">
              {copy.partnersSecretOnce}: {issuedSecret}
            </AppText>
          ) : null}
        </Card>
      ) : null}

      <Card>
        <AppText variant="label">{copy.partnersDocs}</AppText>
        <AppText variant="muted">/api/partner/v1/docs · /oauth/partner/token · Swagger partner</AppText>
        <AppText variant="muted">{copy.partnersSecretWarning}</AppText>
      </Card>
      </FormScreenScroll>
    </AuthScaffold>
  );
}

function UsagePeek({ appId }: { appId: string }) {
  const copy = useCopy();
  const usage = useQuery({
    queryKey: ['partners', 'usage', appId],
    queryFn: () => getPartnerUsage(appId),
  });
  if (!usage.data) {
    return null;
  }
  return (
    <AppText variant="muted">
      {copy.partnersUsageToday}: {usage.data.todayRequests}/{usage.data.dailyQuota}
    </AppText>
  );
}
