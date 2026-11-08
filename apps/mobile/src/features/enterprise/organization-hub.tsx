import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { View } from 'react-native';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { PhoneInput } from '@/components/ui/phone-input';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import { indianMobileSchema } from '@/schemas/common';
import {
  createOrganization,
  getOrganizationAnalytics,
  getOrganizationSubscription,
  inviteOrganizationMember,
  linkClinic as linkOrganizationClinic,
  listOrganizationClinics,
  listOrganizationMembers,
  listOrganizations,
} from '@/services/api/enterprise-api';
import {
  cancelSaasSubscription,
  changeSaasPlan,
  confirmSaasCharge,
  getOrgBranding,
  getOrgUsage,
  getSaasSubscription,
  listPublicPlans,
  listSaasCharges,
  startSaasCharge,
  updateOrgBranding,
} from '@/services/api/saas-api';
import { getMyClinics } from '@/services/api/provider-api';
import { refreshSession } from '@/services/api/client';
import { persistSession, useAccountPermissions, useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { normalizeIndianMobile } from '@/utils/phone';
import { problemMessage } from '@/utils/problem';

function todayIso() {
  return new Date().toISOString().slice(0, 10);
}

function daysAgoIso(days: number) {
  const date = new Date();
  date.setUTCDate(date.getUTCDate() - days);
  return date.toISOString().slice(0, 10);
}

export function OrganizationHub() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const queryClient = useQueryClient();
  const permissions = useAccountPermissions();
  const canAccess = permissions.includes('org.access');
  const canCreate = permissions.includes('provider.onboard');
  const [orgName, setOrgName] = useState('');
  const [selectedOrgId, setSelectedOrgId] = useState<string | null>(null);
  const [invitePhone, setInvitePhone] = useState('');
  const [inviteRole, setInviteRole] = useState('OrganizationAdmin');
  const [error, setError] = useState<string | null>(null);
  const [headline, setHeadline] = useState('');
  const [primaryColor, setPrimaryColor] = useState('#0F766E');
  const [slug, setSlug] = useState('');

  const orgs = useQuery({
    queryKey: ['enterprise', 'organizations'],
    queryFn: listOrganizations,
    enabled: canAccess,
  });

  const activeOrgId = selectedOrgId ?? orgs.data?.[0]?.id ?? null;

  const clinics = useQuery({
    queryKey: ['enterprise', 'clinics', activeOrgId],
    queryFn: () => listOrganizationClinics(activeOrgId!),
    enabled: !!activeOrgId && canAccess,
  });

  const members = useQuery({
    queryKey: ['enterprise', 'members', activeOrgId],
    queryFn: () => listOrganizationMembers(activeOrgId!),
    enabled: !!activeOrgId && permissions.includes('org.staff'),
  });

  const analytics = useQuery({
    queryKey: ['enterprise', 'analytics', activeOrgId],
    queryFn: () => getOrganizationAnalytics(activeOrgId!, daysAgoIso(30), todayIso()),
    enabled: !!activeOrgId && permissions.includes('org.analytics'),
  });

  const subscription = useQuery({
    queryKey: ['enterprise', 'subscription', activeOrgId],
    queryFn: () => getOrganizationSubscription(activeOrgId!),
    enabled: !!activeOrgId && permissions.includes('org.billing'),
  });

  const saasSubscription = useQuery({
    queryKey: ['saas', 'subscription', activeOrgId],
    queryFn: () => getSaasSubscription(activeOrgId!),
    enabled: !!activeOrgId && permissions.includes('org.billing'),
  });

  const usage = useQuery({
    queryKey: ['saas', 'usage', activeOrgId],
    queryFn: () => getOrgUsage(activeOrgId!),
    enabled: !!activeOrgId && canAccess,
  });

  const branding = useQuery({
    queryKey: ['saas', 'branding', activeOrgId],
    queryFn: () => getOrgBranding(activeOrgId!),
    enabled: !!activeOrgId && permissions.includes('org.manage'),
  });

  const plans = useQuery({
    queryKey: ['saas', 'plans'],
    queryFn: listPublicPlans,
    enabled: permissions.includes('org.billing') || permissions.includes('org.manage'),
  });

  const charges = useQuery({
    queryKey: ['saas', 'charges', activeOrgId],
    queryFn: () => listSaasCharges(activeOrgId!),
    enabled: !!activeOrgId && permissions.includes('org.billing'),
  });

  const ownedClinics = useQuery({
    queryKey: ['enterprise', 'owned-clinics'],
    queryFn: getMyClinics,
    enabled: canCreate,
  });

  const create = useMutation({
    mutationFn: async () => {
      const created = await createOrganization(orgName || 'My healthcare group', 'ClinicChain');
      const refreshToken = useSessionStore.getState().refreshToken;
      if (refreshToken) {
        await persistSession(await refreshSession(refreshToken));
      }
      return created;
    },
    onSuccess: async (created) => {
      setOrgName('');
      setSelectedOrgId(created.id);
      setError(null);
      await queryClient.invalidateQueries({ queryKey: ['enterprise'] });
    },
    onError: (err) => setError(problemMessage(err, copy.orgTitle)),
  });

  const link = useMutation({
    mutationFn: (clinicId: string) => linkOrganizationClinic(activeOrgId!, clinicId),
    onSuccess: async () => {
      setError(null);
      await queryClient.invalidateQueries({ queryKey: ['enterprise'] });
    },
    onError: (err) => setError(problemMessage(err, copy.orgLinkClinic)),
  });

  const invite = useMutation({
    mutationFn: () => {
      const parsed = indianMobileSchema.safeParse(invitePhone);
      if (!parsed.success) {
        throw new Error(parsed.error.issues[0]?.message ?? 'Enter a valid Indian mobile number.');
      }
      const phoneNumber = normalizeIndianMobile(parsed.data);
      if (!phoneNumber) {
        throw new Error('Enter a valid Indian mobile number.');
      }
      return inviteOrganizationMember(activeOrgId!, {
        phoneNumber,
        role: inviteRole,
      });
    },
    onSuccess: async () => {
      setInvitePhone('');
      setError(null);
      await queryClient.invalidateQueries({ queryKey: ['enterprise', 'members', activeOrgId] });
    },
    onError: (err) => setError(problemMessage(err, copy.orgInvite)),
  });

  const unlinkedOwned = useMemo(() => {
    const linked = new Set((clinics.data ?? []).map((item) => item.clinicId));
    return (ownedClinics.data ?? []).filter((item) => !linked.has(item.id));
  }, [clinics.data, ownedClinics.data]);

  return (
    <AuthScaffold>
      <View style={{ gap: spacing.md }}>
        <ScreenHeader title={copy.orgTitle} showBack />
        <AppText variant="muted">{copy.orgBody}</AppText>

        {canCreate ? (
          <Card>
            <TextField label={copy.orgName} value={orgName} onChangeText={setOrgName} placeholder={copy.orgNamePlaceholder} />
            <Button label={copy.orgCreate} loading={create.isPending} onPress={() => create.mutate()} />
          </Card>
        ) : null}

        {!canAccess && !canCreate ? <AppText variant="muted">{copy.orgNoAccess}</AppText> : null}

        {(orgs.data ?? []).map((org) => (
          <Card key={org.id}>
            <Button
              label={`${org.name} · ${org.kind}`}
              variant={org.id === activeOrgId ? 'primary' : 'secondary'}
              onPress={() => setSelectedOrgId(org.id)}
            />
            <AppText variant="muted">
              {copy.orgStats
                .replace('{clinics}', String(org.clinicCount))
                .replace('{members}', String(org.memberCount))}
            </AppText>
          </Card>
        ))}

        {activeOrgId ? (
          <>
            <Card>
              <AppText variant="label">{copy.orgClinics}</AppText>
              {(clinics.data ?? []).map((clinic) => (
                <AppText key={clinic.clinicId}>
                  {clinic.name || clinic.clinicId} · {clinic.status} · {clinic.branchCount} {copy.branches}
                </AppText>
              ))}
              {unlinkedOwned.map((clinic) => (
                <Button
                  key={clinic.id}
                  label={`${copy.orgLinkClinic}: ${clinic.name || clinic.id}`}
                  variant="secondary"
                  loading={link.isPending}
                  onPress={() => link.mutate(clinic.id)}
                />
              ))}
            </Card>

            {permissions.includes('org.staff') ? (
              <Card>
                <AppText variant="label">{copy.orgStaff}</AppText>
                <PhoneInput label={copy.phoneLabel} value={invitePhone} onChangeText={setInvitePhone} />
                <TextField label={copy.orgRole} value={inviteRole} onChangeText={setInviteRole} />
                <Button label={copy.orgInvite} loading={invite.isPending} onPress={() => invite.mutate()} />
                {(members.data ?? []).map((member) => (
                  <AppText key={member.id} variant="muted">
                    {member.phoneNumber || member.userId} · {member.role} ·{' '}
                    {member.isActive ? copy.adminEnabled : copy.adminDisabled}
                  </AppText>
                ))}
              </Card>
            ) : null}

            {permissions.includes('org.analytics') && analytics.data ? (
              <Card>
                <AppText variant="label">{copy.orgAnalytics}</AppText>
                <AppText>
                  {copy.orgVolume}: {analytics.data.appointmentVolume}
                </AppText>
                <AppText>
                  {copy.orgCompleted}: {analytics.data.completedAppointments}
                </AppText>
                <AppText>
                  {copy.orgCancelled}: {analytics.data.cancelledAppointments} ({analytics.data.cancellationRate}%)
                </AppText>
                <AppText>
                  {copy.orgRevenue}: ₹{analytics.data.grossConsultationFees.toFixed(0)}
                </AppText>
                <AppText>
                  {copy.orgCommission}: ₹{analytics.data.commissionCaptured.toFixed(0)}
                </AppText>
                {(analytics.data.branches ?? []).slice(0, 5).map((branch) => (
                  <AppText key={branch.branchId} variant="muted">
                    {branch.branchName}: {branch.appointments} · wait{' '}
                    {branch.averageWaitMinutes == null ? '—' : `${branch.averageWaitMinutes}m`}
                  </AppText>
                ))}
              </Card>
            ) : null}

            {canAccess && usage.data ? (
              <Card>
                <AppText variant="label">{copy.orgUsage}</AppText>
                <AppText>
                  {usage.data.planCode} · {usage.data.subscriptionStatus}
                </AppText>
                <AppText variant="muted">
                  Clinics {usage.data.clinicsUsed}/{usage.data.clinicsLimit ?? '∞'} · Staff{' '}
                  {usage.data.staffUsed}/{usage.data.staffLimit ?? '∞'} · Branches {usage.data.branchesUsed}/
                  {usage.data.branchesLimit ?? '∞'}
                </AppText>
              </Card>
            ) : null}

            {permissions.includes('org.manage') && branding.data ? (
              <Card>
                <AppText variant="label">{copy.orgBranding}</AppText>
                {!branding.data.whiteLabelEnabled ? (
                  <AppText variant="muted">{copy.orgUpgradePlan}</AppText>
                ) : (
                  <>
                    <TextField
                      label="Headline"
                      value={headline || branding.data.loginHeadline || ''}
                      onChangeText={setHeadline}
                    />
                    <TextField label="Primary" value={primaryColor} onChangeText={setPrimaryColor} />
                    <TextField
                      label="Subdomain"
                      value={slug || branding.data.subdomainSlug || ''}
                      onChangeText={setSlug}
                    />
                    <Button
                      label={copy.save}
                      onPress={async () => {
                        try {
                          setError(null);
                          await updateOrgBranding(activeOrgId!, {
                            primaryColorHex: primaryColor,
                            accentColorHex: branding.data.accentColorHex,
                            loginHeadline: headline || branding.data.loginHeadline || undefined,
                            subdomainSlug: slug || branding.data.subdomainSlug || undefined,
                          });
                          await queryClient.invalidateQueries({ queryKey: ['saas', 'branding', activeOrgId] });
                        } catch (err) {
                          setError(problemMessage(err, copy.orgTitle));
                        }
                      }}
                    />
                  </>
                )}
              </Card>
            ) : null}

            {permissions.includes('org.billing') && (saasSubscription.data || subscription.data) ? (
              <Card>
                <AppText variant="label">{copy.orgSubscription}</AppText>
                <AppText>
                  {(saasSubscription.data ?? subscription.data)!.planCode} ·{' '}
                  {(saasSubscription.data ?? subscription.data)!.status} ·{' '}
                  {(saasSubscription.data ?? subscription.data)!.seatLimit} {copy.orgSeats}
                </AppText>
                <AppText variant="muted">{copy.orgSaaSNote}</AppText>
                <AppText variant="label">{copy.orgPlans}</AppText>
                {(plans.data ?? []).map((plan) => (
                  <Button
                    key={plan.code}
                    label={`${plan.name} · ₹${plan.monthlyPrice}/mo`}
                    variant="secondary"
                    onPress={async () => {
                      try {
                        setError(null);
                        await changeSaasPlan(activeOrgId!, {
                          planCode: plan.code,
                          billingInterval: 'Monthly',
                          applyImmediately: true,
                        });
                        await queryClient.invalidateQueries({ queryKey: ['saas'] });
                        await queryClient.invalidateQueries({ queryKey: ['enterprise', 'subscription', activeOrgId] });
                      } catch (err) {
                        setError(problemMessage(err, copy.orgTitle));
                      }
                    }}
                  />
                ))}
                <Button
                  label={copy.orgPayRenewal}
                  onPress={async () => {
                    try {
                      setError(null);
                      const charge = await startSaasCharge(activeOrgId!);
                      await confirmSaasCharge(activeOrgId!, charge.id);
                      await queryClient.invalidateQueries({ queryKey: ['saas'] });
                    } catch (err) {
                      setError(problemMessage(err, copy.orgTitle));
                    }
                  }}
                />
                <Button
                  label={copy.orgCancelSub}
                  variant="secondary"
                  onPress={async () => {
                    try {
                      setError(null);
                      await cancelSaasSubscription(activeOrgId!, true);
                      await queryClient.invalidateQueries({ queryKey: ['saas'] });
                    } catch (err) {
                      setError(problemMessage(err, copy.orgTitle));
                    }
                  }}
                />
              </Card>
            ) : null}

            {permissions.includes('org.billing') && charges.data && charges.data.length > 0 ? (
              <Card>
                <AppText variant="label">{copy.orgBillingHistory}</AppText>
                {charges.data.slice(0, 8).map((row) => (
                  <AppText key={row.id} variant="muted">
                    {row.planCode} · {row.status} · ₹{row.amount} · {row.createdAtUtc.slice(0, 10)}
                  </AppText>
                ))}
              </Card>
            ) : null}
          </>
        ) : null}

        {error ? <AppText style={{ color: theme.colors.error }}>{error}</AppText> : null}
      </View>
    </AuthScaffold>
  );
}
