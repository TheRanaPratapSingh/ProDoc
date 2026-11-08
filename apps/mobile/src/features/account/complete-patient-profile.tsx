import { useMutation } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useEffect, useState } from 'react';
import { Pressable, View } from 'react-native';
import Animated, { FadeInDown } from 'react-native-reanimated';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import { completePatientProfileSchema } from '@/schemas/auth';
import { completePatientProfile } from '@/services/api/auth-api';
import { notifySuccess } from '@/stores/feedback-store';
import { useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { duration, radius, spacing } from '@/theme/tokens';
import { homeHrefForAccount, needsProfileCompletion } from '@/utils/home-surface';
import { problemFieldErrors, problemMessage } from '@/utils/problem';

type GenderOption = 'male' | 'female' | 'other';

export function CompletePatientProfileScreen() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const account = useSessionStore((state) => state.account);
  const accessToken = useSessionStore((state) => state.accessToken);
  const setAccount = useSessionStore((state) => state.setAccount);
  const activeSurface = useSessionStore((state) => state.activeSurface);

  const [fullName, setFullName] = useState(account?.fullName ?? '');
  const [ageYears, setAgeYears] = useState(account?.ageYears != null ? String(account.ageYears) : '');
  const [gender, setGender] = useState<GenderOption | null>(account?.gender ?? null);
  const [errors, setErrors] = useState<{ fullName?: string; ageYears?: string }>({});

  useEffect(() => {
    if (!accessToken) {
      router.replace('/sign-in' as Href);
      return;
    }
    if (account && !needsProfileCompletion(account)) {
      router.replace(homeHrefForAccount(account, activeSurface));
    }
  }, [accessToken, account, activeSurface, router]);

  const save = useMutation({
    mutationFn: () =>
      completePatientProfile({
        fullName: fullName.trim(),
        ageYears: Number(ageYears),
        gender,
        preferredLanguage: account?.preferredLanguage === 'hi' ? 'hi' : 'en',
      }),
    onSuccess: (updated) => {
      setAccount(updated);
      notifySuccess(copy.profileCompleteSuccess);
      router.replace(homeHrefForAccount(updated, activeSurface));
    },
  });

  const submit = () => {
    const parsed = completePatientProfileSchema.safeParse({
      fullName,
      ageYears,
      gender,
    });
    if (!parsed.success) {
      const next: { fullName?: string; ageYears?: string } = {};
      for (const issue of parsed.error.issues) {
        const key = issue.path[0];
        if (key === 'fullName' && !next.fullName) next.fullName = issue.message;
        if (key === 'ageYears' && !next.ageYears) next.ageYears = issue.message;
      }
      setErrors(next);
      return;
    }

    setErrors({});
    if (save.isPending) return;
    save.mutate();
  };

  const genderOptions: { value: GenderOption; label: string }[] = [
    { value: 'male', label: copy.profileGenderMale },
    { value: 'female', label: copy.profileGenderFemale },
    { value: 'other', label: copy.profileGenderOther },
  ];

  return (
    <AuthScaffold>
      <Animated.View
        entering={theme.reduceMotion ? undefined : FadeInDown.duration(duration.enter)}
        style={{ gap: spacing.lg }}>
        <ScreenHeader title={copy.profileCompleteTitle} showBack={false} />
        <AppText variant="muted">{copy.profileCompleteSubtitle}</AppText>

        <Card elevated style={{ gap: spacing.lg }}>
          <View style={{ gap: spacing.xs }}>
            <AppText variant="label">{copy.profileMobileVerified}</AppText>
            <AppText variant="body">{account?.phoneNumber ?? '—'}</AppText>
          </View>

          <TextField
            label={copy.nameLabel}
            required
            value={fullName}
            onChangeText={(next) => {
              setFullName(next);
              if (errors.fullName) setErrors((current) => ({ ...current, fullName: undefined }));
            }}
            autoCapitalize="words"
            placeholder={copy.namePlaceholder}
            error={errors.fullName}
            returnKeyType="next"
          />

          <TextField
            label={copy.profileAgeLabel}
            required
            value={ageYears}
            onChangeText={(next) => {
              setAgeYears(next.replace(/[^\d]/g, '').slice(0, 3));
              if (errors.ageYears) setErrors((current) => ({ ...current, ageYears: undefined }));
            }}
            keyboardType="number-pad"
            placeholder={copy.profileAgePlaceholder}
            error={errors.ageYears}
            returnKeyType="done"
          />

          <View style={{ gap: spacing.sm }}>
            <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm }}>
              <AppText variant="label">{copy.profileGenderLabel}</AppText>
              <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                ({copy.profileGenderOptional})
              </AppText>
            </View>
            <View style={{ flexDirection: 'row', gap: spacing.sm, flexWrap: 'wrap' }}>
              {genderOptions.map((option) => {
                const selected = gender === option.value;
                return (
                  <Pressable
                    key={option.value}
                    accessibilityRole="button"
                    onPress={() => setGender(selected ? null : option.value)}
                    style={{
                      paddingHorizontal: spacing.md,
                      paddingVertical: spacing.sm,
                      borderRadius: radius.pill,
                      borderWidth: 1.5,
                      borderColor: selected ? theme.colors.primary : theme.colors.border,
                      backgroundColor: selected ? theme.colors.primaryLight : theme.colors.surface,
                    }}>
                    <AppText
                      variant="label"
                      style={{ color: selected ? theme.colors.primary : theme.colors.text }}>
                      {option.label}
                    </AppText>
                  </Pressable>
                );
              })}
            </View>
          </View>

          {save.error ? (
            <AppText variant="muted" style={{ color: theme.colors.error }}>
              {Object.values(problemFieldErrors(save.error))[0] ??
                problemMessage(save.error, copy.profileCompleteCta)}
            </AppText>
          ) : null}

          <Button
            label={copy.profileCompleteCta}
            loading={save.isPending}
            disabled={save.isPending}
            onPress={submit}
          />
        </Card>
      </Animated.View>
    </AuthScaffold>
  );
}
