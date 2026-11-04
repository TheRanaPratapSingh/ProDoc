import axios from 'axios';

import { api, apiBaseUrl, refreshSession } from '@/services/api/client';
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

export type NamedCount = { label: string; count: number };
export type NamedAmount = { label: string; amount: number };

export type AdminDashboard = {
  totalPatients: number;
  verifiedDoctors: number;
  totalClinics: number;
  totalBranches: number;
  pendingDoctorApplications: number;
  pendingClinicApprovals: number;
  totalAppointments: number;
  completedAppointments: number;
  cancelledAppointments: number;
  activeQueueEntries: number;
  grossBookingValue: number;
  commissionRevenue: number;
  pendingSettlements: number;
  refunds: number;
  monthlyAppointments: NamedCount[];
  monthlyRevenue: NamedAmount[];
  patientGrowth: NamedCount[];
  providerGrowth: NamedCount[];
};

export type AdminUser = {
  userId: string;
  phoneNumber: string;
  fullName: string | null;
  roles: string[];
  createdAtUtc: string;
  suspendedAtUtc: string | null;
  deletionRequestedAtUtc: string | null;
  doctorId: string | null;
  doctorStatus: string | null;
  ownedClinicId: string | null;
  ownedClinicStatus: string | null;
};

export type AuditEntry = {
  id: string;
  actorUserId: string | null;
  action: string;
  occurredAtUtc: string;
  ipAddress: string | null;
};

export type ProviderDirectoryItem = {
  id: string;
  kind: string;
  name: string;
  status: string;
  city: string | null;
  state: string | null;
  updatedAtUtc: string;
};

export type FinanceReportRow = {
  appointmentId: string;
  clinicId: string;
  clinicName: string | null;
  city: string | null;
  state: string | null;
  consultationFee: number;
  commissionAmount: number;
  clinicPayable: number;
  capturedAtUtc: string;
  isWalkInExcluded: boolean;
};

export type SupportTicketSummary = {
  id: string;
  number: string;
  category: string;
  priority: string;
  status: string;
  subject: string;
  createdByUserId: string;
  assignedToUserId: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  dueAtUtc?: string | null;
  escalatedAtUtc?: string | null;
  isOverdue?: boolean;
};

export type SupportTicketNote = {
  id: string;
  authorUserId: string;
  body: string;
  isInternal: boolean;
  createdAtUtc: string;
};

export type SupportTicketDetails = SupportTicketSummary & {
  description: string;
  appointmentId: string | null;
  paymentId: string | null;
  clinicId: string | null;
  resolvedAtUtc: string | null;
  notes: SupportTicketNote[];
};

export type PlatformSetting = {
  id: string;
  key: string;
  value: string;
  description: string | null;
  updatedAtUtc: string;
};

export type FeatureFlag = {
  id: string;
  key: string;
  enabled: boolean;
  description: string | null;
  updatedAtUtc: string;
};

export async function getAdminDashboard(params?: {
  from?: string;
  to?: string;
  state?: string;
  city?: string;
  clinicId?: string;
  appointmentStatus?: string;
}) {
  return withAuthRetry(async () => (await api.get<AdminDashboard>('/api/v1/admin/dashboard', { params })).data);
}

export async function searchAdminUsers(params?: { q?: string; role?: string; suspended?: boolean }) {
  return withAuthRetry(async () => (await api.get<AdminUser[]>('/api/v1/admin/users', { params })).data);
}

export async function suspendUser(userId: string, reason?: string) {
  return withAuthRetry(async () => {
    await api.post(`/api/v1/admin/users/${userId}/suspend`, { reason });
  });
}

export async function reactivateUser(userId: string) {
  return withAuthRetry(async () => {
    await api.post(`/api/v1/admin/users/${userId}/reactivate`);
  });
}

export async function grantUserRole(userId: string, roleName: string) {
  return withAuthRetry(async () => {
    await api.post(`/api/v1/admin/users/${userId}/roles/${encodeURIComponent(roleName)}`);
  });
}

export async function revokeUserRole(userId: string, roleName: string) {
  return withAuthRetry(async () => {
    await api.delete(`/api/v1/admin/users/${userId}/roles/${encodeURIComponent(roleName)}`);
  });
}

export async function listAudit(params?: { action?: string; actorUserId?: string }) {
  return withAuthRetry(async () => (await api.get<AuditEntry[]>('/api/v1/admin/audit', { params })).data);
}

export async function listAdminProviders(params?: { kind?: string; status?: string; city?: string; state?: string }) {
  return withAuthRetry(async () => (await api.get<ProviderDirectoryItem[]>('/api/v1/admin/providers', { params })).data);
}

export async function getFinanceReport(params?: { from?: string; to?: string; state?: string; city?: string; clinicId?: string }) {
  return withAuthRetry(async () => (await api.get<FinanceReportRow[]>('/api/v1/admin/finance/report', { params })).data);
}

export async function downloadFinanceCsv(params?: { from?: string; to?: string; state?: string; city?: string; clinicId?: string }) {
  return withAuthRetry(async () => {
    const response = await api.get<Blob>('/api/v1/admin/finance/export.csv', { params, responseType: 'blob' });
    return response.data;
  });
}

export function financeCsvUrl(params?: { from?: string; to?: string; state?: string; city?: string; clinicId?: string }) {
  const query = new URLSearchParams();
  if (params?.from) query.set('from', params.from);
  if (params?.to) query.set('to', params.to);
  if (params?.state) query.set('state', params.state);
  if (params?.city) query.set('city', params.city);
  if (params?.clinicId) query.set('clinicId', params.clinicId);
  const suffix = query.toString();
  return `${apiBaseUrl}/api/v1/admin/finance/export.csv${suffix ? `?${suffix}` : ''}`;
}

export async function listAdminTickets(status?: string) {
  return withAuthRetry(async () => (await api.get<SupportTicketSummary[]>('/api/v1/admin/support/tickets', { params: { status } })).data);
}

export async function getAdminTicket(ticketId: string) {
  return withAuthRetry(async () => (await api.get<SupportTicketDetails>(`/api/v1/admin/support/tickets/${ticketId}`)).data);
}

export async function updateAdminTicket(
  ticketId: string,
  body: { status?: string; priority?: string; assignedToUserId?: string; note?: string; internalNote?: boolean },
) {
  return withAuthRetry(async () => (await api.put<SupportTicketDetails>(`/api/v1/admin/support/tickets/${ticketId}`, body)).data);
}

export async function listPlatformSettings() {
  return withAuthRetry(async () => (await api.get<PlatformSetting[]>('/api/v1/admin/settings')).data);
}

export async function upsertPlatformSetting(key: string, value: string, description?: string) {
  return withAuthRetry(
    async () => (await api.put<PlatformSetting>(`/api/v1/admin/settings/${encodeURIComponent(key)}`, { value, description })).data,
  );
}

export async function listFeatureFlags() {
  return withAuthRetry(async () => (await api.get<FeatureFlag[]>('/api/v1/admin/feature-flags')).data);
}

export async function upsertFeatureFlag(key: string, enabled: boolean, description?: string) {
  return withAuthRetry(
    async () => (await api.put<FeatureFlag>(`/api/v1/admin/feature-flags/${encodeURIComponent(key)}`, { enabled, description })).data,
  );
}

export async function createSupportTicket(body: {
  category: string;
  priority: string;
  subject: string;
  description: string;
  appointmentId?: string;
  paymentId?: string;
  clinicId?: string;
}) {
  return withAuthRetry(async () => (await api.post<SupportTicketDetails>('/api/v1/support/tickets', body)).data);
}

export async function listMySupportTickets() {
  return withAuthRetry(async () => (await api.get<SupportTicketSummary[]>('/api/v1/support/tickets/mine')).data);
}

export async function getMySupportTicket(ticketId: string) {
  return withAuthRetry(async () => (await api.get<SupportTicketDetails>(`/api/v1/support/tickets/${ticketId}`)).data);
}

export async function addSupportTicketNote(ticketId: string, body: string) {
  return withAuthRetry(async () => (await api.post<SupportTicketDetails>(`/api/v1/support/tickets/${ticketId}/notes`, { body })).data);
}
