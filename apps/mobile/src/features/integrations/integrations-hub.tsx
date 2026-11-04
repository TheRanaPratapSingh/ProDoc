import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { View } from 'react-native';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import {
  clearAbhaLink,
  configureIntegrationProvider,
  createLabOrder,
  exportFhirBundle,
  getAbdmReadiness,
  getIntegrationStatus,
  grantIntegrationConsent,
  listLabCatalog,
  listLabOrders,
  listLabReports,
  listPharmacies,
  listPharmacyShares,
  revokeIntegrationConsent,
  simulateAbhaSandbox,
} from '@/services/api/integrations-api';
import { useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

const consentRows = [
  { kind: 'FhirExchange', purpose: 'fhir.export', labelKey: 'integrationsConsentFhir' as const },
  { kind: 'Laboratory', purpose: 'lab.orders', labelKey: 'integrationsConsentLab' as const },
  { kind: 'Pharmacy', purpose: 'pharmacy.share', labelKey: 'integrationsConsentPharmacy' as const },
  { kind: 'Abdm', purpose: 'abdm.exchange', labelKey: 'integrationsConsentAbdm' as const },
];

export function IntegrationsHub() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const queryClient = useQueryClient();
  const permissions = useSessionStore((state) => state.account?.permissions ?? []);
  const canUse = permissions.includes('integrations.use');
  const canManage = permissions.includes('integrations.manage');
  const [error, setError] = useState<string | null>(null);
  const [fhirSummary, setFhirSummary] = useState<string | null>(null);

  const status = useQuery({
    queryKey: ['integrations', 'status'],
    queryFn: getIntegrationStatus,
    enabled: canUse,
  });
  const readiness = useQuery({
    queryKey: ['integrations', 'abdm'],
    queryFn: getAbdmReadiness,
    enabled: canUse,
  });
  const catalog = useQuery({
    queryKey: ['integrations', 'lab-catalog'],
    queryFn: () => listLabCatalog('lab-sandbox'),
    enabled: canUse,
  });
  const orders = useQuery({
    queryKey: ['integrations', 'lab-orders'],
    queryFn: listLabOrders,
    enabled: canUse,
  });
  const reports = useQuery({
    queryKey: ['integrations', 'lab-reports'],
    queryFn: listLabReports,
    enabled: canUse,
  });
  const pharmacies = useQuery({
    queryKey: ['integrations', 'pharmacies'],
    queryFn: () => listPharmacies('pharmacy-sandbox'),
    enabled: canUse,
  });
  const shares = useQuery({
    queryKey: ['integrations', 'pharmacy-shares'],
    queryFn: listPharmacyShares,
    enabled: canUse,
  });

  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: ['integrations'] });
  };

  const grant = useMutation({
    mutationFn: ({ kind, purpose }: { kind: string; purpose: string }) => grantIntegrationConsent(kind, purpose),
    onSuccess: async () => {
      setError(null);
      await invalidate();
    },
    onError: (err) => setError(problemMessage(err, copy.integrationsConsentGrant)),
  });

  const revoke = useMutation({
    mutationFn: ({ kind, purpose }: { kind: string; purpose: string }) => revokeIntegrationConsent(kind, purpose),
    onSuccess: async () => {
      setError(null);
      await invalidate();
    },
    onError: (err) => setError(problemMessage(err, copy.integrationsConsentRevoke)),
  });

  const abha = useMutation({
    mutationFn: () => simulateAbhaSandbox(),
    onSuccess: async () => {
      setError(null);
      await invalidate();
    },
    onError: (err) => setError(problemMessage(err, copy.integrationsAbhaSimulate)),
  });

  const clearAbha = useMutation({
    mutationFn: clearAbhaLink,
    onSuccess: async () => {
      setError(null);
      await invalidate();
    },
    onError: (err) => setError(problemMessage(err, copy.integrationsAbhaClear)),
  });

  const fhir = useMutation({
    mutationFn: () => exportFhirBundle(),
    onSuccess: async (bundle) => {
      setError(null);
      setFhirSummary(`${bundle.entry.length} resources · ${bundle.disclaimer}`);
      await invalidate();
    },
    onError: (err) => setError(problemMessage(err, copy.integrationsFhirExport)),
  });

  const labOrder = useMutation({
    mutationFn: (catalogCode: string) =>
      createLabOrder({
        providerCode: 'lab-sandbox',
        catalogCode,
        idempotencyKey: `lab-${catalogCode}-${Date.now()}`,
      }),
    onSuccess: async () => {
      setError(null);
      await invalidate();
    },
    onError: (err) => setError(problemMessage(err, copy.integrationsLabOrder)),
  });

  const configure = useMutation({
    mutationFn: (providerId: string) =>
      configureIntegrationProvider(providerId, { mode: 'Sandbox', isActive: true }),
    onSuccess: async () => {
      setError(null);
      await invalidate();
    },
    onError: (err) => setError(problemMessage(err, copy.integrationsAdminConfigure)),
  });

  const consentMap = useMemo(() => {
    const map = new Map<string, string>();
    for (const consent of status.data?.consents ?? []) {
      map.set(`${consent.providerKind}:${consent.purpose}`, consent.status);
    }
    return map;
  }, [status.data?.consents]);

  if (!canUse) {
    return (
      <AuthScaffold>
        <AppText variant="title">{copy.integrationsTitle}</AppText>
        <AppText variant="muted">{copy.integrationsNoAccess}</AppText>
      </AuthScaffold>
    );
  }

  return (
    <AuthScaffold>
      <View style={{ gap: spacing.md }}>
        <AppText variant="title">{copy.integrationsTitle}</AppText>
        <AppText variant="muted">{copy.integrationsBody}</AppText>
        {error ? <AppText style={{ color: theme.colors.error }}>{error}</AppText> : null}

        <Card>
          <AppText variant="label">{copy.integrationsStatus}</AppText>
          <AppText variant="muted">
            {copy.integrationsOpenLabs}: {status.data?.openLabOrders ?? 0} · {copy.integrationsActiveShares}:{' '}
            {status.data?.activePharmacyShares ?? 0}
          </AppText>
          {(status.data?.providers ?? []).map((provider) => (
            <View key={provider.id} style={{ gap: spacing.xs }}>
              <AppText>
                {provider.displayName} · {provider.mode} · {provider.isActive ? copy.integrationsActive : copy.integrationsInactive}
              </AppText>
              {canManage ? (
                <Button
                  label={copy.integrationsAdminConfigure}
                  variant="secondary"
                  loading={configure.isPending}
                  onPress={() => configure.mutate(provider.id)}
                />
              ) : null}
            </View>
          ))}
        </Card>

        <Card>
          <AppText variant="label">{copy.integrationsConsent}</AppText>
          {consentRows.map((row) => {
            const statusValue = consentMap.get(`${row.kind}:${row.purpose}`) ?? 'None';
            return (
              <View key={row.purpose} style={{ gap: spacing.xs }}>
                <AppText>
                  {copy[row.labelKey]} · {statusValue}
                </AppText>
                <View style={{ flexDirection: 'row', gap: spacing.sm, flexWrap: 'wrap' }}>
                  <Button
                    label={copy.integrationsConsentGrant}
                    variant="secondary"
                    loading={grant.isPending}
                    onPress={() => grant.mutate({ kind: row.kind, purpose: row.purpose })}
                  />
                  <Button
                    label={copy.integrationsConsentRevoke}
                    variant="secondary"
                    loading={revoke.isPending}
                    onPress={() => revoke.mutate({ kind: row.kind, purpose: row.purpose })}
                  />
                </View>
              </View>
            );
          })}
        </Card>

        <Card>
          <AppText variant="label">{copy.integrationsAbdm}</AppText>
          <AppText variant="muted">{readiness.data?.message ?? copy.loading}</AppText>
          <AppText variant="muted">
            {copy.integrationsAbhaStatus}: {status.data?.abha.status ?? '—'} ·{' '}
            {status.data?.abha.productionConnected ? copy.integrationsConnected : copy.integrationsNotConnected}
          </AppText>
          {status.data?.abha.sandboxHandle ? (
            <AppText variant="muted">{status.data.abha.sandboxHandle}</AppText>
          ) : null}
          <AppText variant="muted">{status.data?.abha.notes}</AppText>
          <Button
            label={copy.integrationsAbhaSimulate}
            variant="secondary"
            loading={abha.isPending}
            onPress={() => abha.mutate()}
          />
          <Button
            label={copy.integrationsAbhaClear}
            variant="secondary"
            loading={clearAbha.isPending}
            onPress={() => clearAbha.mutate()}
          />
        </Card>

        <Card>
          <AppText variant="label">{copy.integrationsFhir}</AppText>
          <Button
            label={copy.integrationsFhirExport}
            loading={fhir.isPending}
            onPress={() => fhir.mutate()}
          />
          {fhirSummary ? <AppText variant="muted">{fhirSummary}</AppText> : null}
        </Card>

        <Card>
          <AppText variant="label">{copy.integrationsLabs}</AppText>
          {(catalog.data ?? []).map((item) => (
            <View key={item.code} style={{ gap: spacing.xs }}>
              <AppText>
                {item.name} ({item.code}) · {item.turnaround}
              </AppText>
              <Button
                label={copy.integrationsLabOrder}
                variant="secondary"
                loading={labOrder.isPending}
                onPress={() => labOrder.mutate(item.code)}
              />
            </View>
          ))}
          {(orders.data ?? []).slice(0, 5).map((order) => (
            <AppText key={order.id} variant="muted">
              {order.testName} · {order.status}
              {order.resultsArePreliminary ? ` · ${copy.integrationsPreliminary}` : ''}
            </AppText>
          ))}
          {(reports.data ?? []).slice(0, 5).map((report) => (
            <AppText key={report.id} variant="muted">
              {report.summary} · {report.disclaimer}
            </AppText>
          ))}
        </Card>

        <Card>
          <AppText variant="label">{copy.integrationsPharmacy}</AppText>
          <AppText variant="muted">{copy.integrationsPharmacyBody}</AppText>
          {(pharmacies.data ?? []).map((pharmacy) => (
            <AppText key={pharmacy.code} variant="muted">
              {pharmacy.name} · {pharmacy.city}
            </AppText>
          ))}
          {(shares.data ?? []).slice(0, 5).map((share) => (
            <AppText key={share.id} variant="muted">
              {share.pharmacyName} · {share.status}
            </AppText>
          ))}
        </Card>
      </View>
    </AuthScaffold>
  );
}
