import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { ScrollView, useWindowDimensions, View } from 'react-native';

import { AppShell } from '@/components/layout/app-shell';
import { Screen } from '@/components/layout/auth-scaffold';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Segmented } from '@/components/ui/segmented';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import { profileSchema, type ProfileFormValues } from '@/schemas/auth';
import { getAccount, logout, requestDeletion, updateProfile } from '@/services/api/auth-api';
import { getNotificationPreferences, updateNotificationPreferences } from '@/services/api/automation-api';
import type { LanguageCode } from '@/services/api/types';
import { clearSession, useSessionStore } from '@/stores/session-store';
import { useUiStore, type ColorPreference } from '@/stores/ui-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { breakpoint, spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

export function AccountHome() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { width } = useWindowDimensions();
  const account = useSessionStore((state) => state.account);
  const refreshToken = useSessionStore((state) => state.refreshToken);
  const setAccount = useSessionStore((state) => state.setAccount);
  const colorScheme = useUiStore((state) => state.colorScheme);
  const setColorScheme = useUiStore((state) => state.setColorScheme);
  const setLanguage = useUiStore((state) => state.setLanguage);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const form = useForm<ProfileFormValues>({
    values: { fullName: account?.fullName ?? '' },
  });

  const accessToken = useSessionStore((state) => state.accessToken);
  const profileQuery = useQuery({
    queryKey: ['account', 'me'],
    queryFn: getAccount,
    enabled: Boolean(accessToken),
  });

  const prefsQuery = useQuery({
    queryKey: ['account', 'notification-preferences'],
    queryFn: getNotificationPreferences,
    enabled: Boolean(accessToken),
  });

  useEffect(() => {
    if (profileQuery.data) {
      setAccount(profileQuery.data);
    }
  }, [profileQuery.data, setAccount]);

  const save = useMutation({
    mutationFn: (values: { fullName: string; preferredLanguage: LanguageCode }) =>
      updateProfile(values.fullName, values.preferredLanguage),
    onSuccess: async (next) => {
      setAccount(next);
      setLanguage(next.preferredLanguage);
      await queryClient.invalidateQueries({ queryKey: ['account', 'me'] });
    },
  });

  const signOut = useMutation({
    mutationFn: async () => {
      if (refreshToken) {
        await logout(refreshToken);
      }
    },
    onSettled: async () => {
      await clearSession();
      queryClient.clear();
      router.replace('/sign-in');
    },
  });

  const deletion = useMutation({
    mutationFn: requestDeletion,
    onSuccess: async () => {
      await clearSession();
      queryClient.clear();
      router.replace('/sign-in');
    },
  });

  const submit = form.handleSubmit((values) => {
    const parsed = profileSchema.safeParse(values);
    if (!parsed.success) {
      form.setError('fullName', { message: parsed.error.issues[0]?.message });
      return;
    }

    save.mutate({
      fullName: parsed.data.fullName,
      preferredLanguage: account?.preferredLanguage ?? 'en',
    });
  });

  const current = profileQuery.data ?? account;
  const content = (
    <View style={{ width: '100%', maxWidth: width >= breakpoint.wide ? 720 : 480, alignSelf: 'center', gap: spacing.xl }}>
      <View style={{ gap: spacing.sm }}>
        <AppText variant="caption" style={{ color: theme.colors.primary }}>
          {copy.brand}
        </AppText>
        <AppText variant="title">{current?.fullName || copy.welcome}</AppText>
        <AppText variant="muted">{current?.phoneNumber}</AppText>
      </View>
      <Card elevated style={{ gap: spacing.md }}>
        <AppText variant="headline">{copy.loginHeadline}</AppText>
        <AppText variant="muted">{copy.loginSubtitle}</AppText>
        <Button label={copy.findDoctor} onPress={() => router.push('/doctors' as Href)} />
      </Card>

      {current?.permissions.includes('appointments.book.self') ? (
        <Card elevated>
          <AppText variant="label">{copy.myVisits}</AppText>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
            <Button label={copy.myVisits} variant="secondary" onPress={() => router.push('/visits' as Href)} style={{ flexGrow: 1 }} />
            <Button label={copy.myQueue} variant="secondary" onPress={() => router.push('/queue' as Href)} style={{ flexGrow: 1 }} />
            <Button label={copy.healthRecords} variant="secondary" onPress={() => router.push('/health-records' as Href)} style={{ flexGrow: 1 }} />
          </View>
        </Card>
      ) : null}

      <Card elevated>
        <AppText variant="label">{copy.homeTitle}</AppText>
        {(
          [
            current?.permissions.includes('assistant.use') && { label: copy.assistantTitle, href: '/assistant' },
            (current?.permissions.includes('provider.onboard') || current?.permissions.includes('org.access')) && {
              label: copy.orgTitle,
              href: '/organization',
            },
            current?.permissions.includes('growth.use') && { label: copy.growthTitle, href: '/rewards' },
            current?.permissions.includes('integrations.use') && {
              label: copy.integrationsTitle,
              href: '/integrations',
            },
            current?.permissions.includes('partners.develop') && {
              label: copy.partnersPortalTitle,
              href: '/partners',
            },
            current?.permissions.includes('review.create') && { label: copy.reviewWriteTitle, href: '/write-review' },
            current?.permissions.includes('review.respond') && {
              label: copy.reviewInboxTitle,
              href: '/review-inbox',
            },
            current?.permissions.some((permission) =>
              ['appointments.manage.assigned', 'clinic.manage', 'appointments.book.walkin'].includes(permission),
            ) && { label: copy.practiceVisits, href: '/practice' },
            current?.permissions.includes('appointments.manage.assigned') && {
              label: copy.doctorQueue,
              href: '/doctor-queue',
            },
            current?.permissions.includes('queue.operate') && { label: copy.receptionDesk, href: '/reception' },
            (current?.permissions.includes('clinic.manage') || current?.permissions.includes('commissions.manage')) && {
              label: copy.financeDashboard,
              href: '/finance',
            },
            current?.permissions.includes('admin.access') && { label: copy.adminCenter, href: '/admin' },
            current?.permissions.includes('admin.access') && { label: copy.appointmentActivity, href: '/activity' },
            current?.permissions.includes('admin.access') && { label: copy.queueMonitor, href: '/queue-monitor' },
            { label: copy.helpCenterTitle, href: '/help' },
            { label: copy.supportCenter, href: '/support' },
            current?.permissions.includes('admin.support') && { label: copy.agentDeskTitle, href: '/agent-desk' },
            current?.permissions.includes('provider.onboard') && { label: copy.doctorProfile, href: '/doctor' },
            current?.permissions.includes('provider.onboard') && { label: copy.clinicProfile, href: '/clinic' },
            current?.permissions.includes('provider.onboard') && { label: copy.schedules, href: '/schedule' },
            current?.permissions.includes('verification.review') && { label: copy.reviews, href: '/reviews' },
          ] as const
        )
          .filter(Boolean)
          .map((item) =>
            item ? (
              <Button
                key={`${item.label}-${item.href}`}
                label={item.label}
                variant="secondary"
                onPress={() => router.push(item.href as Href)}
              />
            ) : null,
          )}
      </Card>
      <Card elevated>
        <AppText variant="label">{copy.homeTitle}</AppText>
        <TextField
          label={copy.nameLabel}
          placeholder={copy.namePlaceholder}
          value={form.watch('fullName')}
          onChangeText={(value) => form.setValue('fullName', value)}
          error={form.formState.errors.fullName?.message ?? (save.error ? problemMessage(save.error, copy.save) : undefined)}
        />
        <Button label={copy.save} loading={save.isPending} onPress={() => void submit()} />
      </Card>
      {prefsQuery.data ? (
        <Card>
          <AppText variant="label">{copy.notificationPrefs}</AppText>
          {(
            [
              ['inAppEnabled', copy.prefInApp],
              ['emailEnabled', copy.prefEmail],
              ['smsEnabled', copy.prefSms],
              ['pushEnabled', copy.prefPush],
              ['marketingEnabled', copy.prefMarketing],
              ['appointmentRemindersEnabled', copy.prefReminders],
              ['queueUpdatesEnabled', copy.prefQueue],
            ] as const
          ).map(([key, label]) => (
            <Button
              key={key}
              label={`${label}: ${prefsQuery.data![key] ? 'ON' : 'OFF'}`}
              variant="secondary"
              onPress={async () => {
                const next = { ...prefsQuery.data! };
                next[key] = !next[key];
                const { updatedAtUtc: _, ...body } = next;
                await updateNotificationPreferences(body);
                await queryClient.invalidateQueries({ queryKey: ['account', 'notification-preferences'] });
              }}
            />
          ))}
        </Card>
      ) : null}
      <Card elevated>
        <AppText variant="label">{copy.language}</AppText>
        <Segmented
          value={current?.preferredLanguage ?? 'en'}
          options={[
            { value: 'en', label: copy.english },
            { value: 'hi', label: copy.hindi },
          ]}
          onChange={(language) => {
            setLanguage(language);
            save.mutate({ fullName: form.getValues('fullName'), preferredLanguage: language });
          }}
        />
        <AppText variant="label">{copy.appearance}</AppText>
        <Segmented<ColorPreference>
          value={colorScheme}
          options={[
            { value: 'light', label: copy.light },
            { value: 'dark', label: copy.dark },
            { value: 'system', label: copy.system },
          ]}
          onChange={setColorScheme}
        />
      </Card>
      <Card elevated>
        <Button label={copy.signOut} variant="secondary" loading={signOut.isPending} onPress={() => signOut.mutate()} />
        <Button label={copy.delete} variant="danger" onPress={() => setConfirmDelete(true)} />
        {deletion.error ? <AppText variant="muted">{problemMessage(deletion.error, copy.delete)}</AppText> : null}
      </Card>
      <ConfirmDialog
        visible={confirmDelete}
        title={copy.delete}
        body={copy.deleteBody}
        confirmLabel={copy.deleteConfirm}
        cancelLabel={copy.cancel}
        danger
        loading={deletion.isPending}
        onConfirm={() => deletion.mutate()}
        onCancel={() => setConfirmDelete(false)}
      />
    </View>
  );

  return (
    <AppShell active="home">
      <Screen>
        <ScrollView contentContainerStyle={{ flexGrow: 1, padding: width >= breakpoint.wide ? spacing.xxxl : spacing.xl }}>
          {content}
        </ScrollView>
      </Screen>
    </AppShell>
  );
}
