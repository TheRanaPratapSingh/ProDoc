import * as Clipboard from 'expo-clipboard';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { Linking, Platform, ScrollView, Share, View } from 'react-native';

import { BottomSheet } from '@/components/ui/bottom-sheet';
import { Button } from '@/components/ui/button';
import { IconShare } from '@/components/ui/share-icon';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import {
  createAppointmentShareLink,
  getAppointmentSharePreview,
  revokeAllAppointmentShareLinks,
  type SharedAppointmentSummary,
} from '@/services/api/appointment-share-api';
import type { AppointmentDetails } from '@/services/api/provider-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import {
  appointmentToShareSummary,
  buildAppointmentShareMessage,
  type SharedSummaryLike,
} from '@/utils/appointment-share';
import { clinicPlaceLabel, formatVisitDate } from '@/utils/visit-format';
import { problemMessage } from '@/utils/problem';

function toSharedLike(summary: SharedAppointmentSummary): SharedSummaryLike {
  return {
    bookingReference: summary.bookingReference,
    status: summary.status,
    patientName: summary.patientName,
    doctorName: summary.doctorName,
    specialty: summary.specialty,
    clinicName: summary.clinicName,
    branchName: summary.branchName,
    address: summary.address,
    mapsUrl: summary.mapsUrl,
    startsAtUtc: summary.startsAtUtc,
    endsAtUtc: summary.endsAtUtc,
    timeZoneId: summary.timeZoneId,
    bookingMode: summary.bookingMode,
    tokenLabel: summary.tokenLabel,
    tokenNumber: summary.tokenNumber,
  };
}

export function ShareAppointmentButton({
  onPress,
  label,
  style,
}: {
  onPress: () => void;
  label: string;
  style?: object;
}) {
  const theme = useProdocTheme();
  return (
    <View style={[{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm }, style]}>
      <View
        style={{
          width: 40,
          height: 40,
          borderRadius: 12,
          backgroundColor: theme.colors.primaryLight,
          alignItems: 'center',
          justifyContent: 'center',
        }}>
        <IconShare size={18} color={theme.colors.primary} />
      </View>
      <Button label={label} variant="secondary" onPress={onPress} style={{ flex: 1 }} />
    </View>
  );
}

export function ShareAppointmentSheet({
  visible,
  appointment,
  onClose,
}: {
  visible: boolean;
  appointment: AppointmentDetails | null;
  onClose: () => void;
}) {
  const copy = useCopy();
  const theme = useProdocTheme();
  const queryClient = useQueryClient();
  const [recipientName, setRecipientName] = useState('');
  const [includeLink, setIncludeLink] = useState(false);
  const [shareUrl, setShareUrl] = useState<string | null>(null);
  const [shareId, setShareId] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const preview = useQuery({
    queryKey: ['appointment-share-preview', appointment?.id],
    queryFn: () => getAppointmentSharePreview(appointment!.id),
    enabled: visible && Boolean(appointment?.id),
    staleTime: 30_000,
  });

  const summary = useMemo(() => {
    if (preview.data) {
      return toSharedLike(preview.data);
    }
    if (appointment) {
      return appointmentToShareSummary(appointment);
    }
    return null;
  }, [appointment, preview.data]);

  const message = useMemo(() => {
    if (!summary) return '';
    return buildAppointmentShareMessage(summary, {
      recipientName,
      shareUrl: includeLink ? shareUrl : null,
    });
  }, [summary, recipientName, includeLink, shareUrl]);

  const createLink = useMutation({
    mutationFn: async () => {
      if (!appointment) throw new Error('missing_appointment');
      return createAppointmentShareLink(appointment.id);
    },
    onSuccess: (data) => {
      setIncludeLink(true);
      setShareUrl(data.shareUrl);
      setShareId(data.shareId);
      setFeedback(copy.shareLinkReady);
      setError(null);
      void queryClient.invalidateQueries({ queryKey: ['appointment-share-links', appointment?.id] });
    },
    onError: (err) => setError(problemMessage(err, copy.shareLinkFailed)),
  });

  const revokeLinks = useMutation({
    mutationFn: async () => {
      if (!appointment) throw new Error('missing_appointment');
      await revokeAllAppointmentShareLinks(appointment.id);
    },
    onSuccess: () => {
      setIncludeLink(false);
      setShareUrl(null);
      setShareId(null);
      setFeedback(copy.shareLinkRevoked);
      setError(null);
    },
    onError: (err) => setError(problemMessage(err, copy.shareLinkRevokeFailed)),
  });

  const resetAndClose = () => {
    setRecipientName('');
    setIncludeLink(false);
    setShareUrl(null);
    setShareId(null);
    setFeedback(null);
    setError(null);
    onClose();
  };

  const copyMessage = async () => {
    if (!message.trim()) return;
    await Clipboard.setStringAsync(message);
    setFeedback(copy.shareCopied);
    setError(null);
  };

  const openNativeShare = async () => {
    if (!message.trim()) return;
    try {
      const result = await Share.share(
        Platform.OS === 'ios'
          ? { message, url: includeLink && shareUrl ? shareUrl : undefined }
          : { message },
      );
      if (result.action === Share.sharedAction) {
        setFeedback(copy.shareOpened);
        setError(null);
      }
    } catch {
      setError(copy.shareCancelled);
    }
  };

  const openWhatsApp = async () => {
    if (!message.trim()) return;
    const url = `https://wa.me/?text=${encodeURIComponent(message)}`;
    await Linking.openURL(url);
    setFeedback(copy.shareOpened);
  };

  const openSms = async () => {
    if (!message.trim()) return;
    const separator = Platform.OS === 'ios' ? '&' : '?';
    const url = `sms:${separator}body=${encodeURIComponent(message)}`;
    await Linking.openURL(url);
    setFeedback(copy.shareOpened);
  };

  const openEmail = async () => {
    if (!message.trim() || !summary) return;
    const subject = encodeURIComponent(`ProDoc appointment – ${summary.bookingReference}`);
    const body = encodeURIComponent(message);
    await Linking.openURL(`mailto:?subject=${subject}&body=${body}`);
    setFeedback(copy.shareOpened);
  };

  return (
    <BottomSheet visible={visible} title={copy.shareAppointment} onClose={resetAndClose} maxHeightRatio={0.88}>
      <ScrollView style={{ maxHeight: 520 }} contentContainerStyle={{ gap: spacing.md, paddingBottom: spacing.sm }}>
        {summary ? (
          <View
            style={{
              gap: spacing.xs,
              padding: spacing.md,
              borderRadius: 14,
              borderWidth: 1,
              borderColor: theme.colors.border,
              backgroundColor: theme.colors.primaryLight,
            }}>
            <AppText variant="label">{copy.sharePreview}</AppText>
            <AppText>{summary.patientName}</AppText>
            <AppText variant="muted">{summary.doctorName}</AppText>
            {summary.specialty ? <AppText variant="caption">{summary.specialty}</AppText> : null}
            <AppText variant="muted">{clinicPlaceLabel(summary.clinicName, summary.branchName)}</AppText>
            <AppText variant="muted">
              {formatVisitDate(summary.startsAtUtc, summary.timeZoneId)} · {summary.status}
            </AppText>
            {summary.tokenLabel ? (
              <AppText variant="label">
                {copy.tokenNumber}: {summary.tokenLabel}
              </AppText>
            ) : null}
            {summary.address ? (
              <AppText variant="caption" numberOfLines={3}>
                {summary.address}
              </AppText>
            ) : null}
          </View>
        ) : (
          <AppText variant="muted">{copy.loading}</AppText>
        )}

        <TextField
          label={copy.shareRecipientOptional}
          value={recipientName}
          onChangeText={setRecipientName}
          placeholder={copy.shareRecipientPlaceholder}
        />

        <View style={{ gap: spacing.sm }}>
          <AppText variant="label">{copy.shareSecureLink}</AppText>
          <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
            {copy.shareSecureLinkHint}
          </AppText>
          {includeLink && shareUrl ? (
            <AppText variant="caption" numberOfLines={2} style={{ color: theme.colors.primary }}>
              {shareUrl}
            </AppText>
          ) : null}
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
            <Button
              label={includeLink ? copy.shareLinkReadyShort : copy.createShareLink}
              variant="secondary"
              loading={createLink.isPending}
              onPress={() => createLink.mutate()}
              style={{ flexGrow: 1, minWidth: 140 }}
            />
            {includeLink || shareId ? (
              <Button
                label={copy.revokeShareLink}
                variant="ghost"
                loading={revokeLinks.isPending}
                onPress={() => revokeLinks.mutate()}
                style={{ flexGrow: 1, minWidth: 120 }}
              />
            ) : null}
          </View>
        </View>

        {feedback ? (
          <AppText variant="caption" style={{ color: theme.colors.primary }}>
            {feedback}
          </AppText>
        ) : null}
        {error ? (
          <AppText variant="caption" style={{ color: theme.colors.error }}>
            {error}
          </AppText>
        ) : null}

        <View style={{ gap: spacing.sm }}>
          <Button label={copy.shareViaApps} onPress={() => void openNativeShare()} />
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
            <Button label={copy.shareWhatsApp} variant="secondary" onPress={() => void openWhatsApp()} style={{ flexGrow: 1 }} />
            <Button label={copy.shareSms} variant="secondary" onPress={() => void openSms()} style={{ flexGrow: 1 }} />
            <Button label={copy.shareEmail} variant="secondary" onPress={() => void openEmail()} style={{ flexGrow: 1 }} />
            <Button label={copy.copyDetails} variant="ghost" onPress={() => void copyMessage()} style={{ flexGrow: 1 }} />
          </View>
          <Button label={copy.cancel} variant="ghost" onPress={resetAndClose} />
        </View>
      </ScrollView>
    </BottomSheet>
  );
}
