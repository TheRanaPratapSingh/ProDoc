import { useEffect, useMemo, useState } from 'react';
import { View } from 'react-native';

import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { OtpInput } from '@/components/ui/otp-input';
import { OtpShield } from '@/components/ui/otp-shield';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';

type OtpVerificationPanelProps = {
  title?: string;
  phoneNumber: string;
  expiresAtUtc?: string | null;
  developmentCode?: string | null;
  verifying?: boolean;
  resending?: boolean;
  error?: string | null;
  onVerify: (code: string) => void;
  onResend: () => void;
  onCancel: () => void;
  onCodeChange?: () => void;
  autoSubmit?: boolean;
};

function formatCountdown(totalSeconds: number) {
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, '0')}`;
}

/** Shared 6-digit OTP verification surface for auth and staff invite flows. */
export function OtpVerificationPanel({
  title,
  phoneNumber,
  expiresAtUtc,
  developmentCode,
  verifying = false,
  resending = false,
  error,
  onVerify,
  onResend,
  onCancel,
  onCodeChange,
  autoSubmit = true,
}: OtpVerificationPanelProps) {
  const copy = useCopy();
  const theme = useProdocTheme();
  const [code, setCode] = useState('');
  const [localError, setLocalError] = useState<string | null>(null);
  const [nowMs, setNowMs] = useState(() => Date.now());
  const [resendReadyAt, setResendReadyAt] = useState(() => Date.now() + 30_000);

  useEffect(() => {
    const timer = setInterval(() => setNowMs(Date.now()), 1000);
    return () => clearInterval(timer);
  }, []);

  useEffect(() => {
    setResendReadyAt(Date.now() + 30_000);
  }, [expiresAtUtc, developmentCode]);

  const expiresInSeconds = useMemo(() => {
    if (!expiresAtUtc) return null;
    return Math.max(0, Math.floor((new Date(expiresAtUtc).getTime() - nowMs) / 1000));
  }, [expiresAtUtc, nowMs]);

  const resendInSeconds = Math.max(0, Math.floor((resendReadyAt - nowMs) / 1000));
  const expired = expiresInSeconds === 0;
  const displayError = localError ?? error ?? undefined;

  const submit = (nextCode = code) => {
    setLocalError(null);
    if (nextCode.length !== 6) {
      setLocalError(copy.otpIncomplete);
      return;
    }
    if (expired) {
      setLocalError(copy.otpExpired);
      return;
    }
    onVerify(nextCode);
  };

  return (
    <Card elevated>
      <OtpShield />
      <View style={{ gap: spacing.sm, alignItems: 'center' }}>
        <AppText variant="title">{title ?? copy.codeTitle}</AppText>
        <AppText variant="muted" style={{ textAlign: 'center' }}>
          {copy.codeBody} {phoneNumber}
        </AppText>
        {expiresInSeconds != null ? (
          <AppText
            variant="caption"
            style={{ color: expired ? theme.colors.error : theme.colors.primary }}>
            {expired ? copy.otpExpired : `${copy.otpExpiresIn} ${formatCountdown(expiresInSeconds)}`}
          </AppText>
        ) : null}
      </View>

      {developmentCode ? (
        <Card style={{ backgroundColor: theme.colors.primaryLight, gap: spacing.sm }}>
          <AppText variant="label" style={{ color: theme.colors.primary }}>
            {copy.devCode}
          </AppText>
          <AppText variant="title" style={{ letterSpacing: 4 }}>
            {developmentCode}
          </AppText>
          <AppText variant="muted">{copy.devCodeBody}</AppText>
        </Card>
      ) : null}

      <OtpInput
        label={copy.codeLabel}
        value={code}
        error={displayError}
        onChange={(value) => {
          setLocalError(null);
          onCodeChange?.();
          setCode(value);
          if (autoSubmit && value.length === 6 && !verifying && !expired) {
            onVerify(value);
          }
        }}
      />

      <Button
        label={copy.verify}
        loading={verifying}
        disabled={verifying || resending || expired}
        onPress={() => submit()}
      />
      <Button
        label={resendInSeconds > 0 ? `${copy.resend} (${resendInSeconds}s)` : copy.resend}
        variant="ghost"
        loading={resending}
        disabled={verifying || resending || resendInSeconds > 0}
        onPress={() => {
          setCode('');
          setLocalError(null);
          onResend();
        }}
      />
      <Button label={copy.cancel} variant="secondary" disabled={verifying || resending} onPress={onCancel} />
    </Card>
  );
}
