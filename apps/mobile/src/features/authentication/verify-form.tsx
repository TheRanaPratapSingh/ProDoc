import { useMutation } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { useForm } from 'react-hook-form';
import { Platform, View } from 'react-native';
import Animated, { FadeInDown } from 'react-native-reanimated';

import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { OtpInput } from '@/components/ui/otp-input';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { otpSchema, type OtpFormValues } from '@/schemas/auth';
import { requestOtp, verifyOtp } from '@/services/api/auth-api';
import { persistSession, useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

export function VerifyForm() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const pending = useSessionStore((state) => state.pending);
  const setPending = useSessionStore((state) => state.setPending);
  const form = useForm<OtpFormValues>({ defaultValues: { code: '' } });

  const verify = useMutation({
    mutationFn: (code: string) =>
      verifyOtp(pending?.phoneNumber ?? '', code, Platform.OS === 'web' ? 'prodoc-web' : `prodoc-${Platform.OS}`),
    onSuccess: async (session) => {
      await persistSession(session);
      router.replace('/home');
    },
  });

  const resend = useMutation({
    mutationFn: () => requestOtp(pending?.phoneNumber ?? ''),
    onSuccess: (result) => {
      if (pending) {
        setPending({ phoneNumber: pending.phoneNumber, developmentCode: result.developmentCode });
      }
    },
  });

  const submit = form.handleSubmit((values) => {
    const parsed = otpSchema.safeParse(values);
    if (!parsed.success) {
      form.setError('code', { message: parsed.error.issues[0]?.message });
      return;
    }

    verify.mutate(parsed.data.code);
  });

  return (
    <Animated.View entering={theme.reduceMotion ? undefined : FadeInDown.duration(260)}>
      <Card elevated>
        <View style={{ gap: spacing.sm }}>
          <AppText variant="title">{copy.codeTitle}</AppText>
          <AppText variant="muted">
            {copy.codeBody} {pending?.phoneNumber}
          </AppText>
        </View>
        {pending?.developmentCode ? (
          <Card style={{ backgroundColor: theme.colors.primaryLight, gap: spacing.sm }}>
            <AppText variant="label" style={{ color: theme.colors.primary }}>
              {copy.devCode}
            </AppText>
            <AppText variant="title" style={{ letterSpacing: 4 }}>
              {pending.developmentCode}
            </AppText>
            <AppText variant="muted">{copy.devCodeBody}</AppText>
          </Card>
        ) : null}
        <OtpInput
          label={copy.codeLabel}
          value={form.watch('code')}
          onChange={(value) => {
            form.clearErrors('code');
            verify.reset();
            form.setValue('code', value);
            if (value.length === 6) {
              verify.mutate(value);
            }
          }}
          error={
            form.formState.errors.code?.message ??
            (verify.error ? problemMessage(verify.error, copy.phoneRequestFailed) : undefined)
          }
        />
        <Button
          label={copy.verify}
          loading={verify.isPending}
          disabled={verify.isPending}
          onPress={() => void submit()}
        />
        <Button
          label={copy.resend}
          variant="ghost"
          loading={resend.isPending}
          disabled={resend.isPending || verify.isPending}
          onPress={() => {
            if (pending?.phoneNumber) {
              resend.reset();
              resend.mutate();
            }
          }}
        />
        {resend.error ? (
          <AppText variant="muted" style={{ color: theme.colors.error }}>
            {problemMessage(resend.error, copy.phoneRequestFailed)}
          </AppText>
        ) : null}
      </Card>
    </Animated.View>
  );
}
