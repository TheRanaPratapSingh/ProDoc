import { useMutation } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { Platform } from 'react-native';
import Animated, { FadeInDown } from 'react-native-reanimated';

import { OtpVerificationPanel } from '@/components/ui/otp-verification-panel';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { otpSchema } from '@/schemas/auth';
import { requestOtp, verifyOtp } from '@/services/api/auth-api';
import { clearPendingReferralCode, readPendingReferralCode } from '@/services/referral-intake';
import { notifySuccess } from '@/stores/feedback-store';
import { persistSession, useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { homeHrefForAccount } from '@/utils/home-surface';
import { classifyApiError, notifyApiError, problemCode, problemMessage } from '@/utils/problem';

export function VerifyForm() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const pending = useSessionStore((state) => state.pending);
  const setPending = useSessionStore((state) => state.setPending);
  const setActiveSurface = useSessionStore((state) => state.setActiveSurface);
  const purpose = pending?.purpose ?? 'patient';
  const [inlineError, setInlineError] = useState<string | null>(null);

  const verify = useMutation({
    mutationFn: async (code: string) => {
      const parsed = otpSchema.safeParse({ code });
      if (!parsed.success) {
        throw new Error(parsed.error.issues[0]?.message ?? copy.otpIncomplete);
      }
      const referralCode = purpose === 'patient' ? await readPendingReferralCode() : null;
      return verifyOtp(
        pending?.phoneNumber ?? '',
        parsed.data.code,
        Platform.OS === 'web' ? 'prodoc-web' : `prodoc-${Platform.OS}`,
        purpose,
        referralCode,
      );
    },
    onSuccess: async (session) => {
      setInlineError(null);
      notifySuccess(copy.otpVerifiedSuccess);
      await clearPendingReferralCode();
      // Clear any previous role choice so multi-role accounts re-select when needed.
      setActiveSurface(null);
      await persistSession(session, null);
      router.replace(homeHrefForAccount(session.account, null));
    },
    onError: (error) => {
      const kind = classifyApiError(error);
      const code = problemCode(error);
      const message = problemMessage(error, copy.otpVerifyFailed);
      setInlineError(message);
      // Validation/auth OTP failures stay inline — avoid crash toasts for expected wrong codes.
      if (kind === 'network' || kind === 'timeout' || kind === 'server') {
        notifyApiError(error, copy.otpVerifyFailed);
        return;
      }
      if (code === 'otp_expired' || code === 'otp_attempts_exceeded' || code === 'otp_used') {
        // Keep inline; resend remains available in the panel.
        return;
      }
    },
  });

  const resend = useMutation({
    mutationFn: () => requestOtp(pending?.phoneNumber ?? '', purpose),
    onSuccess: (result) => {
      setInlineError(null);
      if (pending) {
        setPending({
          phoneNumber: pending.phoneNumber,
          developmentCode: result.developmentCode,
          purpose: pending.purpose,
          expiresAtUtc: result.expiresAtUtc,
        });
      }
      notifySuccess(copy.otpResentSuccess);
    },
    onError: (error) => {
      const message = problemMessage(error, copy.otpResendFailed);
      setInlineError(message);
      if (classifyApiError(error) === 'network' || classifyApiError(error) === 'server') {
        notifyApiError(error, copy.otpResendFailed);
      }
    },
  });

  if (!pending?.phoneNumber) {
    return (
      <AppText variant="muted">{copy.phoneRequestFailed}</AppText>
    );
  }

  const panelError =
    inlineError ??
    (verify.error instanceof Error && !('isAxiosError' in verify.error)
      ? verify.error.message
      : null);

  return (
    <Animated.View entering={theme.reduceMotion ? undefined : FadeInDown.duration(260)}>
      <OtpVerificationPanel
        phoneNumber={pending.phoneNumber}
        expiresAtUtc={pending.expiresAtUtc}
        developmentCode={pending.developmentCode}
        verifying={verify.isPending}
        resending={resend.isPending}
        error={panelError}
        onCodeChange={() => {
          if (inlineError) setInlineError(null);
          if (verify.isError) verify.reset();
          if (resend.isError) resend.reset();
        }}
        onVerify={(code) => {
          if (verify.isPending) {
            return;
          }
          setInlineError(null);
          verify.reset();
          verify.mutate(code);
        }}
        onResend={() => {
          if (resend.isPending) {
            return;
          }
          setInlineError(null);
          resend.reset();
          verify.reset();
          resend.mutate();
        }}
        onCancel={() => {
          setPending(null);
          router.replace(purpose === 'admin' ? '/admin-sign-in' : '/');
        }}
      />
    </Animated.View>
  );
}
