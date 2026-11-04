import axios from 'axios';

import { api, refreshSession } from '@/services/api/client';
import { persistSession, useSessionStore } from '@/stores/session-store';

async function withAuthRetry<T>(run: () => Promise<T>): Promise<T> {
  try {
    return await run();
  } catch (error) {
    if (!axios.isAxiosError(error) || error.response?.status !== 403) {
      throw error;
    }
    const refreshToken = useSessionStore.getState().refreshToken;
    if (!refreshToken) {
      throw error;
    }
    await persistSession(await refreshSession(refreshToken));
    return run();
  }
}

export type SaaSEntitlement = { featureCode: string; isEnabled: boolean; limitValue: number | null };

export type SaaSPlan = {
  id: string;
  code: string;
  name: string;
  description: string;
  monthlyPrice: number;
  annualPrice: number;
  currency: string;
  trialDays: number;
  gracePeriodDays: number;
  refundPolicyDays: number;
  isPublic: boolean;
  isActive: boolean;
  isCustom: boolean;
  sortOrder: number;
  entitlements: SaaSEntitlement[];
};

export type OrganizationBranding = {
  organizationId: string;
  organizationName: string;
  logoStorageKey: string | null;
  primaryColorHex: string;
  accentColorHex: string;
  loginHeadline: string | null;
  loginSubheadline: string | null;
  emailFromName: string | null;
  emailFooterText: string | null;
  notificationAccentHex: string | null;
  customDomain: string | null;
  subdomainSlug: string | null;
  whiteLabelEnabled: boolean;
  customDomainEnabled: boolean;
};

export type TenantUsage = {
  organizationId: string;
  clinicsUsed: number;
  clinicsLimit: number | null;
  branchesUsed: number;
  branchesLimit: number | null;
  doctorsUsed: number;
  doctorsLimit: number | null;
  staffUsed: number;
  staffLimit: number | null;
  storageBytesUsed: number;
  storageMbLimit: number | null;
  subscriptionStatus: string;
  planCode: string;
  periodEndUtc: string | null;
  gracePeriodEndsUtc: string | null;
  cancelAtPeriodEnd: boolean;
  effectiveEntitlements: SaaSEntitlement[];
};

export type TenantSubscription = {
  organizationId: string;
  planCode: string;
  planId: string | null;
  status: string;
  seatLimit: number;
  billingInterval: string;
  billingEmail: string | null;
  currentPeriodStartUtc: string | null;
  currentPeriodEndUtc: string | null;
  trialEndsUtc: string | null;
  gracePeriodEndsUtc: string | null;
  cancelAtPeriodEnd: boolean;
  pendingPlanCode: string | null;
  note: string;
};

export type SaaSCharge = {
  id: string;
  organizationId: string;
  invoiceId: string;
  status: string;
  interval: string;
  planCode: string;
  amount: number;
  currency: string;
  providerOrderId: string | null;
  checkoutPayload: string | null;
  periodStartUtc: string;
  periodEndUtc: string;
  createdAtUtc: string;
  paidAtUtc: string | null;
};

export type PlatformTenant = {
  organizationId: string;
  name: string;
  kind: string;
  isActive: boolean;
  planCode: string;
  subscriptionStatus: string;
  periodEndUtc: string | null;
  clinicCount: number;
  memberCount: number;
  saaSRevenueCollected: number;
};

export type PlatformSaaSMetrics = {
  activeTenants: number;
  trialTenants: number;
  pastDueTenants: number;
  suspendedTenants: number;
  saaSRevenueCollected: number;
  appointmentCommissionRevenue: number;
  tenants: PlatformTenant[];
};

export function listPublicPlans() {
  return withAuthRetry(async () => (await api.get<SaaSPlan[]>('/api/v1/saas/plans')).data);
}

export function getOrgBranding(organizationId: string) {
  return withAuthRetry(
    async () => (await api.get<OrganizationBranding>(`/api/v1/organizations/${organizationId}/branding`)).data,
  );
}

export function updateOrgBranding(
  organizationId: string,
  body: {
    primaryColorHex: string;
    accentColorHex: string;
    loginHeadline?: string;
    loginSubheadline?: string;
    emailFromName?: string;
    emailFooterText?: string;
    notificationAccentHex?: string;
    customDomain?: string;
    subdomainSlug?: string;
  },
) {
  return withAuthRetry(
    async () => (await api.put<OrganizationBranding>(`/api/v1/organizations/${organizationId}/branding`, body)).data,
  );
}

export function getOrgUsage(organizationId: string) {
  return withAuthRetry(async () => (await api.get<TenantUsage>(`/api/v1/organizations/${organizationId}/usage`)).data);
}

export function getSaasSubscription(organizationId: string) {
  return withAuthRetry(
    async () => (await api.get<TenantSubscription>(`/api/v1/organizations/${organizationId}/saas/subscription`)).data,
  );
}

export function changeSaasPlan(
  organizationId: string,
  body: { planCode: string; billingInterval: string; applyImmediately?: boolean },
) {
  return withAuthRetry(
    async () =>
      (await api.post<TenantSubscription>(`/api/v1/organizations/${organizationId}/saas/change-plan`, body)).data,
  );
}

export function cancelSaasSubscription(organizationId: string, atPeriodEnd = true) {
  return withAuthRetry(
    async () =>
      (await api.post<TenantSubscription>(`/api/v1/organizations/${organizationId}/saas/cancel`, { atPeriodEnd })).data,
  );
}

export function startSaasCharge(organizationId: string, idempotencyKey?: string) {
  return withAuthRetry(
    async () =>
      (await api.post<SaaSCharge>(`/api/v1/organizations/${organizationId}/saas/charges`, { idempotencyKey })).data,
  );
}

export function confirmSaasCharge(organizationId: string, chargeId: string, providerPaymentId?: string) {
  return withAuthRetry(
    async () =>
      (
        await api.post<SaaSCharge>(`/api/v1/organizations/${organizationId}/saas/charges/${chargeId}/confirm`, {
          providerPaymentId,
        })
      ).data,
  );
}

export function listSaasCharges(organizationId: string) {
  return withAuthRetry(
    async () => (await api.get<SaaSCharge[]>(`/api/v1/organizations/${organizationId}/saas/charges`)).data,
  );
}

export function getPlatformTenants() {
  return withAuthRetry(async () => (await api.get<PlatformSaaSMetrics>('/api/v1/admin/tenants')).data);
}

export function suspendTenant(organizationId: string, suspend = true) {
  return withAuthRetry(
    async () =>
      (await api.post<PlatformTenant>(`/api/v1/admin/tenants/${organizationId}/suspend`, { suspend })).data,
  );
}
