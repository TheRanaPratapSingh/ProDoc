import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { useForm } from 'react-hook-form';
import { Pressable, useWindowDimensions, View } from 'react-native';

import { AppShell } from '@/components/layout/app-shell';
import { Screen } from '@/components/layout/auth-scaffold';
import { FormScreenScroll } from '@/components/layout/keyboard-aware-form';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { ProfileAvatar } from '@/components/ui/profile-avatar';
import {
  ProfileMenuCard,
  ProfileMenuDivider,
  ProfileMenuLink,
  ProfileMenuSection,
} from '@/components/ui/profile-menu';
import {
  IconAppointments,
  IconBellOutline,
  IconBriefcase,
  IconBuilding,
  IconChevron,
  IconEdit,
  IconGift,
  IconGlobe,
  IconHealthRecords,
  IconHelp,
  IconHeart,
  IconInfo,
  IconMoon,
  IconSpark,
} from '@/components/ui/prodoc-icons';
import { Segmented } from '@/components/ui/segmented';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import { profileSchema, type ProfileFormValues } from '@/schemas/auth';
import { getAccount, logout, requestDeletion, updateProfile } from '@/services/api/auth-api';
import { getNotificationPreferences, updateNotificationPreferences } from '@/services/api/automation-api';
import { listFavourites } from '@/services/api/provider-api';
import type { LanguageCode } from '@/services/api/types';
import { notifySuccess } from '@/stores/feedback-store';
import { clearSession, useSessionStore } from '@/stores/session-store';
import { useUiStore, type ColorPreference } from '@/stores/ui-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { breakpoint, spacing } from '@/theme/tokens';
import { canCreateClinic } from '@/utils/admin-permissions';
import { notifyApiError, problemMessage } from '@/utils/problem';

type SectionId =
  | 'edit'
  | 'appointments'
  | 'records'
  | 'notifications'
  | 'language'
  | 'appearance'
  | 'doctors'
  | 'workspace'
  | 'help'
  | null;

function NavRow({
  title,
  subtitle,
  icon,
  onPress,
}: {
  title: string;
  subtitle: string;
  icon: ReactNode;
  onPress: () => void;
}) {
  const theme = useProdocTheme();
  return (
    <Pressable
      onPress={onPress}
      style={({ pressed }) => ({
        flexDirection: 'row',
        alignItems: 'center',
        gap: 12,
        paddingHorizontal: 14,
        paddingVertical: 14,
        backgroundColor: pressed ? theme.colors.primaryLight : theme.colors.surface,
      })}>
      <View
        style={{
          width: 40,
          height: 40,
          borderRadius: 12,
          backgroundColor: theme.colors.primaryLight,
          alignItems: 'center',
          justifyContent: 'center',
        }}>
        {icon}
      </View>
      <View style={{ flex: 1, gap: 2, minWidth: 0 }}>
        <AppText variant="label" style={{ fontSize: 15, lineHeight: 20 }} numberOfLines={1}>
          {title}
        </AppText>
        <AppText variant="muted" style={{ fontSize: 12, lineHeight: 16 }} numberOfLines={1}>
          {subtitle}
        </AppText>
      </View>
      <IconChevron size={18} color={theme.colors.textMuted} />
    </Pressable>
  );
}

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
  const [openSection, setOpenSection] = useState<SectionId>(null);
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
    enabled: Boolean(accessToken) && openSection === 'notifications',
  });

  const favouritesQuery = useQuery({
    queryKey: ['favourites'],
    queryFn: listFavourites,
    enabled: Boolean(accessToken) && openSection === 'doctors',
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
      setOpenSection(null);
      notifySuccess(copy.profileUpdatedSuccess);
    },
    onError: (error) => {
      notifyApiError(error, copy.saveFailed);
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

  const toggle = (id: Exclude<SectionId, null>) => {
    setOpenSection((current) => (current === id ? null : id));
  };

  const current = profileQuery.data ?? account;
  const permissions = current?.permissions ?? [];
  const canBook = permissions.includes('appointments.book.self');

  const workspaceLinks = useMemo(
    () =>
      (
        [
          permissions.some((permission) =>
            ['appointments.manage.assigned', 'clinic.manage', 'appointments.book.walkin'].includes(permission),
          ) && { label: copy.practiceVisits, href: '/practice' },
          permissions.includes('appointments.manage.assigned') && {
            label: copy.doctorQueue,
            href: '/doctor-queue',
          },
          permissions.includes('queue.operate') && { label: copy.receptionDesk, href: '/reception' },
          (permissions.includes('clinic.manage') || permissions.includes('commissions.manage')) && {
            label: copy.financeDashboard,
            href: '/finance',
          },
          permissions.includes('admin.access') && { label: copy.adminCenter, href: '/admin' },
          permissions.includes('admin.access') && { label: copy.appointmentActivity, href: '/activity' },
          permissions.includes('admin.access') && { label: copy.queueMonitor, href: '/queue-monitor' },
          permissions.includes('admin.support') && { label: copy.agentDeskTitle, href: '/agent-desk' },
          // Developer portal is PlatformAdmin-only (partners.develop is not granted to patients).
          permissions.includes('partners.develop') && { label: copy.partnersPortalTitle, href: '/partners' },
          permissions.includes('provider.onboard') && { label: copy.doctorProfile, href: '/doctor' },
          permissions.includes('provider.onboard') &&
            !canCreateClinic(permissions) && { label: copy.clinicProfile, href: '/clinic' },
          canCreateClinic(permissions) && { label: copy.createClinic, href: '/admin' },
          permissions.includes('provider.onboard') && { label: copy.schedules, href: '/schedule' },
          permissions.includes('verification.review') && { label: copy.reviews, href: '/reviews' },
          permissions.includes('review.create') && { label: copy.reviewWriteTitle, href: '/write-review' },
          permissions.includes('review.create') && { label: copy.reviewSectionTitle, href: '/my-reviews' },
          permissions.includes('review.respond') && { label: copy.reviewInboxTitle, href: '/review-inbox' },
          permissions.includes('integrations.use') && { label: copy.integrationsTitle, href: '/integrations' },
        ] as const
      ).filter(Boolean) as { label: string; href: string }[],
    [copy, permissions],
  );

  const pad = width >= breakpoint.wide ? spacing.xxxl : spacing.xl;
  const showOrg = permissions.includes('org.access') || permissions.includes('org.manage');
  const showAssistant = permissions.includes('assistant.use');
  const showGrowth = permissions.includes('growth.use');

  return (
    <AppShell active="profile">
      <Screen>
        <FormScreenScroll
          contentContainerStyle={{
            flexGrow: 1,
            padding: pad,
            paddingBottom: 32,
            gap: 14,
            width: '100%',
            maxWidth: width >= breakpoint.wide ? 720 : 480,
            alignSelf: 'center',
          }}>
          <View
            style={{
              backgroundColor: theme.colors.surface,
              borderRadius: 18,
              borderWidth: 1,
              borderColor: theme.colors.border,
              padding: 16,
              gap: 14,
            }}>
            <View style={{ flexDirection: 'row', alignItems: 'center', gap: 14 }}>
              <ProfileAvatar name={current?.fullName || copy.brand} size={64} />
              <View style={{ flex: 1, gap: 4, minWidth: 0 }}>
                <AppText variant="headline" style={{ fontSize: 20, lineHeight: 26 }} numberOfLines={1}>
                  {current?.fullName || copy.welcome}
                </AppText>
                <AppText variant="muted" style={{ fontSize: 14 }} numberOfLines={1}>
                  {current?.phoneNumber}
                </AppText>
              </View>
              <Pressable
                onPress={() => toggle('edit')}
                accessibilityRole="button"
                accessibilityLabel={copy.editProfile}
                style={{
                  flexDirection: 'row',
                  alignItems: 'center',
                  gap: 6,
                  paddingHorizontal: 12,
                  paddingVertical: 8,
                  borderRadius: 999,
                  backgroundColor: theme.colors.primaryLight,
                }}>
                <IconEdit size={16} color={theme.colors.primary} />
                <AppText variant="label" style={{ fontSize: 12, color: theme.colors.primary }}>
                  {copy.editProfile}
                </AppText>
              </Pressable>
            </View>

            {openSection === 'edit' ? (
              <View style={{ gap: spacing.md, paddingTop: 4, borderTopWidth: 1, borderTopColor: theme.colors.border }}>
                <TextField
                  label={copy.nameLabel}
                  placeholder={copy.namePlaceholder}
                  value={form.watch('fullName')}
                  onChangeText={(value) => form.setValue('fullName', value)}
                  required
                  error={
                    form.formState.errors.fullName?.message ??
                    (save.error ? problemMessage(save.error, copy.saveFailed) : undefined)
                  }
                />
                <Button
                  label={copy.save}
                  loading={save.isPending}
                  disabled={save.isPending}
                  onPress={() => void submit()}
                />
              </View>
            ) : null}
          </View>

          <ProfileMenuCard>
            {canBook ? (
              <>
                <ProfileMenuSection
                  title={copy.profileMenuAppointments}
                  subtitle={copy.profileMenuAppointmentsBody}
                  icon={<IconAppointments size={20} color={theme.colors.primary} />}
                  open={openSection === 'appointments'}
                  onToggle={() => toggle('appointments')}
                  reduceMotion={theme.reduceMotion}>
                  <ProfileMenuLink label={copy.myVisits} onPress={() => router.push('/visits' as Href)} />
                  <ProfileMenuDivider />
                  <ProfileMenuLink label={copy.myQueue} onPress={() => router.push('/queue' as Href)} />
                  <ProfileMenuDivider />
                  <ProfileMenuLink label={copy.appointmentHistory} onPress={() => router.push('/visits' as Href)} />
                </ProfileMenuSection>
                <View style={{ height: 1, backgroundColor: theme.colors.border }} />
              </>
            ) : null}

            <ProfileMenuSection
              title={copy.profileMenuRecords}
              subtitle={copy.profileMenuRecordsBody}
              icon={<IconHealthRecords size={20} color={theme.colors.primary} />}
              open={openSection === 'records'}
              onToggle={() => toggle('records')}
              reduceMotion={theme.reduceMotion}>
              <ProfileMenuLink label={copy.prescriptions} onPress={() => router.push('/health-records' as Href)} />
              <ProfileMenuDivider />
              <ProfileMenuLink label={copy.labReports} onPress={() => router.push('/health-records' as Href)} />
              <ProfileMenuDivider />
              <ProfileMenuLink label={copy.medicalDocuments} onPress={() => router.push('/health-records' as Href)} />
              <ProfileMenuDivider />
              <ProfileMenuLink label={copy.healthHistory} onPress={() => router.push('/health-records' as Href)} />
            </ProfileMenuSection>

            <View style={{ height: 1, backgroundColor: theme.colors.border }} />

            <ProfileMenuSection
              title={copy.profileMenuNotifications}
              subtitle={copy.profileMenuNotificationsBody}
              icon={<IconBellOutline size={20} color={theme.colors.primary} />}
              open={openSection === 'notifications'}
              onToggle={() => toggle('notifications')}
              reduceMotion={theme.reduceMotion}>
              {prefsQuery.isLoading ? (
                <AppText variant="muted" style={{ paddingHorizontal: 66, paddingVertical: 12 }}>
                  {copy.loading}
                </AppText>
              ) : null}
              {prefsQuery.data
                ? (
                    [
                      ['inAppEnabled', copy.prefInApp],
                      ['emailEnabled', copy.prefEmail],
                      ['smsEnabled', copy.prefSms],
                      ['pushEnabled', copy.prefPush],
                      ['marketingEnabled', copy.prefMarketing],
                      ['appointmentRemindersEnabled', copy.prefReminders],
                      ['queueUpdatesEnabled', copy.prefQueue],
                    ] as const
                  ).map(([key, label], index, rows) => (
                    <View key={key}>
                      <ProfileMenuLink
                        label={label}
                        detail={prefsQuery.data![key] ? 'ON' : 'OFF'}
                        onPress={async () => {
                          const next = { ...prefsQuery.data! };
                          next[key] = !next[key];
                          const { updatedAtUtc: _, ...body } = next;
                          await updateNotificationPreferences(body);
                          await queryClient.invalidateQueries({ queryKey: ['account', 'notification-preferences'] });
                        }}
                      />
                      {index < rows.length - 1 ? <ProfileMenuDivider /> : null}
                    </View>
                  ))
                : null}
            </ProfileMenuSection>

            <View style={{ height: 1, backgroundColor: theme.colors.border }} />

            <ProfileMenuSection
              title={copy.profileMenuLanguage}
              subtitle={copy.profileMenuLanguageBody}
              icon={<IconGlobe size={20} color={theme.colors.primary} />}
              open={openSection === 'language'}
              onToggle={() => toggle('language')}
              reduceMotion={theme.reduceMotion}>
              <View style={{ paddingHorizontal: 16, paddingVertical: 12, paddingLeft: 66 }}>
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
              </View>
            </ProfileMenuSection>

            <View style={{ height: 1, backgroundColor: theme.colors.border }} />

            <ProfileMenuSection
              title={copy.profileMenuAppearance}
              subtitle={copy.profileMenuAppearanceBody}
              icon={<IconMoon size={20} color={theme.colors.primary} />}
              open={openSection === 'appearance'}
              onToggle={() => toggle('appearance')}
              reduceMotion={theme.reduceMotion}>
              <View style={{ paddingHorizontal: 16, paddingVertical: 12, paddingLeft: 66 }}>
                <Segmented<ColorPreference>
                  value={colorScheme}
                  options={[
                    { value: 'light', label: copy.light },
                    { value: 'dark', label: copy.dark },
                    { value: 'system', label: copy.system },
                  ]}
                  onChange={setColorScheme}
                />
              </View>
            </ProfileMenuSection>

            <View style={{ height: 1, backgroundColor: theme.colors.border }} />

            <ProfileMenuSection
              title={copy.profileMenuDoctors}
              subtitle={copy.profileMenuDoctorsBody}
              icon={<IconHeart size={20} color={theme.colors.primary} />}
              open={openSection === 'doctors'}
              onToggle={() => toggle('doctors')}
              reduceMotion={theme.reduceMotion}>
              <AppText variant="label" style={{ paddingHorizontal: 66, paddingTop: 10, paddingBottom: 4, fontSize: 12 }}>
                {copy.savedDoctors}
              </AppText>
              {(favouritesQuery.data ?? []).map((doctor, index) => (
                <View key={doctor.id}>
                  {index > 0 ? <ProfileMenuDivider /> : null}
                  <ProfileMenuLink
                    label={doctor.displayName}
                    detail={doctor.primarySpecialization}
                    onPress={() => router.push(`/practices/${doctor.id}` as Href)}
                  />
                </View>
              ))}
              {favouritesQuery.isFetched && (favouritesQuery.data?.length ?? 0) === 0 ? (
                <AppText variant="muted" style={{ paddingHorizontal: 66, paddingBottom: 12 }}>
                  {copy.noSavedDoctors}
                </AppText>
              ) : null}
              {permissions.includes('provider.onboard') ? (
                <>
                  <ProfileMenuDivider />
                  <ProfileMenuLink label={copy.doctorProfile} onPress={() => router.push('/doctor' as Href)} />
                  <ProfileMenuDivider />
                  <ProfileMenuLink
                    label={canCreateClinic(permissions) ? copy.createClinic : copy.clinicProfile}
                    onPress={() =>
                      router.push((canCreateClinic(permissions) ? '/admin' : '/clinic') as Href)
                    }
                  />
                </>
              ) : canCreateClinic(permissions) ? (
                <>
                  <ProfileMenuDivider />
                  <ProfileMenuLink label={copy.createClinic} onPress={() => router.push('/admin' as Href)} />
                </>
              ) : null}
            </ProfileMenuSection>
          </ProfileMenuCard>

          <ProfileMenuCard>
            {showAssistant ? (
              <>
                <NavRow
                  title={copy.assistantTitle}
                  subtitle={copy.assistantMessage}
                  icon={<IconSpark size={20} color={theme.colors.primary} />}
                  onPress={() => router.push('/assistant' as Href)}
                />
                <View style={{ height: 1, backgroundColor: theme.colors.border }} />
              </>
            ) : null}

            {showOrg ? (
              <>
                <NavRow
                  title={copy.orgTitle}
                  subtitle={copy.profileMenuWorkspaceBody}
                  icon={<IconBuilding size={20} color={theme.colors.primary} />}
                  onPress={() => router.push('/organization' as Href)}
                />
                <View style={{ height: 1, backgroundColor: theme.colors.border }} />
              </>
            ) : null}

            {showGrowth ? (
              <>
                <NavRow
                  title={copy.growthTitle}
                  subtitle={copy.rewardsWallet}
                  icon={<IconGift size={20} color={theme.colors.primary} />}
                  onPress={() => router.push('/rewards' as Href)}
                />
                <View style={{ height: 1, backgroundColor: theme.colors.border }} />
              </>
            ) : null}

            <ProfileMenuSection
              title={copy.profileMenuHelp}
              subtitle={copy.profileMenuHelpBody}
              icon={<IconHelp size={20} color={theme.colors.primary} />}
              open={openSection === 'help'}
              onToggle={() => toggle('help')}
              reduceMotion={theme.reduceMotion}>
              <ProfileMenuLink label={copy.helpCenterTitle} onPress={() => router.push('/help' as Href)} />
              <ProfileMenuDivider />
              <ProfileMenuLink label={copy.supportCenter} onPress={() => router.push('/support' as Href)} />
            </ProfileMenuSection>

            <View style={{ height: 1, backgroundColor: theme.colors.border }} />

            <NavRow
              title={copy.profileMenuAbout}
              subtitle={copy.profileMenuAboutBody}
              icon={<IconInfo size={20} color={theme.colors.primary} />}
              onPress={() => router.push('/about' as Href)}
            />
          </ProfileMenuCard>

          {workspaceLinks.length > 0 ? (
            <ProfileMenuCard>
              <ProfileMenuSection
                title={copy.profileMenuWorkspace}
                subtitle={copy.profileMenuWorkspaceBody}
                icon={<IconBriefcase size={20} color={theme.colors.primary} />}
                open={openSection === 'workspace'}
                onToggle={() => toggle('workspace')}
                reduceMotion={theme.reduceMotion}>
                {workspaceLinks.map((item, index) => (
                  <View key={`${item.href}-${item.label}`}>
                    {index > 0 ? <ProfileMenuDivider /> : null}
                    <ProfileMenuLink label={item.label} onPress={() => router.push(item.href as Href)} />
                  </View>
                ))}
              </ProfileMenuSection>
            </ProfileMenuCard>
          ) : null}

          <ProfileMenuCard>
            <View style={{ padding: 14, gap: 10 }}>
              <Button
                label={copy.signOut}
                variant="secondary"
                loading={signOut.isPending}
                onPress={() => signOut.mutate()}
              />
              <Button label={copy.delete} variant="danger" onPress={() => setConfirmDelete(true)} />
              {deletion.error ? <AppText variant="muted">{problemMessage(deletion.error, copy.delete)}</AppText> : null}
            </View>
          </ProfileMenuCard>

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
        </FormScreenScroll>
      </Screen>
    </AppShell>
  );
}
