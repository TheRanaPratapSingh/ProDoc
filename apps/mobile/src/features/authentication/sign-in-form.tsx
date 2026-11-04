import { useMutation } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { useForm } from 'react-hook-form';
import { View } from 'react-native';
import Animated, { FadeInDown } from 'react-native-reanimated';

import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { PhoneInput } from '@/components/ui/phone-input';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { phoneSchema, type PhoneFormValues } from '@/schemas/auth';
import { requestOtp } from '@/services/api/auth-api';
import { apiBaseUrl } from '@/services/api/client';
import { useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { normalizeIndianMobile } from '@/utils/phone';
import { problemMessage } from '@/utils/problem';

export function SignInForm() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const setPending = useSessionStore((state) => state.setPending);
  const form = useForm<PhoneFormValues>({ defaultValues: { phoneNumber: '' } });
  const mutation = useMutation({
    mutationFn: (phoneNumber: string) => requestOtp(phoneNumber),
    onSuccess: (result, phoneNumber) => {
      setPending({ phoneNumber, developmentCode: result.developmentCode });
      router.push('/verify');
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
    <Animated.View entering={theme.reduceMotion ? undefined : FadeInDown.duration(260)}>
      <Card elevated>
        <View style={{ gap: spacing.sm }}>
          <AppText variant="title">{copy.loginHeadline}</AppText>
          <AppText variant="muted">{copy.loginSubtitle}</AppText>
        </View>
        <PhoneInput
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
        {__DEV__ ? (
          <AppText variant="caption">
            {copy.apiEndpointHint}: {apiBaseUrl}
          </AppText>
        ) : null}
        <Button
          label={copy.continue}
          loading={mutation.isPending}
          disabled={mutation.isPending}
          onPress={() => void submit()}
        />
      </Card>
    </Animated.View>
  );
}
