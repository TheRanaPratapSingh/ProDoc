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

export type IntegrationProvider = {
  id: string;
  code: string;
  displayName: string;
  kind: string;
  mode: string;
  isActive: boolean;
  baseUrl: string | null;
  credentialReference: string | null;
  contractVersion: string;
  updatedAtUtc: string;
};

export type IntegrationConsent = {
  id: string;
  providerKind: string;
  purpose: string;
  scopeJson: string;
  status: string;
  grantedAtUtc: string;
  revokedAtUtc: string | null;
};

export type AbhaLink = {
  status: string;
  sandboxHandle: string | null;
  notes: string;
  updatedAtUtc: string;
  productionConnected: boolean;
};

export type IntegrationStatus = {
  providers: IntegrationProvider[];
  consents: IntegrationConsent[];
  abha: AbhaLink;
  openLabOrders: number;
  activePharmacyShares: number;
};

export type LabCatalogItem = {
  code: string;
  name: string;
  specimen: string;
  turnaround: string;
};

export type LabOrder = {
  id: string;
  providerCode: string;
  catalogCode: string;
  testName: string;
  status: string;
  externalOrderId: string | null;
  resultsArePreliminary: boolean;
  appointmentId: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type LabReport = {
  id: string;
  labOrderId: string;
  summary: string;
  resultJson: string;
  isPreliminary: boolean;
  disclaimer: string;
  reportedAtUtc: string;
};

export type PharmacyDirectoryItem = {
  code: string;
  name: string;
  city: string;
  supportsAvailability: boolean;
};

export type PharmacyShare = {
  id: string;
  prescriptionId: string;
  providerCode: string;
  pharmacyName: string;
  status: string;
  externalShareId: string | null;
  sharedAtUtc: string;
  updatedAtUtc: string;
};

export type AbdmReadiness = {
  officialOnboardingComplete: boolean;
  sandboxAvailable: boolean;
  message: string;
  requiredApprovals: string[];
};

export type FhirBundleExport = {
  resourceType: string;
  type: string;
  timestamp: string;
  entry: unknown[];
  disclaimer: string;
};

export function getIntegrationStatus() {
  return withAuthRetry(async () => (await api.get<IntegrationStatus>('/api/v1/integrations/status')).data);
}

export function listIntegrationProviders() {
  return withAuthRetry(async () => (await api.get<IntegrationProvider[]>('/api/v1/integrations/providers')).data);
}

export function grantIntegrationConsent(providerKind: string, purpose: string, scopeJson?: string) {
  return withAuthRetry(
    async () =>
      (await api.post<IntegrationConsent>('/api/v1/integrations/consents', { providerKind, purpose, scopeJson })).data,
  );
}

export function revokeIntegrationConsent(providerKind: string, purpose: string) {
  return withAuthRetry(
    async () =>
      (await api.post<IntegrationConsent>('/api/v1/integrations/consents/revoke', { providerKind, purpose })).data,
  );
}

export function exportFhirBundle(appointmentId?: string, prescriptionId?: string) {
  return withAuthRetry(
    async () =>
      (
        await api.post<FhirBundleExport>('/api/v1/integrations/fhir/export', {
          appointmentId: appointmentId ?? null,
          prescriptionId: prescriptionId ?? null,
        })
      ).data,
  );
}

export function getAbdmReadiness() {
  return withAuthRetry(async () => (await api.get<AbdmReadiness>('/api/v1/integrations/abdm/readiness')).data);
}

export function getAbhaLink() {
  return withAuthRetry(async () => (await api.get<AbhaLink>('/api/v1/integrations/abha')).data);
}

export function simulateAbhaSandbox(preferredHandle?: string) {
  return withAuthRetry(
    async () => (await api.post<AbhaLink>('/api/v1/integrations/abha/sandbox-link', { preferredHandle })).data,
  );
}

export function clearAbhaLink() {
  return withAuthRetry(async () => (await api.post<AbhaLink>('/api/v1/integrations/abha/clear')).data);
}

export function listLabCatalog(providerCode?: string) {
  return withAuthRetry(
    async () =>
      (await api.get<LabCatalogItem[]>('/api/v1/integrations/labs/catalog', { params: { providerCode } })).data,
  );
}

export function createLabOrder(input: {
  providerCode: string;
  catalogCode: string;
  appointmentId?: string;
  prescriptionId?: string;
  idempotencyKey: string;
}) {
  return withAuthRetry(async () => (await api.post<LabOrder>('/api/v1/integrations/labs/orders', input)).data);
}

export function listLabOrders() {
  return withAuthRetry(async () => (await api.get<LabOrder[]>('/api/v1/integrations/labs/orders')).data);
}

export function listLabReports() {
  return withAuthRetry(async () => (await api.get<LabReport[]>('/api/v1/integrations/labs/reports')).data);
}

export function listPharmacies(providerCode?: string, city?: string) {
  return withAuthRetry(
    async () =>
      (await api.get<PharmacyDirectoryItem[]>('/api/v1/integrations/pharmacies', { params: { providerCode, city } }))
        .data,
  );
}

export function listPharmacyShares() {
  return withAuthRetry(async () => (await api.get<PharmacyShare[]>('/api/v1/integrations/pharmacies/shares')).data);
}

export function configureIntegrationProvider(
  providerId: string,
  input: { mode: string; isActive: boolean; baseUrl?: string; credentialReference?: string },
) {
  return withAuthRetry(
    async () => (await api.put<IntegrationProvider>(`/api/v1/admin/integrations/providers/${providerId}`, input)).data,
  );
}
