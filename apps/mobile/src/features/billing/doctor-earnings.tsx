import { useQuery, useQueryClient } from '@tanstack/react-query';
import { View } from 'react-native';

import { ClinicSwitcher, useSelectedDoctorClinicId } from '@/components/doctor/clinic-switcher';
import { DoctorShell } from '@/components/layout/doctor-shell';
import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Card } from '@/components/ui/card';
import { DashboardStatCard } from '@/components/ui/stat-card';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { getDoctorEarnings, listDoctorClinics } from '@/services/api/doctor-portal-api';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

export function DoctorEarningsScreen() {
  const copy = useCopy();
  const queryClient = useQueryClient();
  const clinics = useQuery({ queryKey: ['doctor', 'clinics'], queryFn: listDoctorClinics });
  const clinicId = useSelectedDoctorClinicId(clinics.data);
  const earnings = useQuery({
    queryKey: ['doctor', 'earnings', clinicId ?? 'all'],
    queryFn: () => getDoctorEarnings(clinicId),
  });

  return (
    <DoctorShell active="earnings">
      <AuthScaffold>
        <ScreenHeader title={copy.doctorEarningsTitle} />
        <ClinicSwitcher
          clinics={clinics.data ?? []}
          onChanged={() => void queryClient.invalidateQueries({ queryKey: ['doctor', 'earnings'] })}
        />
        {earnings.data && !earnings.data.paymentsEnabled && earnings.data.totalGross <= 0 ? (
          <AppText variant="muted">{copy.doctorEarningsEmpty}</AppText>
        ) : null}
        {earnings.data ? (
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md }}>
            <DashboardStatCard label={copy.doctorEarningsGross} value={`₹${earnings.data.totalGross.toFixed(0)}`} />
            <DashboardStatCard label={copy.doctorEarningsToday} value={`₹${earnings.data.todayGross.toFixed(0)}`} />
            <DashboardStatCard label={copy.doctorEarningsMonth} value={`₹${earnings.data.monthGross.toFixed(0)}`} />
            <DashboardStatCard label={copy.doctorEarningsCommission} value={`₹${earnings.data.commissionDeducted.toFixed(0)}`} />
            <DashboardStatCard label={copy.doctorEarningsNet} value={`₹${earnings.data.netPayable.toFixed(0)}`} />
            <DashboardStatCard label={copy.doctorEarningsPending} value={`₹${earnings.data.pendingSettlement.toFixed(0)}`} />
          </View>
        ) : null}
        {(earnings.data?.recentCommissions ?? []).slice(0, 8).map((row) => (
          <Card key={row.id} style={{ gap: spacing.sm }}>
            <AppText variant="label">₹{row.consultationFee.toFixed(0)}</AppText>
            <AppText variant="muted">
              {copy.doctorEarningsCommission}: ₹{row.commissionAmount.toFixed(0)} · {copy.doctorEarningsNet}: ₹
              {row.clinicPayableAmount.toFixed(0)}
            </AppText>
            <AppText variant="caption">{new Date(row.capturedAtUtc).toLocaleString()}</AppText>
          </Card>
        ))}
        {earnings.error ? <AppText variant="muted">{problemMessage(earnings.error, copy.doctorEarningsTitle)}</AppText> : null}
      </AuthScaffold>
    </DoctorShell>
  );
}
