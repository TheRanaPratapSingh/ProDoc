import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { BackHandler, Linking, Pressable, View } from 'react-native';

import { AdminChartCard } from '@/components/admin/admin-chart-card';
import {
  AdminIconAudit,
  AdminIconAutomation,
  AdminIconChart,
  AdminIconConfig,
  AdminIconFinance,
  AdminIconGrowth,
  AdminIconOps,
  AdminIconOverview,
  AdminIconPartners,
  AdminIconProviders,
  AdminIconShield,
  AdminIconStar,
  AdminIconSupport,
  AdminIconTenants,
  AdminIconUsers,
} from '@/components/admin/admin-icons';
import { AdminKpiCard } from '@/components/admin/admin-kpi-card';
import { AdminPanel, AdminShell, AdminSkeleton, type AdminNavItem } from '@/components/admin/admin-shell';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { StatusBadge } from '@/components/ui/status-badge';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { AdminClinicsPanel } from '@/features/admin/admin-clinics';
import { AdminCreateUserPanel } from '@/features/admin/admin-create-user';
import { AdminManageUsersPanel } from '@/features/admin/admin-manage-users';
import { useCopy } from '@/i18n/copy';
import { logout } from '@/services/api/auth-api';
import {
  financeCsvUrl,
  getAdminDashboard,
  getAdminTicket,
  getFinanceReport,
  listAdminProviders,
  listAdminTickets,
  listAudit,
  listFeatureFlags,
  listPlatformSettings,
  updateAdminTicket,
  upsertFeatureFlag,
  upsertPlatformSetting,
  type AdminDashboard,
  type NamedAmount,
  type NamedCount,
} from '@/services/api/admin-api';
import {
  analyticsExportUrl,
  getExecutiveAnalytics,
  getOperationalAnalytics,
  getPredictiveInsights,
} from '@/services/api/analytics-api';
import { getPlatformTenants, suspendTenant } from '@/services/api/saas-api';
import {
  getAutomationAnalytics,
  listAutomationOutbox,
  listAutomationRuns,
  listWorkflows,
  setWorkflowEnabled,
} from '@/services/api/automation-api';
import { decidePartner, getPartnerPlatformStats, listAdminPartners } from '@/services/api/partners-api';
import {
  createAdminCoupon,
  getGrowthDashboard,
  listAdminCampaigns,
  listAdminCoupons,
  setCampaignActive,
  setCouponActive,
} from '@/services/api/growth-api';
import { listModerationReviews, moderateReview } from '@/services/api/review-api';
import { clearSession, useAccountPermissions, useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

type Section =
  | 'overview'
  | 'analytics'
  | 'users-create'
  | 'users-manage'
  | 'clinics'
  | 'providers'
  | 'verification'
  | 'ops'
  | 'finance'
  | 'support'
  | 'moderation'
  | 'growth'
  | 'tenants'
  | 'automation'
  | 'partners'
  | 'config'
  | 'audit';

function isoDaysAgo(days: number) {
  const d = new Date();
  d.setUTCDate(d.getUTCDate() - days);
  return d.toISOString().slice(0, 10);
}

function isoToday() {
  return new Date().toISOString().slice(0, 10);
}

function MetricCard({ label, value, hint, icon }: { label: string; value: string; hint?: string; icon?: ReactNode }) {
  return <AdminKpiCard label={label} value={value} hint={hint} icon={icon} />;
}

function BarChart({ title, rows, kind }: { title: string; rows: NamedCount[] | NamedAmount[]; kind: 'count' | 'amount' }) {
  return (
    <AdminChartCard
      title={title}
      rows={rows.map((row) => ({
        label: row.label,
        value: kind === 'count' ? (row as NamedCount).count : Number((row as NamedAmount).amount),
      }))}
      formatValue={kind === 'amount' ? (value) => `₹${value.toFixed(0)}` : undefined}
    />
  );
}

function Skeleton() {
  return <AdminSkeleton height={88} />;
}

export function AdminControlCenter() {
  const copy = useCopy();
  const router = useRouter();
  const theme = useProdocTheme();
  const queryClient = useQueryClient();
  const permissions = useAccountPermissions();
  const refreshToken = useSessionStore((state) => state.refreshToken);
  const iconColor = theme.colors.textMuted;
  const iconActive = theme.colors.primary;
  const [section, setSection] = useState<Section>('overview');
  const [sectionHistory, setSectionHistory] = useState<Section[]>([]);
  const backLock = useRef(false);
  const [stateFilter, setStateFilter] = useState('');
  const [cityFilter, setCityFilter] = useState('');
  const [ticketNote, setTicketNote] = useState('');
  const [selectedTicketId, setSelectedTicketId] = useState<string | null>(null);
  const [settingDrafts, setSettingDrafts] = useState<Record<string, string>>({});
  const [moderationReason, setModerationReason] = useState('');
  const [biFrom, setBiFrom] = useState(isoDaysAgo(30));
  const [biTo, setBiTo] = useState(isoToday());
  const [couponCode, setCouponCode] = useState('');
  const [couponName, setCouponName] = useState('');
  const [couponValue, setCouponValue] = useState('10');

  const goToSection = useCallback((id: Section) => {
    setSection((current) => {
      if (current === id) {
        return current;
      }
      setSectionHistory((history) => [...history, current]);
      return id;
    });
  }, []);

  const handleAdminBack = useCallback(() => {
    if (backLock.current) {
      return true;
    }

    if (sectionHistory.length > 0) {
      backLock.current = true;
      setTimeout(() => {
        backLock.current = false;
      }, 400);
      const previous = sectionHistory[sectionHistory.length - 1]!;
      setSectionHistory((history) => history.slice(0, -1));
      setSection(previous);
      return true;
    }

    if (section !== 'overview') {
      backLock.current = true;
      setTimeout(() => {
        backLock.current = false;
      }, 400);
      setSection('overview');
      return true;
    }

    // Admin landing: never replace('/admin') again. Let Android exit if needed.
    return false;
  }, [section, sectionHistory]);

  useEffect(() => {
    const sub = BackHandler.addEventListener('hardwareBackPress', () => handleAdminBack());
    return () => sub.remove();
  }, [handleAdminBack]);

  const signOut = useMutation({
    mutationFn: async () => {
      if (refreshToken) {
        await logout(refreshToken);
      }
    },
    onSettled: async () => {
      await clearSession();
      queryClient.clear();
      router.replace('/admin-sign-in');
    },
  });

  const canUsers = permissions.includes('admin.users');
  const canSupport = permissions.includes('admin.support');
  const canConfig = permissions.includes('admin.config');
  const canFinance = permissions.includes('commissions.manage');
  const canAudit = permissions.includes('audit.read');
  const canVerify = permissions.includes('verification.review');
  const canModerateReviews = permissions.includes('review.moderate');
  const canGrowth = permissions.includes('growth.manage');
  const canAnalytics = permissions.includes('analytics.read');
  const canAnalyticsExport = permissions.includes('analytics.export');
  const canTenants = permissions.includes('tenants.manage');
  const canAutomation = permissions.includes('automation.manage');
  const canPartners = permissions.includes('partners.manage');

  const nav: AdminNavItem[] = [
    { id: 'overview', label: copy.adminOverview, visible: true, icon: <AdminIconOverview color={section === 'overview' ? iconActive : iconColor} /> },
    { id: 'analytics', label: copy.biTitle, visible: canAnalytics, icon: <AdminIconChart color={section === 'analytics' ? iconActive : iconColor} /> },
    { id: 'users-create', label: copy.adminCreateUser, visible: canUsers, icon: <AdminIconUsers color={section === 'users-create' ? iconActive : iconColor} /> },
    { id: 'users-manage', label: copy.adminManageUsers, visible: canUsers, icon: <AdminIconUsers color={section === 'users-manage' ? iconActive : iconColor} /> },
    { id: 'clinics', label: copy.adminClinicsNav, visible: canUsers, icon: <AdminIconProviders color={section === 'clinics' ? iconActive : iconColor} /> },
    { id: 'providers', label: copy.adminProviders, visible: true, icon: <AdminIconProviders color={section === 'providers' ? iconActive : iconColor} /> },
    { id: 'verification', label: copy.reviews, visible: canVerify, icon: <AdminIconShield color={section === 'verification' ? iconActive : iconColor} /> },
    { id: 'ops', label: copy.adminOps, visible: true, icon: <AdminIconOps color={section === 'ops' ? iconActive : iconColor} /> },
    { id: 'finance', label: copy.adminFinanceReports, visible: canFinance, icon: <AdminIconFinance color={section === 'finance' ? iconActive : iconColor} /> },
    { id: 'support', label: copy.adminSupport, visible: canSupport, icon: <AdminIconSupport color={section === 'support' ? iconActive : iconColor} /> },
    { id: 'moderation', label: copy.reviewModeration, visible: canModerateReviews, icon: <AdminIconStar color={section === 'moderation' ? iconActive : iconColor} /> },
    { id: 'growth', label: copy.growthAdmin, visible: canGrowth, icon: <AdminIconGrowth color={section === 'growth' ? iconActive : iconColor} /> },
    { id: 'tenants', label: copy.tenantsAdmin, visible: canTenants, icon: <AdminIconTenants color={section === 'tenants' ? iconActive : iconColor} /> },
    { id: 'automation', label: copy.automationTitle, visible: canAutomation, icon: <AdminIconAutomation color={section === 'automation' ? iconActive : iconColor} /> },
    { id: 'partners', label: copy.partnersAdmin, visible: canPartners, icon: <AdminIconPartners color={section === 'partners' ? iconActive : iconColor} /> },
    { id: 'config', label: copy.adminConfig, visible: canConfig, icon: <AdminIconConfig color={section === 'config' ? iconActive : iconColor} /> },
    { id: 'audit', label: copy.adminAudit, visible: canAudit, icon: <AdminIconAudit color={section === 'audit' ? iconActive : iconColor} /> },
  ];

  const sectionMeta: Record<Section, { title: string; subtitle: string }> = {
    overview: { title: copy.adminOverview, subtitle: copy.adminCenterBody },
    analytics: { title: copy.biTitle, subtitle: copy.biBody },
    'users-create': { title: copy.adminCreateUser, subtitle: copy.adminCreateUserBody },
    'users-manage': { title: copy.adminManageUsers, subtitle: copy.adminManageUsersBody },
    clinics: { title: copy.adminClinicsNav, subtitle: copy.adminClinicsBody },
    providers: { title: copy.adminProviders, subtitle: copy.adminEmptyProviders },
    verification: { title: copy.reviews, subtitle: copy.adminPendingDoctors },
    ops: { title: copy.adminOps, subtitle: copy.adminOpsBody },
    finance: { title: copy.adminFinanceReports, subtitle: copy.adminActualFinanceNote },
    support: { title: copy.adminSupport, subtitle: copy.adminEmptyTickets },
    moderation: { title: copy.reviewModeration, subtitle: copy.reviewModeration },
    growth: { title: copy.growthAdmin, subtitle: copy.growthAdmin },
    tenants: { title: copy.tenantsAdmin, subtitle: copy.tenantsAdmin },
    automation: { title: copy.automationTitle, subtitle: copy.automationTitle },
    partners: { title: copy.partnersAdmin, subtitle: copy.partnersAdmin },
    config: { title: copy.adminConfig, subtitle: copy.adminSettings },
    audit: { title: copy.adminAudit, subtitle: copy.adminEmptyAudit },
  };

  const dashboard = useQuery({
    queryKey: ['admin', 'dashboard', stateFilter, cityFilter],
    queryFn: () =>
      getAdminDashboard({
        state: stateFilter || undefined,
        city: cityFilter || undefined,
      }),
    enabled: section === 'overview' || section === 'verification',
    refetchInterval: section === 'overview' ? 60_000 : false,
    refetchIntervalInBackground: false,
  });

  const executive = useQuery({
    queryKey: ['analytics', 'executive', biFrom, biTo],
    queryFn: () => getExecutiveAnalytics({ from: biFrom, to: biTo }),
    enabled: section === 'analytics' && canAnalytics,
  });

  const operational = useQuery({
    queryKey: ['analytics', 'operational', biFrom, biTo],
    queryFn: () => getOperationalAnalytics({ from: biFrom, to: biTo }),
    enabled: section === 'analytics' && canAnalytics,
  });

  const predictive = useQuery({
    queryKey: ['analytics', 'predictive'],
    queryFn: getPredictiveInsights,
    enabled: section === 'analytics' && canAnalytics,
  });

  const tenants = useQuery({
    queryKey: ['admin', 'tenants'],
    queryFn: getPlatformTenants,
    enabled: section === 'tenants' && canTenants,
  });

  const workflows = useQuery({
    queryKey: ['automation', 'workflows'],
    queryFn: () => listWorkflows(),
    enabled: section === 'automation' && canAutomation,
  });

  const automationAnalytics = useQuery({
    queryKey: ['automation', 'analytics'],
    queryFn: getAutomationAnalytics,
    enabled: section === 'automation' && canAutomation,
  });

  const automationRuns = useQuery({
    queryKey: ['automation', 'runs'],
    queryFn: () => listAutomationRuns(),
    enabled: section === 'automation' && canAutomation,
  });

  const automationOutbox = useQuery({
    queryKey: ['automation', 'outbox'],
    queryFn: () => listAutomationOutbox(),
    enabled: section === 'automation' && canAutomation,
  });

  const partnerStats = useQuery({
    queryKey: ['admin', 'partners', 'stats'],
    queryFn: getPartnerPlatformStats,
    enabled: section === 'partners' && canPartners,
  });

  const partnerOrgs = useQuery({
    queryKey: ['admin', 'partners'],
    queryFn: () => listAdminPartners(),
    enabled: section === 'partners' && canPartners,
  });

  const providers = useQuery({
    queryKey: ['admin', 'providers', section],
    queryFn: () => listAdminProviders({ kind: section === 'providers' ? 'doctor' : 'doctor' }),
    enabled: section === 'providers',
  });

  const providerClinics = useQuery({
    queryKey: ['admin', 'provider-clinics'],
    queryFn: () => listAdminProviders({ kind: 'clinic' }),
    enabled: section === 'providers',
  });

  const finance = useQuery({
    queryKey: ['admin', 'finance-report', stateFilter, cityFilter],
    queryFn: () => getFinanceReport({ state: stateFilter || undefined, city: cityFilter || undefined }),
    enabled: section === 'finance' && canFinance,
  });

  const tickets = useQuery({
    queryKey: ['admin', 'tickets'],
    queryFn: () => listAdminTickets(),
    enabled: section === 'support' && canSupport,
  });

  const ticket = useQuery({
    queryKey: ['admin', 'ticket', selectedTicketId],
    queryFn: () => getAdminTicket(selectedTicketId!),
    enabled: section === 'support' && canSupport && Boolean(selectedTicketId),
  });

  const settings = useQuery({
    queryKey: ['admin', 'settings'],
    queryFn: listPlatformSettings,
    enabled: (section === 'config' && canConfig) || (section === 'growth' && canGrowth),
  });

  const flags = useQuery({
    queryKey: ['admin', 'flags'],
    queryFn: listFeatureFlags,
    enabled: section === 'config' && canConfig,
  });

  const audit = useQuery({
    queryKey: ['admin', 'audit'],
    queryFn: () => listAudit(),
    enabled: section === 'audit' && canAudit,
  });

  const moderation = useQuery({
    queryKey: ['admin', 'review-moderation'],
    queryFn: () => listModerationReviews(),
    enabled: section === 'moderation' && canModerateReviews,
  });

  const growth = useQuery({
    queryKey: ['admin', 'growth-dashboard'],
    queryFn: getGrowthDashboard,
    enabled: section === 'growth' && canGrowth,
  });

  const growthCampaigns = useQuery({
    queryKey: ['admin', 'growth-campaigns'],
    queryFn: listAdminCampaigns,
    enabled: section === 'growth' && canGrowth,
  });

  const growthCoupons = useQuery({
    queryKey: ['admin', 'growth-coupons'],
    queryFn: listAdminCoupons,
    enabled: section === 'growth' && canGrowth,
  });

  const resolveTicket = useMutation({
    mutationFn: () =>
      updateAdminTicket(selectedTicketId!, {
        status: 'Resolved',
        note: ticketNote || undefined,
        internalNote: true,
      }),
    onSuccess: async () => {
      setTicketNote('');
      await queryClient.invalidateQueries({ queryKey: ['admin', 'tickets'] });
      await queryClient.invalidateQueries({ queryKey: ['admin', 'ticket', selectedTicketId] });
    },
  });

  const saveSetting = useMutation({
    mutationFn: (input: { key: string; value: string }) => upsertPlatformSetting(input.key, input.value),
    onSuccess: async () => queryClient.invalidateQueries({ queryKey: ['admin', 'settings'] }),
  });

  const toggleFlag = useMutation({
    mutationFn: (input: { key: string; enabled: boolean }) => upsertFeatureFlag(input.key, input.enabled),
    onSuccess: async () => queryClient.invalidateQueries({ queryKey: ['admin', 'flags'] }),
  });

  const moderate = useMutation({
    mutationFn: (input: { reviewId: string; status: string }) =>
      moderateReview(input.reviewId, input.status, moderationReason || 'Moderated by admin'),
    onSuccess: async () => {
      setModerationReason('');
      await queryClient.invalidateQueries({ queryKey: ['admin', 'review-moderation'] });
    },
  });

  const toggleCampaign = useMutation({
    mutationFn: (input: { campaignId: string; isActive: boolean }) => setCampaignActive(input.campaignId, input.isActive),
    onSuccess: async () => queryClient.invalidateQueries({ queryKey: ['admin', 'growth-campaigns'] }),
  });

  const toggleCoupon = useMutation({
    mutationFn: (input: { couponId: string; isActive: boolean }) => setCouponActive(input.couponId, input.isActive),
    onSuccess: async () => queryClient.invalidateQueries({ queryKey: ['admin', 'growth-coupons'] }),
  });

  const createCoupon = useMutation({
    mutationFn: () => {
      const starts = new Date();
      const ends = new Date();
      ends.setDate(ends.getDate() + 90);
      return createAdminCoupon({
        code: couponCode.trim().toUpperCase(),
        name: couponName.trim() || couponCode.trim().toUpperCase(),
        discountType: 'Percent',
        discountValue: Number(couponValue) || 10,
        startsAtUtc: starts.toISOString(),
        endsAtUtc: ends.toISOString(),
        firstBookingOnly: true,
        newUserOnly: false,
        maxRedemptionsPerUser: 1,
      });
    },
    onSuccess: async () => {
      setCouponCode('');
      setCouponName('');
      setCouponValue('10');
      await queryClient.invalidateQueries({ queryKey: ['admin', 'growth-coupons'] });
      await queryClient.invalidateQueries({ queryKey: ['admin', 'growth-dashboard'] });
    },
  });

  const metrics = useMemo(() => dashboard.data as AdminDashboard | undefined, [dashboard.data]);
  const error =
    dashboard.error ??
    providers.error ??
    providerClinics.error ??
    finance.error ??
    tickets.error ??
    moderation.error ??
    moderate.error ??
    growth.error ??
    growthCampaigns.error ??
    growthCoupons.error ??
    settings.error ??
    flags.error ??
    audit.error ??
    resolveTicket.error ??
    saveSetting.error ??
    toggleFlag.error ??
    toggleCampaign.error ??
    toggleCoupon.error ??
    createCoupon.error;

  const onSelectSection = (id: string) => {
    goToSection(id as Section);
  };

  const showBack = section !== 'overview' || sectionHistory.length > 0;

  const filters = (
    <AdminPanel title={copy.adminFilters} description={copy.adminCenterBody}>
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md }}>
        <View style={{ flexGrow: 1, flexBasis: 180, minWidth: 160 }}>
          <TextField label={copy.state} value={stateFilter} onChangeText={setStateFilter} placeholder="Maharashtra" />
        </View>
        <View style={{ flexGrow: 1, flexBasis: 180, minWidth: 160 }}>
          <TextField label={copy.city} value={cityFilter} onChangeText={setCityFilter} placeholder="Mumbai" />
        </View>
      </View>
    </AdminPanel>
  );

  const content = (
    <View style={{ flex: 1, gap: spacing.lg }}>
      {section === 'overview' ? (
        <>
          {filters}
          {dashboard.isLoading ? <Skeleton /> : null}
          {metrics ? (
            <>
              <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md }}>
                <MetricCard
                  label={copy.adminPatients}
                  value={String(metrics.totalPatients)}
                  hint={copy.adminPatientGrowth}
                  icon={<AdminIconUsers size={16} color={theme.colors.primary} />}
                />
                <MetricCard
                  label={copy.adminVerifiedDoctors}
                  value={String(metrics.verifiedDoctors)}
                  hint={copy.adminProviderGrowth}
                  icon={<AdminIconProviders size={16} color={theme.colors.primary} />}
                />
                <MetricCard
                  label={copy.adminClinics}
                  value={`${metrics.totalClinics}`}
                  hint={`${metrics.totalBranches} ${copy.adminClinics}`}
                  icon={<AdminIconTenants size={16} color={theme.colors.primary} />}
                />
                <MetricCard
                  label={copy.adminAppointments}
                  value={String(metrics.totalAppointments)}
                  hint={`${metrics.completedAppointments} ${copy.adminCompleted}`}
                  icon={<AdminIconOps size={16} color={theme.colors.primary} />}
                />
                <MetricCard
                  label={copy.adminCompleted}
                  value={String(metrics.completedAppointments)}
                  hint={`${metrics.cancelledAppointments} ${copy.adminCancelled}`}
                  icon={<AdminIconChart size={16} color={theme.colors.primary} />}
                />
                <MetricCard
                  label={copy.adminPendingDoctors}
                  value={String(metrics.pendingDoctorApplications)}
                  hint={`${metrics.pendingClinicApprovals} ${copy.adminPendingClinics}`}
                  icon={<AdminIconShield size={16} color={theme.colors.primary} />}
                />
                <MetricCard
                  label={copy.commissionEarned}
                  value={`₹${metrics.commissionRevenue.toFixed(0)}`}
                  hint={copy.adminActualFinanceNote}
                  icon={<AdminIconFinance size={16} color={theme.colors.primary} />}
                />
                <MetricCard
                  label={copy.grossBookings}
                  value={`₹${metrics.grossBookingValue.toFixed(0)}`}
                  hint={`${copy.refunds}: ₹${metrics.refunds.toFixed(0)}`}
                  icon={<AdminIconGrowth size={16} color={theme.colors.primary} />}
                />
              </View>

              <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md }}>
                <BarChart title={copy.adminMonthlyAppointments} rows={metrics.monthlyAppointments} kind="count" />
                <BarChart title={copy.adminMonthlyRevenue} rows={metrics.monthlyRevenue} kind="amount" />
                <BarChart title={copy.adminPatientGrowth} rows={metrics.patientGrowth} kind="count" />
                <BarChart title={copy.adminProviderGrowth} rows={metrics.providerGrowth} kind="count" />
              </View>

              <AdminPanel title={copy.adminPendingDoctors} description={copy.reviews}>
                <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
                  <StatusBadge label={`${metrics.pendingDoctorApplications} doctors`} tone="warning" />
                  <StatusBadge label={`${metrics.pendingClinicApprovals} clinics`} tone="warning" />
                  <StatusBadge label={`${metrics.activeQueueEntries} ${copy.adminActiveQueues}`} tone="primary" />
                  <StatusBadge label={`₹${metrics.pendingSettlements.toFixed(0)} ${copy.pendingPayables}`} tone="neutral" />
                </View>
                {canVerify ? (
                  <Button
                    label={copy.reviews}
                    variant="secondary"
                    onPress={() => router.push('/reviews' as Href)}
                  />
                ) : null}
              </AdminPanel>
            </>
          ) : null}
        </>
      ) : null}

      {section === 'analytics' ? (
        canAnalytics ? (
          <>
            <Card>
              <AppText variant="label">{copy.biTitle}</AppText>
              <AppText variant="muted">{copy.biBody}</AppText>
              <TextField label={copy.biFrom} value={biFrom} onChangeText={setBiFrom} placeholder="2026-01-01" />
              <TextField label={copy.biTo} value={biTo} onChangeText={setBiTo} placeholder="2026-01-31" />
            </Card>
            {executive.isLoading || operational.isLoading || predictive.isLoading ? <Skeleton /> : null}
            {executive.data ? (
              <>
                <AppText variant="label">{copy.biExecutive}</AppText>
                <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md }}>
                  <MetricCard label={copy.adminPatients} value={String(executive.data.totalPatients)} />
                  <MetricCard label={copy.adminVerifiedDoctors} value={String(executive.data.verifiedDoctors)} />
                  <MetricCard label={copy.adminClinics} value={String(executive.data.totalClinics)} />
                  <MetricCard label={copy.biNewPatients} value={String(executive.data.newPatientsInRange)} />
                  <MetricCard label={copy.biNewDoctors} value={String(executive.data.newDoctorsInRange)} />
                  <MetricCard label={copy.adminAppointments} value={String(executive.data.appointmentsInRange)} />
                  <MetricCard label={copy.biCompletionRate} value={`${executive.data.completionRatePercent}%`} />
                  <MetricCard label={copy.biCancelRate} value={`${executive.data.cancellationRatePercent}%`} />
                  <MetricCard label={copy.biMissedRate} value={`${executive.data.missedRatePercent}%`} />
                  <MetricCard label={copy.grossBookings} value={`₹${executive.data.grossBookingValue.toFixed(0)}`} />
                  <MetricCard label={copy.commissionEarned} value={`₹${executive.data.commissionRevenue.toFixed(0)}`} />
                  <MetricCard label={copy.refunds} value={`₹${executive.data.refunds.toFixed(0)}`} />
                  <MetricCard label={copy.pendingPayables} value={`₹${executive.data.pendingPayouts.toFixed(0)}`} />
                  <MetricCard label={copy.biRetention} value={`${executive.data.patientRetentionPercent}%`} />
                  <MetricCard label={copy.biRepeatBookers} value={String(executive.data.repeatBookers)} />
                </View>
                <AppText variant="muted">{executive.data.dataFreshnessNote}</AppText>
                <BarChart title={copy.adminMonthlyAppointments} rows={executive.data.appointmentTrend} kind="count" />
                <BarChart title={copy.adminMonthlyRevenue} rows={executive.data.commissionTrend} kind="amount" />
                <BarChart title={copy.adminClinics} rows={executive.data.topClinicsByCompletions} kind="count" />
                <BarChart title={copy.doctor} rows={executive.data.topDoctorsByCompletions} kind="count" />
              </>
            ) : executive.isFetched ? (
              <AppText variant="muted">{copy.biEmpty}</AppText>
            ) : null}

            {operational.data ? (
              <>
                <AppText variant="label">{copy.biOperational}</AppText>
                <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md }}>
                  <MetricCard
                    label={copy.biAvgWait}
                    value={operational.data.averageWaitMinutes == null ? '—' : operational.data.averageWaitMinutes.toFixed(1)}
                  />
                  <MetricCard
                    label={copy.biAvgConsult}
                    value={
                      operational.data.averageConsultationMinutes == null
                        ? '—'
                        : operational.data.averageConsultationMinutes.toFixed(1)
                    }
                  />
                  <MetricCard label={copy.biSlotUtil} value={`${operational.data.slotUtilizationPercent}%`} />
                  <MetricCard label={copy.biWalkIn} value={String(operational.data.walkInBookings)} />
                  <MetricCard label={copy.biOnline} value={String(operational.data.onlineBookings)} />
                </View>
                <AppText variant="muted">{operational.data.methodologyNote}</AppText>
                <BarChart title={copy.biPeakHours} rows={operational.data.peakBookingHoursUtc} kind="count" />
                <BarChart title={copy.biDemandCity} rows={operational.data.demandByCity} kind="count" />
                <BarChart title={copy.biDemandSpec} rows={operational.data.demandBySpecialization} kind="count" />
                <Card>
                  <AppText variant="label">{copy.biDoctorUtil}</AppText>
                  {operational.data.doctorUtilization.length === 0 ? <AppText variant="muted">—</AppText> : null}
                  {operational.data.doctorUtilization.map((row) => (
                    <AppText key={row.doctorId}>
                      {row.doctorName}: {row.completedVisits}/{row.scheduledVisits} ({row.utilizationPercent}%)
                    </AppText>
                  ))}
                </Card>
                <Card>
                  <AppText variant="label">{copy.biBranchPerf}</AppText>
                  {operational.data.branchPerformance.length === 0 ? <AppText variant="muted">—</AppText> : null}
                  {operational.data.branchPerformance.map((row) => (
                    <AppText key={row.branchId}>
                      {row.clinicName} / {row.branchName}: {row.completions} done, {row.cancellations} cancelled
                      {row.averageWaitMinutes != null ? `, wait ${row.averageWaitMinutes.toFixed(1)}m` : ''}
                    </AppText>
                  ))}
                </Card>
              </>
            ) : null}

            {predictive.data ? (
              <>
                <AppText variant="label">{copy.biPredictive}</AppText>
                <Card>
                  <AppText style={{ color: theme.colors.warning }}>{copy.biForecastBadge}</AppText>
                  <AppText variant="muted">{predictive.data.disclaimer}</AppText>
                </Card>
                <Card>
                  <AppText variant="label">{copy.biForecastDemand}</AppText>
                  {predictive.data.demandForecastNext7Days.map((row) => (
                    <AppText key={row.label}>
                      {row.label}: {row.predictedValue} ({row.confidencePercent}% · {row.basis})
                    </AppText>
                  ))}
                </Card>
                <Card>
                  <AppText variant="label">{copy.biForecastClinic}</AppText>
                  {predictive.data.expectedVolumeByClinic.length === 0 ? <AppText variant="muted">—</AppText> : null}
                  {predictive.data.expectedVolumeByClinic.map((row) => (
                    <AppText key={row.label}>
                      {row.label}: {row.predictedValue} ({row.confidencePercent}%)
                    </AppText>
                  ))}
                </Card>
                <Card>
                  <AppText variant="label">{copy.biNoShowRisk}</AppText>
                  {predictive.data.noShowRiskByHourUtc.length === 0 ? <AppText variant="muted">—</AppText> : null}
                  {predictive.data.noShowRiskByHourUtc.map((row) => (
                    <AppText key={row.label}>
                      {row.label}: {row.ratePercent}% ({row.numerator}/{row.denominator})
                    </AppText>
                  ))}
                </Card>
                <Card>
                  <AppText variant="label">{copy.biAvailabilityHints}</AppText>
                  {predictive.data.availabilityPlanningHints.length === 0 ? <AppText variant="muted">—</AppText> : null}
                  {predictive.data.availabilityPlanningHints.map((row) => (
                    <AppText key={row.label}>
                      {row.label} ({row.count}%)
                    </AppText>
                  ))}
                </Card>
              </>
            ) : null}

            {canAnalyticsExport ? (
              <Card>
                <AppText variant="label">{copy.biExports}</AppText>
                <Button
                  label={copy.biExportExecutiveCsv}
                  variant="secondary"
                  onPress={() =>
                    Linking.openURL(analyticsExportUrl({ reportType: 'executive', format: 'csv', from: biFrom, to: biTo }))
                  }
                />
                <Button
                  label={copy.biExportOperationalCsv}
                  variant="secondary"
                  onPress={() =>
                    Linking.openURL(analyticsExportUrl({ reportType: 'operational', format: 'csv', from: biFrom, to: biTo }))
                  }
                />
                <Button
                  label={copy.biExportFinancialPdf}
                  variant="secondary"
                  onPress={() =>
                    Linking.openURL(analyticsExportUrl({ reportType: 'financial', format: 'pdf', from: biFrom, to: biTo }))
                  }
                />
              </Card>
            ) : null}
          </>
        ) : (
          <AppText variant="muted">{copy.biNoAccess}</AppText>
        )
      ) : null}

      {section === 'users-create' && canUsers ? (
        <AdminCreateUserPanel onActivated={() => goToSection('users-manage')} />
      ) : null}
      {section === 'users-manage' && canUsers ? <AdminManageUsersPanel /> : null}
      {section === 'clinics' && canUsers ? <AdminClinicsPanel /> : null}

      {section === 'providers' ? (
        <>
          <AdminPanel title={copy.doctor}>
            {(providers.data ?? []).map((item) => (
              <View
                key={item.id}
                style={{
                  flexDirection: 'row',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  gap: spacing.sm,
                  paddingVertical: 8,
                  borderBottomWidth: 1,
                  borderBottomColor: theme.colors.border,
                }}>
                <View style={{ flex: 1, gap: 2 }}>
                  <AppText>{item.name}</AppText>
                  <AppText variant="muted">
                    {item.city ? `${item.city}${item.state ? `, ${item.state}` : ''}` : item.state || '—'}
                  </AppText>
                </View>
                <StatusBadge
                  label={item.status}
                  tone={item.status === 'Approved' ? 'success' : item.status === 'Rejected' ? 'error' : 'warning'}
                />
              </View>
            ))}
            {providers.data?.length === 0 ? <AppText variant="muted">{copy.adminEmptyProviders}</AppText> : null}
          </AdminPanel>
          <AdminPanel title={copy.clinic}>
            {(providerClinics.data ?? []).map((item) => (
              <View
                key={item.id}
                style={{
                  flexDirection: 'row',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  gap: spacing.sm,
                  paddingVertical: 8,
                  borderBottomWidth: 1,
                  borderBottomColor: theme.colors.border,
                }}>
                <View style={{ flex: 1, gap: 2 }}>
                  <AppText>{item.name}</AppText>
                  <AppText variant="muted">{item.city ? `${item.city}` : '—'}</AppText>
                </View>
                <StatusBadge label={item.status} tone="primary" />
              </View>
            ))}
          </AdminPanel>
        </>
      ) : null}

      {section === 'verification' && canVerify ? (
        <AdminPanel title={copy.reviews} description={copy.adminPendingDoctors}>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
            <StatusBadge
              label={`${metrics?.pendingDoctorApplications ?? '—'} ${copy.adminPendingDoctors}`}
              tone="warning"
            />
            <StatusBadge
              label={`${metrics?.pendingClinicApprovals ?? '—'} ${copy.adminPendingClinics}`}
              tone="warning"
            />
          </View>
          <Button label={copy.reviews} onPress={() => router.push('/reviews' as Href)} />
        </AdminPanel>
      ) : null}

      {section === 'ops' ? (
        <AdminPanel title={copy.adminOps} description={copy.adminOpsBody}>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
            <Button label={copy.appointmentActivity} onPress={() => router.push('/activity' as Href)} />
            <Button label={copy.queueMonitor} variant="secondary" onPress={() => router.push('/queue-monitor' as Href)} />
            <Button label={copy.financeDashboard} variant="secondary" onPress={() => router.push('/finance' as Href)} />
          </View>
        </AdminPanel>
      ) : null}

      {section === 'finance' && canFinance ? (
        <>
          {filters}
          <AdminPanel
            title={copy.adminFinanceReports}
            description={copy.adminActualFinanceNote}
            action={
              <Button
                label={copy.adminExportCsv}
                variant="secondary"
                onPress={() => {
                  void Linking.openURL(financeCsvUrl({ state: stateFilter || undefined, city: cityFilter || undefined }));
                }}
              />
            }>
            {(finance.data ?? []).slice(0, 40).map((row) => (
              <View
                key={row.appointmentId}
                style={{
                  paddingVertical: 10,
                  borderBottomWidth: 1,
                  borderBottomColor: theme.colors.border,
                  gap: 2,
                }}>
                <AppText>{row.clinicName || row.clinicId}</AppText>
                <AppText variant="muted">
                  ₹{row.consultationFee.toFixed(0)} · commission ₹{row.commissionAmount.toFixed(0)}
                  {row.city ? ` · ${row.city}` : ''}
                </AppText>
              </View>
            ))}
            {finance.data?.length === 0 ? <AppText variant="muted">{copy.adminEmptyFinance}</AppText> : null}
          </AdminPanel>
        </>
      ) : null}

      {section === 'support' && canSupport ? (
        <>
          <Card>
            {(tickets.data ?? []).map((item) => (
              <Pressable key={item.id} onPress={() => setSelectedTicketId(item.id)}>
                <AppText>
                  {item.number} · {item.subject}
                </AppText>
                <AppText variant="muted">
                  {item.status} · {item.priority} · {item.category}
                </AppText>
              </Pressable>
            ))}
            {tickets.data?.length === 0 ? <AppText variant="muted">{copy.adminEmptyTickets}</AppText> : null}
          </Card>
          {ticket.data ? (
            <Card>
              <AppText variant="title">{ticket.data.subject}</AppText>
              <AppText variant="muted">{ticket.data.description}</AppText>
              {ticket.data.notes.map((note) => (
                <AppText key={note.id} variant="muted">
                  {note.isInternal ? '[internal] ' : ''}
                  {note.body}
                </AppText>
              ))}
              <TextField label={copy.adminInternalNote} value={ticketNote} onChangeText={setTicketNote} />
              <Button label={copy.adminResolveTicket} loading={resolveTicket.isPending} onPress={() => resolveTicket.mutate()} />
            </Card>
          ) : null}
        </>
      ) : null}

      {section === 'partners' && canPartners ? (
        <>
          <Card>
            <AppText variant="label">{copy.partnersAdmin}</AppText>
            <AppText variant="muted">{copy.partnersPortalBody}</AppText>
          </Card>
          {partnerStats.data ? (
            <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md }}>
              <MetricCard label="Pending" value={String(partnerStats.data.pendingPartners)} />
              <MetricCard label="Approved" value={String(partnerStats.data.approvedPartners)} />
              <MetricCard label="Suspended" value={String(partnerStats.data.suspendedPartners)} />
              <MetricCard label="Active apps" value={String(partnerStats.data.activeApps)} />
              <MetricCard label="Dead letters" value={String(partnerStats.data.deadLetterDeliveries)} />
              <MetricCard label="Requests today" value={String(partnerStats.data.requestsToday)} />
            </View>
          ) : null}
          {(partnerOrgs.data ?? []).map((partner) => (
            <Card key={partner.id}>
              <AppText>
                {partner.name} · {partner.status}
              </AppText>
              <AppText variant="muted">{partner.contactEmail}</AppText>
              <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm, marginTop: spacing.sm }}>
                <Button
                  label={copy.partnersApprove}
                  variant="secondary"
                  onPress={async () => {
                    await decidePartner(partner.id, 'approve');
                    await queryClient.invalidateQueries({ queryKey: ['admin', 'partners'] });
                  }}
                />
                <Button
                  label={copy.partnersReject}
                  variant="secondary"
                  onPress={async () => {
                    await decidePartner(partner.id, 'reject', 'Does not meet partner criteria');
                    await queryClient.invalidateQueries({ queryKey: ['admin', 'partners'] });
                  }}
                />
                <Button
                  label={copy.partnersSuspend}
                  variant="secondary"
                  onPress={async () => {
                    await decidePartner(partner.id, 'suspend', 'Suspended by platform admin');
                    await queryClient.invalidateQueries({ queryKey: ['admin', 'partners'] });
                  }}
                />
              </View>
            </Card>
          ))}
        </>
      ) : null}

      {section === 'automation' && canAutomation ? (
        <>
          <Card>
            <AppText variant="label">{copy.automationTitle}</AppText>
            <AppText variant="muted">{copy.automationBody}</AppText>
          </Card>
          {automationAnalytics.data ? (
            <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md }}>
              <MetricCard label={copy.automationEnabled} value={String(automationAnalytics.data.enabledWorkflows)} />
              <MetricCard label={copy.automationRuns} value={String(automationAnalytics.data.runsLast7Days)} />
              <MetricCard label="Failed" value={String(automationAnalytics.data.failedRunsLast7Days)} />
              <MetricCard label="Dead letters" value={String(automationAnalytics.data.deadLetters)} />
              <MetricCard label="Pending" value={String(automationAnalytics.data.pendingOutbox)} />
            </View>
          ) : null}
          <Card>
            <AppText variant="label">{copy.automationWorkflows}</AppText>
            {(workflows.data ?? []).map((wf) => (
              <Card key={wf.id}>
                <AppText>
                  {wf.name} · {wf.triggerType}
                </AppText>
                <AppText variant="muted">
                  {wf.isEnabled ? copy.automationEnabled : copy.automationDisabled} · {wf.actions.length} actions
                </AppText>
                <Button
                  label={wf.isEnabled ? copy.automationDisabled : copy.automationEnabled}
                  variant="secondary"
                  onPress={async () => {
                    await setWorkflowEnabled(wf.id, !wf.isEnabled);
                    await queryClient.invalidateQueries({ queryKey: ['automation'] });
                  }}
                />
              </Card>
            ))}
          </Card>
          <Card>
            <AppText variant="label">{copy.automationRuns}</AppText>
            {(automationRuns.data ?? []).slice(0, 12).map((run) => (
              <AppText key={run.id} variant="muted">
                {run.workflowName}: {run.status} · {run.triggerType}
                {run.failureDetails ? ` · ${run.failureDetails}` : ''}
              </AppText>
            ))}
          </Card>
          <Card>
            <AppText variant="label">{copy.automationOutbox}</AppText>
            {(automationOutbox.data ?? []).slice(0, 12).map((item) => (
              <AppText key={item.id} variant="muted">
                {item.eventType}: {item.status} · attempts {item.attemptCount}
                {item.deadLetterReason ? ` · ${item.deadLetterReason}` : ''}
              </AppText>
            ))}
          </Card>
        </>
      ) : null}

      {section === 'tenants' && canTenants ? (
        <>
          {tenants.isLoading ? <Skeleton /> : null}
          {tenants.data ? (
            <>
              <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md }}>
                <MetricCard label={copy.tenantsActive} value={String(tenants.data.activeTenants)} />
                <MetricCard label={copy.tenantsTrial} value={String(tenants.data.trialTenants)} />
                <MetricCard label={copy.tenantsPastDue} value={String(tenants.data.pastDueTenants)} />
                <MetricCard label={copy.tenantsSuspended} value={String(tenants.data.suspendedTenants)} />
                <MetricCard label={copy.tenantsSaasRevenue} value={`₹${tenants.data.saaSRevenueCollected.toFixed(0)}`} />
                <MetricCard
                  label={copy.tenantsCommissionRevenue}
                  value={`₹${tenants.data.appointmentCommissionRevenue.toFixed(0)}`}
                />
              </View>
              {(tenants.data.tenants ?? []).map((tenant) => (
                <Card key={tenant.organizationId}>
                  <AppText>
                    {tenant.name} · {tenant.planCode} · {tenant.subscriptionStatus}
                  </AppText>
                  <AppText variant="muted">
                    Clinics {tenant.clinicCount} · Members {tenant.memberCount} ·{' '}
                    {tenant.isActive ? 'active' : 'suspended'}
                  </AppText>
                  <Button
                    label={tenant.isActive ? copy.tenantsSuspend : copy.tenantsReactivate}
                    variant={tenant.isActive ? 'danger' : 'secondary'}
                    onPress={async () => {
                      await suspendTenant(tenant.organizationId, tenant.isActive);
                      await queryClient.invalidateQueries({ queryKey: ['admin', 'tenants'] });
                    }}
                  />
                </Card>
              ))}
            </>
          ) : null}
        </>
      ) : null}

      {section === 'config' && canConfig ? (
        <>
          <Card>
            <AppText variant="label">{copy.adminSettings}</AppText>
            {(settings.data ?? []).map((setting) => {
              const value = settingDrafts[setting.key] ?? setting.value;
              return (
                <View key={setting.id} style={{ gap: spacing.sm }}>
                  <TextField
                    label={setting.key}
                    value={value}
                    onChangeText={(next) => setSettingDrafts((prev) => ({ ...prev, [setting.key]: next }))}
                  />
                  {setting.description ? <AppText variant="muted">{setting.description}</AppText> : null}
                  <Button
                    label={copy.save}
                    variant="secondary"
                    loading={saveSetting.isPending}
                    onPress={() => saveSetting.mutate({ key: setting.key, value })}
                  />
                </View>
              );
            })}
          </Card>
          <Card>
            <AppText variant="label">{copy.adminFeatureFlags}</AppText>
            {(flags.data ?? []).map((flag) => (
              <View key={flag.id} style={{ gap: spacing.sm }}>
                <AppText>
                  {flag.key}: {flag.enabled ? copy.adminEnabled : copy.adminDisabled}
                </AppText>
                <Button
                  label={flag.enabled ? copy.adminDisable : copy.adminEnable}
                  variant="secondary"
                  loading={toggleFlag.isPending}
                  onPress={() => toggleFlag.mutate({ key: flag.key, enabled: !flag.enabled })}
                />
              </View>
            ))}
          </Card>
        </>
      ) : null}

      {section === 'moderation' && canModerateReviews ? (
        <Card>
          <TextField label={copy.reviewModerationReason} value={moderationReason} onChangeText={setModerationReason} />
          {(moderation.data ?? []).map((item) => (
            <Card key={item.id}>
              <AppText>
                {item.overallRating}★ · {item.targetKind} · {item.status}
              </AppText>
              {item.body ? <AppText variant="muted">{item.body}</AppText> : null}
              <AppText variant="muted">
                {copy.reviewReport}: {item.openReportCount}
              </AppText>
              <Button
                label={copy.reviewHide}
                variant="secondary"
                loading={moderate.isPending}
                onPress={() => moderate.mutate({ reviewId: item.id, status: 'Hidden' })}
              />
              <Button
                label={copy.reviewRestore}
                variant="secondary"
                loading={moderate.isPending}
                onPress={() => moderate.mutate({ reviewId: item.id, status: 'Published' })}
              />
              <Button
                label={copy.reviewRemove}
                variant="danger"
                loading={moderate.isPending}
                onPress={() => moderate.mutate({ reviewId: item.id, status: 'Removed' })}
              />
            </Card>
          ))}
          {moderation.data?.length === 0 ? <AppText variant="muted">{copy.reviewEmptyPublic}</AppText> : null}
        </Card>
      ) : null}

      {section === 'growth' && canGrowth ? (
        <>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md }}>
            <MetricCard label={copy.growthNewRegs} value={String(growth.data?.newRegistrations30d ?? 0)} />
            <MetricCard label={copy.growthReferralConv} value={String(growth.data?.referralConversions ?? 0)} />
            <MetricCard label={copy.growthCouponUse} value={String(growth.data?.couponsRedeemed30d ?? 0)} />
            <MetricCard label={copy.growthRewardLiability} value={String(growth.data?.outstandingRewardPoints ?? 0)} />
            <MetricCard label={copy.growthRetention} value={`${growth.data?.patientRetentionRate90d ?? 0}%`} />
            <MetricCard
              label={copy.growthAdmin}
              value={`₹${(growth.data?.outstandingRewardCreditsInr ?? 0).toFixed(0)}`}
            />
          </View>
          <Card>
            <AppText variant="label">Referral reward rules</AppText>
            <AppText variant="muted">
              Qualifying event: first paid non-walk-in booking by the referred patient. Registration alone does not credit
              rewards.
            </AppText>
            {!canConfig ? (
              <AppText variant="muted">Platform config permission is required to edit reward rules.</AppText>
            ) : (
              <>
                {(settings.data ?? [])
                  .filter((setting) => setting.key.startsWith('growth.'))
                  .map((setting) => {
                    const value = settingDrafts[setting.key] ?? setting.value;
                    return (
                      <View key={setting.id} style={{ gap: spacing.sm }}>
                        <TextField
                          label={setting.key}
                          value={value}
                          onChangeText={(next) => setSettingDrafts((prev) => ({ ...prev, [setting.key]: next }))}
                          keyboardType="number-pad"
                        />
                        {setting.description ? <AppText variant="muted">{setting.description}</AppText> : null}
                        <Button
                          label={copy.save}
                          variant="secondary"
                          loading={saveSetting.isPending}
                          onPress={() => saveSetting.mutate({ key: setting.key, value })}
                        />
                      </View>
                    );
                  })}
                {(settings.data ?? []).every((setting) => !setting.key.startsWith('growth.')) && !settings.isLoading ? (
                  <AppText variant="muted">Growth settings will appear after platform seed runs.</AppText>
                ) : null}
              </>
            )}
          </Card>
          <Card>
            <AppText variant="label">{copy.growthCampaigns}</AppText>
            {(growthCampaigns.data ?? []).map((campaign) => (
              <View key={campaign.id} style={{ gap: spacing.sm }}>
                <AppText>
                  {campaign.name} · {campaign.kind} · {campaign.deliveryCount}
                </AppText>
                <AppText variant="muted">{campaign.title}</AppText>
                <Button
                  label={campaign.isActive ? copy.adminDisable : copy.adminEnable}
                  variant="secondary"
                  loading={toggleCampaign.isPending}
                  onPress={() => toggleCampaign.mutate({ campaignId: campaign.id, isActive: !campaign.isActive })}
                />
              </View>
            ))}
          </Card>
          <Card>
            <AppText variant="label">{copy.growthTopCoupons}</AppText>
            <AppText variant="muted">
              Referrals invited {growth.data?.referralRegistrations ?? 0} · rewarded {growth.data?.referralRewardsPaid ?? 0}
            </AppText>
            <TextField label="New coupon code" value={couponCode} onChangeText={setCouponCode} autoCapitalize="characters" />
            <TextField label="Coupon name" value={couponName} onChangeText={setCouponName} />
            <TextField label="Percent off" value={couponValue} onChangeText={setCouponValue} keyboardType="number-pad" />
            <Button
              label="Create coupon"
              loading={createCoupon.isPending}
              onPress={() => createCoupon.mutate()}
              disabled={!couponCode.trim()}
            />
            {(growth.data?.topCoupons ?? []).map((row) => (
              <AppText key={row.code} variant="muted">
                {row.code} · {row.redemptions} · ₹{row.discountTotal.toFixed(0)}
              </AppText>
            ))}
            {(growthCoupons.data ?? []).map((coupon) => (
              <View key={coupon.id} style={{ gap: spacing.sm }}>
                <AppText>
                  {coupon.code} · {coupon.isActive ? copy.adminEnabled : copy.adminDisabled}
                </AppText>
                <Button
                  label={coupon.isActive ? copy.adminDisable : copy.adminEnable}
                  variant="secondary"
                  loading={toggleCoupon.isPending}
                  onPress={() => toggleCoupon.mutate({ couponId: coupon.id, isActive: !coupon.isActive })}
                />
              </View>
            ))}
          </Card>
        </>
      ) : null}

      {section === 'audit' && canAudit ? (
        <AdminPanel title={copy.adminAudit}>
          {(audit.data ?? []).map((entry) => (
            <View
              key={entry.id}
              style={{
                paddingVertical: 10,
                borderBottomWidth: 1,
                borderBottomColor: theme.colors.border,
                gap: 2,
              }}>
              <AppText variant="label">{entry.action}</AppText>
              <AppText variant="muted">{new Date(entry.occurredAtUtc).toLocaleString()}</AppText>
            </View>
          ))}
          {audit.data?.length === 0 ? <AppText variant="muted">{copy.adminEmptyAudit}</AppText> : null}
        </AdminPanel>
      ) : null}

      {error ? (
        <AdminPanel title={copy.adminCenter}>
          <AppText variant="muted" style={{ color: theme.colors.error }}>
            {problemMessage(error, copy.adminCenter)}
          </AppText>
        </AdminPanel>
      ) : null}
    </View>
  );

  return (
    <AdminShell
      title={sectionMeta[section].title}
      subtitle={sectionMeta[section].subtitle}
      activeId={section}
      items={nav}
      onSelect={onSelectSection}
      showBack={showBack}
      onBack={handleAdminBack}
      onLogout={() => signOut.mutate()}>
      {content}
    </AdminShell>
  );
}
