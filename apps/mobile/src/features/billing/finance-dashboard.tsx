import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';

import { View } from 'react-native';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { DashboardStatCard } from '@/components/ui/stat-card';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { getAdminFinance, getClinicFinance } from '@/services/api/billing-api';
import { listDeskClinics } from '@/services/api/queue-api';
import { useAccountPermissions } from '@/stores/session-store';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

export function FinanceDashboardScreen() {
  const copy = useCopy();
  const permissions = useAccountPermissions();
  const isAdmin = permissions.includes('commissions.manage') || permissions.includes('admin.access');
  const clinics = useQuery({ queryKey: ['clinics', 'desk'], queryFn: listDeskClinics, enabled: !isAdmin });
  const [clinicId, setClinicId] = useState('');
  const activeClinicId = clinicId || clinics.data?.[0]?.id || '';
  const clinicFinance = useQuery({
    queryKey: ['finance', 'clinic', activeClinicId],
    queryFn: () => getClinicFinance(activeClinicId),
    enabled: Boolean(activeClinicId) && !isAdmin,
  });
  const adminFinance = useQuery({
    queryKey: ['finance', 'admin'],
    queryFn: getAdminFinance,
    enabled: isAdmin,
  });
  const error = clinics.error ?? clinicFinance.error ?? adminFinance.error;

  return (
    <AuthScaffold>
      <ScreenHeader title={copy.financeDashboard} />
      {isAdmin && adminFinance.data ? (
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md }}>
          <DashboardStatCard label={copy.grossBookings} value={`₹${adminFinance.data.grossPlatformBookingValue.toFixed(0)}`} />
          <DashboardStatCard label={copy.onlineCollected} value={`₹${adminFinance.data.onlineCollected.toFixed(0)}`} />
          <DashboardStatCard label={copy.commissionEarned} value={`₹${adminFinance.data.commissionEarned.toFixed(0)}`} />
          <DashboardStatCard label={copy.refunds} value={`₹${adminFinance.data.refundsIssued.toFixed(0)}`} />
          <DashboardStatCard label={copy.pendingPayables} value={`₹${adminFinance.data.pendingClinicPayables.toFixed(0)}`} />
          <DashboardStatCard label={copy.eligibleVisits} value={adminFinance.data.completedEligibleVisits} />
        </View>
      ) : null}
      {!isAdmin ? (
        <>
          {(clinics.data ?? []).map((clinic) => (
            <Button
              key={clinic.id}
              label={clinic.name}
              variant={activeClinicId === clinic.id ? 'primary' : 'secondary'}
              onPress={() => setClinicId(clinic.id)}
            />
          ))}
          {clinicFinance.data ? (
            <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md }}>
              <DashboardStatCard label={copy.grossBookings} value={`₹${clinicFinance.data.grossEligibleFees.toFixed(0)}`} />
              <DashboardStatCard label={copy.onlineCollected} value={`₹${clinicFinance.data.onlineCollected.toFixed(0)}`} />
              <DashboardStatCard label={copy.payAtClinic} value={`₹${clinicFinance.data.payAtClinicCollected.toFixed(0)}`} />
              <DashboardStatCard label={copy.commissionEarned} value={`₹${clinicFinance.data.commissionDeducted.toFixed(0)}`} />
              <DashboardStatCard label={copy.pendingPayables} value={`₹${clinicFinance.data.pendingSettlement.toFixed(0)}`} />
              <DashboardStatCard label={copy.netPayable} value={`₹${clinicFinance.data.netPayable.toFixed(0)}`} />
            </View>
          ) : null}
        </>
      ) : null}
      {error ? <AppText variant="muted">{problemMessage(error, copy.financeDashboard)}</AppText> : null}
    </AuthScaffold>
  );
}
