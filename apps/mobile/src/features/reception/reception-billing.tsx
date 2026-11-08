import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, ScrollView, View } from 'react-native';

import { ReceptionClinicSwitcher, useReceptionDeskSelection } from '@/components/reception/clinic-switcher';
import { ReceptionShell } from '@/components/layout/reception-shell';
import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { EmptyState } from '@/components/ui/empty-state';
import { DashboardStatCard } from '@/components/ui/stat-card';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { collectReceptionPayment, getReceptionDashboard, listReceptionPendingPayments } from '@/services/api/reception-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

export function ReceptionBilling() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const queryClient = useQueryClient();
  const { clinicId, branchId } = useReceptionDeskSelection();
  const dashboard = useQuery({
    queryKey: ['reception', 'dashboard', clinicId, branchId],
    queryFn: () => getReceptionDashboard(clinicId, branchId),
    enabled: Boolean(clinicId),
  });
  const pending = useQuery({
    queryKey: ['reception', 'payments', clinicId, branchId],
    queryFn: () => listReceptionPendingPayments(clinicId, branchId),
    enabled: Boolean(clinicId),
  });
  const collect = useMutation({
    mutationFn: (appointmentId: string) => collectReceptionPayment(appointmentId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['reception'] });
      Alert.alert(copy.recordClinicPayment, 'Payment recorded.');
    },
  });

  return (
    <ReceptionShell active="billing">
      <AuthScaffold>
        <ScreenHeader title={copy.receptionBillingTitle} />
        <ScrollView contentContainerStyle={{ gap: spacing.lg, paddingBottom: spacing.xxl }}>
          <ReceptionClinicSwitcher />
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md }}>
            <DashboardStatCard label={copy.receptionPendingPayments} value={String(dashboard.data?.payments.pending ?? 0)} />
            <DashboardStatCard
              label={copy.receptionCollectedToday}
              value={`₹${Math.round(dashboard.data?.payments.collectedToday ?? 0)}`}
            />
          </View>

          {(pending.data ?? []).length === 0 ? <EmptyState title={copy.receptionEmptyPayments} /> : null}
          {collect.error ? (
            <AppText style={{ color: theme.colors.error }}>{problemMessage(collect.error, copy.receptionBillingTitle)}</AppText>
          ) : null}
          {(pending.data ?? []).map((row) => (
            <Card key={row.appointmentId} style={{ gap: spacing.sm }}>
              <AppText variant="label">
                {row.patientName} · {row.reference}
              </AppText>
              <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                {row.doctorName} · {row.paymentStatus} · ₹{row.fee}
              </AppText>
              <Button
                label={copy.recordClinicPayment}
                onPress={() =>
                  Alert.alert(copy.recordClinicPayment, copy.receptionCollectConfirm, [
                    { text: 'Cancel', style: 'cancel' },
                    { text: 'Collect', onPress: () => collect.mutate(row.appointmentId) },
                  ])
                }
                loading={collect.isPending}
              />
            </Card>
          ))}
        </ScrollView>
      </AuthScaffold>
    </ReceptionShell>
  );
}
