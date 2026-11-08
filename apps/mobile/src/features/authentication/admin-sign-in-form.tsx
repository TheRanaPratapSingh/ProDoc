import { useMutation } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { Pressable, useWindowDimensions, View } from 'react-native';
import Animated, { FadeInDown } from 'react-native-reanimated';

import { AdminSecurityHero } from '@/components/ui/admin-security-hero';
import { ProdocLogo } from '@/components/ui/prodoc-logo';
import { AppText } from '@/components/ui/text';
import {
  SUPER_ADMIN_MOBILE_DIGITS,
  SUPER_ADMIN_MOBILE_DISPLAY,
  SUPER_ADMIN_MOBILE_E164,
} from '@/config/super-admin';
import { useCopy } from '@/i18n/copy';
import { requestOtp } from '@/services/api/auth-api';
import { useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';
import { notifySuccess } from '@/stores/feedback-store';
import { notifyApiError, problemMessage } from '@/utils/problem';

import { LoginPhoneField } from './login-phone-field';
import { LoginPrimaryButton } from './login-primary-button';

export function AdminSignInForm() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const { height: windowHeight } = useWindowDimensions();
  const heroHeight = windowHeight < 720 ? 96 : 112;
  const setPending = useSessionStore((state) => state.setPending);

  const mutation = useMutation({
    mutationFn: () => requestOtp(SUPER_ADMIN_MOBILE_E164, 'admin'),
    onSuccess: (result) => {
      setPending({
        phoneNumber: SUPER_ADMIN_MOBILE_E164,
        developmentCode: result.developmentCode,
        purpose: 'admin',
        expiresAtUtc: result.expiresAtUtc,
      });
      notifySuccess(copy.otpSentSuccess);
      router.push('/verify');
    },
    onError: (error) => {
      notifyApiError(error, copy.adminLoginFailed);
    },
  });

  const fieldError = mutation.error ? problemMessage(mutation.error, copy.adminLoginFailed) : undefined;

  return (
    <View style={{ width: '100%', maxWidth: 400, alignSelf: 'center', gap: spacing.lg }}>
      <Animated.View
        entering={theme.reduceMotion ? undefined : FadeInDown.duration(240)}
        style={{ alignItems: 'center', gap: spacing.md }}>
        <ProdocLogo size={40} />
        <AdminSecurityHero height={heroHeight} />
      </Animated.View>

      <Animated.View entering={theme.reduceMotion ? undefined : FadeInDown.delay(60).duration(260)}>
        <View
          style={{
            backgroundColor: '#FFFFFF',
            borderRadius: radius.xl,
            borderWidth: 1,
            borderColor: '#E2E8F0',
            paddingHorizontal: spacing.lg,
            paddingTop: spacing.lg,
            paddingBottom: spacing.md,
            gap: spacing.md,
            shadowColor: '#14243D',
            shadowOffset: { width: 0, height: 8 },
            shadowOpacity: 0.07,
            shadowRadius: 18,
            elevation: 3,
          }}>
          <View style={{ gap: 4 }}>
            <AppText
              variant="title"
              style={{
                color: '#14243D',
                fontSize: 22,
                lineHeight: 28,
                textAlign: 'center',
                letterSpacing: -0.2,
              }}>
              {copy.adminLoginTitle}
            </AppText>
            <AppText
              variant="muted"
              style={{
                color: '#64748B',
                fontSize: 14,
                lineHeight: 20,
                textAlign: 'center',
                marginTop: 2,
              }}>
              {copy.adminLoginSubtitle}
            </AppText>
          </View>

          <LoginPhoneField
            label={copy.phoneLabel}
            value={SUPER_ADMIN_MOBILE_DIGITS}
            editable={false}
            selectTextOnFocus={false}
            caretHidden
            showSoftInputOnFocus={false}
            accessibilityLabel={`${copy.phoneLabel}: ${SUPER_ADMIN_MOBILE_DISPLAY}`}
            accessibilityState={{ disabled: true }}
            error={fieldError}
          />

          <AppText
            variant="caption"
            style={{ color: '#64748B', fontSize: 12, lineHeight: 16, marginTop: -4 }}>
            {copy.adminAuthorizedNumberHint}
          </AppText>

          <LoginPrimaryButton
            label={copy.continue}
            loading={mutation.isPending}
            disabled={mutation.isPending}
            onPress={() => {
              if (!mutation.isPending) {
                mutation.mutate();
              }
            }}
          />

          <Pressable
            accessibilityRole="link"
            accessibilityLabel={copy.patientLoginLink}
            onPress={() => router.replace('/sign-in' as Href)}
            hitSlop={10}
            style={{ paddingVertical: spacing.sm, alignItems: 'center' }}>
            <AppText
              variant="caption"
              style={{
                color: theme.colors.primary,
                fontSize: 13,
                lineHeight: 18,
                textAlign: 'center',
                fontWeight: '600',
              }}>
              {copy.patientLoginLink}
            </AppText>
          </Pressable>
        </View>
      </Animated.View>
    </View>
  );
}
