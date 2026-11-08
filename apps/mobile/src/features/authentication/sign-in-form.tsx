import { useMutation } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useForm } from 'react-hook-form';
import { Pressable, useWindowDimensions, View } from 'react-native';
import Animated, { FadeInDown } from 'react-native-reanimated';

import { HealthcareHero } from '@/components/ui/healthcare-hero';
import { ProdocLogo } from '@/components/ui/prodoc-logo';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { phoneSchema, type PhoneFormValues } from '@/schemas/auth';
import { requestOtp } from '@/services/api/auth-api';
import { useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';
import { notifySuccess } from '@/stores/feedback-store';
import { normalizeIndianMobile } from '@/utils/phone';
import { notifyApiError, problemMessage } from '@/utils/problem';

import { LoginPhoneField } from './login-phone-field';
import { LoginPrimaryButton } from './login-primary-button';

export function SignInForm() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const { height: windowHeight } = useWindowDimensions();
  const heroHeight = windowHeight < 720 ? 92 : windowHeight < 820 ? 108 : 116;
  const setPending = useSessionStore((state) => state.setPending);
  const form = useForm<PhoneFormValues>({ defaultValues: { phoneNumber: '' } });
  const mutation = useMutation({
    mutationFn: (phoneNumber: string) => requestOtp(phoneNumber, 'patient'),
    onSuccess: (result, phoneNumber) => {
      setPending({
        phoneNumber,
        developmentCode: result.developmentCode,
        purpose: result.purpose?.toLowerCase() === 'adminsignin' ? 'admin' : 'patient',
        expiresAtUtc: result.expiresAtUtc,
      });
      notifySuccess(copy.otpSentSuccess);
      router.push('/verify');
    },
    onError: (error) => {
      notifyApiError(error, copy.phoneRequestFailed);
    },
  });

  const submit = form.handleSubmit((values) => {
    if (mutation.isPending) {
      return;
    }

    const parsed = phoneSchema.safeParse(values);
    if (!parsed.success) {
      form.setError('phoneNumber', { message: parsed.error.issues[0]?.message });
      return;
    }

    const phoneNumber = normalizeIndianMobile(parsed.data.phoneNumber);
    if (!phoneNumber) {
      form.setError('phoneNumber', { message: 'Enter a valid Indian mobile number.' });
      return;
    }

    mutation.mutate(phoneNumber);
  });

  const fieldError =
    form.formState.errors.phoneNumber?.message ??
    (mutation.error ? problemMessage(mutation.error, copy.phoneRequestFailed) : undefined);

  return (
    <View style={{ width: '100%', maxWidth: 400, alignSelf: 'center', gap: spacing.md }}>
      <Animated.View
        entering={theme.reduceMotion ? undefined : FadeInDown.duration(240)}
        style={{ alignItems: 'center', gap: spacing.sm }}>
        <ProdocLogo size={40} />
        <HealthcareHero height={heroHeight} variant="illustration" />
      </Animated.View>

      <Animated.View entering={theme.reduceMotion ? undefined : FadeInDown.delay(60).duration(260)}>
        <View
          style={{
            backgroundColor: '#FFFFFF',
            borderRadius: radius.xl,
            borderWidth: 1,
            borderColor: '#E2E8F0',
            paddingHorizontal: spacing.lg,
            paddingTop: spacing.md + 2,
            paddingBottom: spacing.sm + 2,
            gap: spacing.md,
            // RN web + native friendly elevation
            shadowColor: '#14243D',
            shadowOffset: { width: 0, height: 6 },
            shadowOpacity: 0.06,
            shadowRadius: 14,
            elevation: 2,
          }}>
          <View style={{ gap: 6, alignItems: 'center', paddingBottom: 2 }}>
            <AppText
              variant="title"
              style={{
                color: '#14243D',
                fontSize: 22,
                lineHeight: 28,
                textAlign: 'center',
                letterSpacing: -0.3,
                fontWeight: '700',
              }}>
              {copy.loginHeadline}
            </AppText>
            <AppText
              variant="muted"
              style={{
                color: '#64748B',
                fontSize: 13,
                lineHeight: 18,
                textAlign: 'center',
                maxWidth: 280,
              }}>
              {copy.loginSubtitle}
            </AppText>
          </View>

          <LoginPhoneField
            label={copy.phoneLabel}
            placeholder={copy.phonePlaceholder}
            value={form.watch('phoneNumber')}
            onChangeText={(value) => {
              form.clearErrors('phoneNumber');
              mutation.reset();
              form.setValue('phoneNumber', value, { shouldDirty: true });
            }}
            error={fieldError}
          />

          <LoginPrimaryButton
            label={copy.continue}
            loading={mutation.isPending}
            disabled={mutation.isPending}
            onPress={() => void submit()}
          />

          <Pressable
            accessibilityRole="link"
            accessibilityLabel={copy.adminLoginLink}
            onPress={() => router.push('/admin-sign-in' as Href)}
            hitSlop={10}
            style={{ paddingTop: 2, paddingBottom: spacing.xs, alignItems: 'center' }}>
            <AppText
              variant="caption"
              style={{
                color: theme.colors.primary,
                fontSize: 13,
                lineHeight: 18,
                textAlign: 'center',
                fontWeight: '600',
              }}>
              {copy.adminLoginLink}
            </AppText>
          </Pressable>
        </View>
      </Animated.View>
    </View>
  );
}
