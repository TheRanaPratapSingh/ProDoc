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

export type PartnerOrganization = {
  id: string;
  name: string;
  contactEmail: string;
  website: string | null;
  status: string;
  ownerUserId: string;
  reviewNote: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type PartnerApp = {
  id: string;
  partnerOrganizationId: string;
  name: string;
  clientId: string;
  scopes: string[];
  environment: string;
  isActive: boolean;
  rateLimitPerMinute: number;
  dailyQuota: number;
};

export type PartnerCredentialIssued = {
  credentialId: string;
  clientId: string;
  clientSecret: string;
  secretPrefix: string;
  createdAtUtc: string;
};

export type PartnerUsage = {
  partnerAppId: string;
  todayRequests: number;
  todayErrors: number;
  dailyQuota: number;
  rateLimitPerMinute: number;
  last7Days: { day: string; requestCount: number; errorCount: number }[];
};

export type PartnerPlatformStats = {
  pendingPartners: number;
  approvedPartners: number;
  suspendedPartners: number;
  activeApps: number;
  deadLetterDeliveries: number;
  requestsToday: number;
};

export type PartnerCatalog = {
  scopes: string[];
  webhookEvents: string[];
  errorCodes: { code: string; httpStatus: number; description: string }[];
};

export async function getPartnerCatalog() {
  return withAuthRetry(async () => (await api.get<PartnerCatalog>('/api/v1/partners/catalog')).data);
}

export async function getMyPartnerOrganization() {
  return withAuthRetry(async () => (await api.get<PartnerOrganization | null>('/api/v1/partners/me')).data);
}

export async function registerPartner(body: { name: string; contactEmail: string; website?: string | null }) {
  return withAuthRetry(async () => (await api.post<PartnerOrganization>('/api/v1/partners/register', body)).data);
}

export async function listPartnerApps(partnerOrganizationId: string) {
  return withAuthRetry(async () => (await api.get<PartnerApp[]>(`/api/v1/partners/${partnerOrganizationId}/apps`)).data);
}

export async function createPartnerApp(
  partnerOrganizationId: string,
  body: { name: string; scopes: string[]; environment: string },
) {
  return withAuthRetry(
    async () => (await api.post<PartnerApp>(`/api/v1/partners/${partnerOrganizationId}/apps`, body)).data,
  );
}

export async function issuePartnerCredential(appId: string) {
  return withAuthRetry(async () => (await api.post<PartnerCredentialIssued>(`/api/v1/partners/apps/${appId}/credentials`)).data);
}

export async function getPartnerUsage(appId: string) {
  return withAuthRetry(async () => (await api.get<PartnerUsage>(`/api/v1/partners/apps/${appId}/usage`)).data);
}

export async function listAdminPartners(status?: string) {
  return withAuthRetry(
    async () => (await api.get<PartnerOrganization[]>('/api/v1/admin/partners', { params: { status } })).data,
  );
}

export async function getPartnerPlatformStats() {
  return withAuthRetry(async () => (await api.get<PartnerPlatformStats>('/api/v1/admin/partners/stats')).data);
}

export async function decidePartner(partnerOrganizationId: string, decision: string, note?: string) {
  return withAuthRetry(
    async () =>
      (
        await api.post<PartnerOrganization>(`/api/v1/admin/partners/${partnerOrganizationId}/decision`, {
          decision,
          note,
        })
      ).data,
  );
}

export async function configurePartnerApp(
  appId: string,
  body: { scopes: string[]; rateLimitPerMinute: number; dailyQuota: number; isActive?: boolean | null },
) {
  return withAuthRetry(async () => (await api.put<PartnerApp>(`/api/v1/admin/partners/apps/${appId}`, body)).data);
}
