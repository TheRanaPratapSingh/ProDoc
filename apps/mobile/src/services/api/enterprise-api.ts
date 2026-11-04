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

export type OrganizationDetails = {
  id: string;
  name: string;
  kind: string;
  isActive: boolean;
  createdAtUtc: string;
  clinicCount: number;
  memberCount: number;
};

export type OrganizationClinic = {
  clinicId: string;
  name: string | null;
  status: string;
  ownerUserId: string;
  branchCount: number;
};

export type OrganizationMember = {
  id: string;
  organizationId: string;
  userId: string;
  phoneNumber: string | null;
  role: string;
  clinicId: string | null;
  branchId: string | null;
  isActive: boolean;
};

export type OrganizationAnalytics = {
  appointmentVolume: number;
  completedAppointments: number;
  cancelledAppointments: number;
  noShowAppointments: number;
  uniquePatients: number;
  grossConsultationFees: number;
  commissionCaptured: number;
  cancellationRate: number;
  noShowRate: number;
  branches: Array<{
    branchId: string;
    branchName: string;
    clinicId: string;
    clinicName: string | null;
    appointments: number;
    completed: number;
    cancelled: number;
    noShows: number;
    grossFees: number;
    commission: number;
    averageWaitMinutes: number | null;
  }>;
  doctors: Array<{
    doctorId: string;
    doctorName: string;
    appointments: number;
    completed: number;
    grossFees: number;
  }>;
};

export type OrganizationSubscription = {
  organizationId: string;
  planCode: string;
  status: string;
  seatLimit: number;
  billingEmail: string | null;
  currentPeriodEndUtc: string | null;
  note: string;
};

export function listOrganizations() {
  return withAuthRetry(async () => (await api.get<OrganizationDetails[]>('/api/v1/organizations')).data);
}

export function createOrganization(name: string, kind: string) {
  return withAuthRetry(async () => (await api.post<OrganizationDetails>('/api/v1/organizations', { name, kind })).data);
}

export function listOrganizationClinics(organizationId: string) {
  return withAuthRetry(
    async () => (await api.get<OrganizationClinic[]>(`/api/v1/organizations/${organizationId}/clinics`)).data,
  );
}

export function linkClinic(organizationId: string, clinicId: string) {
  return withAuthRetry(
    async () => (await api.post<OrganizationClinic>(`/api/v1/organizations/${organizationId}/clinics`, { clinicId })).data,
  );
}

export function listOrganizationMembers(organizationId: string) {
  return withAuthRetry(
    async () => (await api.get<OrganizationMember[]>(`/api/v1/organizations/${organizationId}/members`)).data,
  );
}

export function inviteOrganizationMember(
  organizationId: string,
  input: { phoneNumber: string; role: string; clinicId?: string; branchId?: string },
) {
  return withAuthRetry(
    async () => (await api.post<OrganizationMember>(`/api/v1/organizations/${organizationId}/members`, input)).data,
  );
}

export function getOrganizationAnalytics(organizationId: string, from: string, to: string) {
  return withAuthRetry(
    async () =>
      (
        await api.get<OrganizationAnalytics>(`/api/v1/organizations/${organizationId}/analytics`, {
          params: { from, to },
        })
      ).data,
  );
}

export function getOrganizationSubscription(organizationId: string) {
  return withAuthRetry(
    async () => (await api.get<OrganizationSubscription>(`/api/v1/organizations/${organizationId}/subscription`)).data,
  );
}
