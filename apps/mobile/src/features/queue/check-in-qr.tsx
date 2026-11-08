import { useMutation, useQuery } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';
import { Modal, Pressable, View } from 'react-native';
import QRCode from 'react-native-qrcode-svg';
import { useRouter } from 'expo-router';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { StatusBadge } from '@/components/ui/status-badge';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { getAppointment, type AppointmentDetails } from '@/services/api/provider-api';
import { mintCheckInQr } from '@/services/api/queue-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';
import {
  clinicPlaceLabel,
  formatDoctorDisplayName,
  formatVisitDate,
  formatVisitTime,
} from '@/utils/visit-format';

function statusLabel(copy: ReturnType<typeof useCopy>, status: string) {
  if (status === 'Confirmed') return copy.confirmed;
  if (status === 'CheckedIn') return copy.checkedIn;
  if (status === 'Completed') return copy.completedVisit;
  if (status === 'Cancelled') return copy.cancelled;
  return status;
}

function DetailLine({ label, value }: { label: string; value: string }) {
  const theme = useProdocTheme();
  return (
    <View style={{ gap: 2 }}>
      <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
        {label}
      </AppText>
      <AppText style={{ color: theme.colors.text }}>{value}</AppText>
    </View>
  );
}

export function CheckInQr({ appointmentId }: { appointmentId: string }) {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const [token, setToken] = useState<string | null>(null);
  const [expires, setExpires] = useState<string | null>(null);
  const [modalOpen, setModalOpen] = useState(true);

  const appointment = useQuery({
    queryKey: ['appointments', appointmentId],
    queryFn: () => getAppointment(appointmentId),
  });

  const mint = useMutation({
    mutationFn: () => mintCheckInQr(appointmentId),
    onSuccess: (result) => {
      setToken(result.token);
      setExpires(result.expiresAtUtc);
      setModalOpen(true);
    },
  });

  useEffect(() => {
    if (appointment.data?.status === 'Confirmed' && !token && !mint.isPending && !mint.isError) {
      mint.mutate();
    }
    // Auto-mint once when the confirmed visit loads.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [appointment.data?.status, appointmentId]);

  const expired = useMemo(() => {
    if (!expires) return false;
    return new Date(expires).getTime() <= Date.now();
  }, [expires]);

  const visit = appointment.data;
  const doctorName = formatDoctorDisplayName(visit?.doctorName);
  const clinicName = clinicPlaceLabel(visit?.clinicName, visit?.branchName);

  const close = () => {
    setModalOpen(false);
    if (router.canGoBack()) {
      router.back();
    }
  };

  return (
    <AuthScaffold>
      <ScreenHeader title={copy.showCheckInQr} showBack />
      <Card elevated>
        {appointment.isLoading || mint.isPending ? (
          <AppText variant="muted">{copy.loading}</AppText>
        ) : null}
        {appointment.error ? (
          <AppText variant="muted">{problemMessage(appointment.error, copy.showCheckInQr)}</AppText>
        ) : null}
        {mint.error ? <AppText variant="muted">{problemMessage(mint.error, copy.showCheckInQr)}</AppText> : null}
        {visit ? <VisitSummary visit={visit} copy={copy} /> : null}
        <Button
          label={token ? copy.refreshQr : copy.showCheckInQr}
          loading={mint.isPending}
          onPress={() => mint.mutate()}
        />
        {token ? (
          <Button label={copy.showCheckInQr} variant="secondary" onPress={() => setModalOpen(true)} />
        ) : null}
      </Card>

      <Modal visible={modalOpen && !!token} animationType="slide" presentationStyle="pageSheet" onRequestClose={close}>
        <View style={{ flex: 1, backgroundColor: theme.colors.background, padding: spacing.xl, gap: spacing.lg }}>
          <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
            <AppText variant="headline">{copy.showCheckInQr}</AppText>
            <Pressable onPress={close} hitSlop={12}>
              <AppText style={{ color: theme.colors.primary }}>{copy.closeQr}</AppText>
            </Pressable>
          </View>

          <View
            style={{
              alignItems: 'center',
              gap: spacing.md,
              padding: spacing.xl,
              backgroundColor: theme.colors.surface,
              borderRadius: radius.xl,
              borderWidth: 1,
              borderColor: theme.colors.border,
            }}>
            {token ? (
              <QRCode value={token} size={220} color={theme.colors.text} backgroundColor={theme.colors.surface} />
            ) : null}
            <AppText variant="muted" style={{ textAlign: 'center' }}>
              {expired ? copy.qrExpiredHint : copy.qrReadyHint}
            </AppText>
            {expires ? (
              <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                {copy.qrExpiresAt}: {formatVisitTime(expires)} · {formatVisitDate(expires)}
              </AppText>
            ) : null}
          </View>

          <View
            style={{
              backgroundColor: theme.colors.primaryLight,
              borderRadius: radius.lg,
              padding: spacing.lg,
              alignItems: 'center',
              gap: 2,
            }}>
            <AppText variant="caption" style={{ color: theme.colors.primaryPressed, letterSpacing: 0.8 }}>
              {copy.tokenNumber.toUpperCase()}
            </AppText>
            <AppText variant="headline" style={{ color: theme.colors.primaryPressed, fontSize: 28 }}>
              {visit?.tokenLabel ?? '—'}
            </AppText>
          </View>

          <View style={{ gap: spacing.md }}>
            <DetailLine label={copy.doctorLabel} value={doctorName} />
            <DetailLine label={copy.clinicLabel} value={clinicName || '—'} />
            <DetailLine label={copy.patientLabel} value={visit?.patientName || '—'} />
            <DetailLine
              label={copy.appointmentDate}
              value={visit ? formatVisitDate(visit.startsAtUtc, visit.timeZoneId) : '—'}
            />
            <DetailLine
              label={copy.appointmentTime}
              value={visit ? formatVisitTime(visit.startsAtUtc, visit.timeZoneId) : '—'}
            />
            {visit ? (
              <StatusBadge label={statusLabel(copy, visit.status)} tone={visit.status === 'Confirmed' ? 'success' : 'neutral'} />
            ) : null}
          </View>

          <Button
            label={copy.refreshQr}
            loading={mint.isPending}
            variant={expired ? 'primary' : 'secondary'}
            onPress={() => mint.mutate()}
          />
          <Button label={copy.closeQr} variant="secondary" onPress={close} />
        </View>
      </Modal>
    </AuthScaffold>
  );
}

function VisitSummary({ visit, copy }: { visit: AppointmentDetails; copy: ReturnType<typeof useCopy> }) {
  return (
    <View style={{ gap: spacing.sm }}>
      <AppText variant="headline">{formatDoctorDisplayName(visit.doctorName)}</AppText>
      <AppText variant="muted">{clinicPlaceLabel(visit.clinicName, visit.branchName)}</AppText>
      <AppText>
        {formatVisitDate(visit.startsAtUtc, visit.timeZoneId)} · {formatVisitTime(visit.startsAtUtc, visit.timeZoneId)}
      </AppText>
      <AppText>
        {copy.tokenNumber}: {visit.tokenLabel ?? '—'}
      </AppText>
    </View>
  );
}
