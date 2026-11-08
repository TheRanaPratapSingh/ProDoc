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

export async function searchAdminUsers(params?: {
  q?: string;
  role?: string;
  suspended?: boolean;
  clinicId?: string;
}) {
  return withAuthRetry(async () => (await api.get<AdminUser[]>('/api/v1/admin/users', { params })).data);
}

export type AdminUserClinicLink = {
  clinicId: string;
  clinicName: string;
  role: string;
  isActive: boolean;
};

export type AdminUserDoctorLink = {
  doctorId: string;
  doctorName: string;
  clinicId: string;
  clinicName: string;
};

export type AdminUserDetails = {
  userId: string;
  phoneNumber: string;
  fullName: string | null;
  email: string | null;
  roles: string[];
  createdAtUtc: string;
  suspendedAtUtc: string | null;
  deletionRequestedAtUtc: string | null;
  doctorId: string | null;
  doctorStatus: string | null;
  primarySpecialization: string | null;
  qualifications: string | null;
  registrationNumber: string | null;
  yearsOfExperience: number | null;
  clinics: AdminUserClinicLink[];
  assignedDoctors: AdminUserDoctorLink[];
};

export async function getAdminUser(userId: string) {
  return withAuthRetry(async () => (await api.get<AdminUserDetails>(`/api/v1/admin/users/${userId}`)).data);
}

export async function deleteAdminUser(userId: string) {
  return withAuthRetry(async () => {
    await api.delete(`/api/v1/admin/users/${userId}`);
  });
}

export type AdminClinicSummary = {
  id: string;
  reference: string;
  name: string | null;
  status: string;
  phoneNumber: string | null;
  email: string | null;
  city: string | null;
  state: string | null;
  doctorCount: number;
  staffCount: number;
  updatedAtUtc: string;
};

export type AdminClinicMember = {
  userId: string;
  doctorId: string | null;
  fullName: string;
  phoneNumber: string;
  role: string;
  designation: string | null;
  isActive: boolean;
};

export type AdminClinicBranch = {
  id: string;
  name: string;
  addressLine: string;
  addressLine2: string | null;
  landmark?: string | null;
  locality?: string | null;
  city: string;
  district?: string | null;
  state: string;
  postalCode: string;
  country?: string;
  latitude?: number | null;
  longitude?: number | null;
  googlePlaceId?: string | null;
  formattedAddress?: string | null;
  phoneNumber: string | null;
  isPrimary: boolean;
  isActive: boolean;
  locationVerified?: boolean;
};

export type AdminClinicDetails = {
  id: string;
  reference: string;
  name: string | null;
  type: string | null;
  status: string;
  phoneNumber: string | null;
  email: string | null;
  website: string | null;
  description: string | null;
  registrationNumber: string | null;
  ownerUserId: string;
  branches: AdminClinicBranch[];
  doctors: AdminClinicMember[];
  staff: AdminClinicMember[];
  createdAtUtc: string;
  updatedAtUtc: string;
};

export async function listAdminClinics(q?: string) {
  return withAuthRetry(async () => (await api.get<AdminClinicSummary[]>('/api/v1/admin/clinics', { params: { q } })).data);
}

export async function getAdminClinic(clinicId: string) {
  return withAuthRetry(async () => (await api.get<AdminClinicDetails>(`/api/v1/admin/clinics/${clinicId}`)).data);
}

export async function createAdminClinic(body: {
  name: string;
  type: string;
  phoneNumber: string;
  email?: string | null;
  website?: string | null;
  description?: string | null;
  registrationNumber?: string | null;
  branchName?: string | null;
  addressLine: string;
  addressLine2?: string | null;
  landmark?: string | null;
  locality?: string | null;
  city: string;
  district?: string | null;
  state: string;
  postalCode: string;
  country?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  googlePlaceId?: string | null;
  branchPhoneNumber?: string | null;
}) {
  return withAuthRetry(async () => (await api.post<AdminClinicDetails>('/api/v1/admin/clinics', body)).data);
}

export async function updateAdminClinicBranch(
  clinicId: string,
  branchId: string,
  body: {
    name: string;
    addressLine: string;
    addressLine2?: string | null;
    landmark?: string | null;
    locality?: string | null;
    city: string;
    district?: string | null;
    state: string;
    postalCode: string;
    country?: string | null;
    latitude?: number | null;
    longitude?: number | null;
    googlePlaceId?: string | null;
    phoneNumber?: string | null;
    isPrimary: boolean;
  },
) {
  return withAuthRetry(
    async () => (await api.put<AdminClinicDetails>(`/api/v1/admin/clinics/${clinicId}/branches/${branchId}`, body)).data,
  );
}

export async function updateAdminClinic(
  clinicId: string,
  body: {
    name: string;
    type: string;
    phoneNumber: string;
    email?: string | null;
    website?: string | null;
    description?: string | null;
    registrationNumber?: string | null;
  },
) {
  return withAuthRetry(async () => (await api.put<AdminClinicDetails>(`/api/v1/admin/clinics/${clinicId}`, body)).data);
}

export async function assignAdminClinicDoctor(clinicId: string, doctorId: string) {
  return withAuthRetry(async () => {
    await api.post(`/api/v1/admin/clinics/${clinicId}/doctors/${doctorId}`);
  });
}

/** Approve a doctor for patient discovery and ensure a published clinic schedule. */
export async function activateAdminDoctorDiscovery(doctorId: string) {
  return withAuthRetry(async () => {
    await api.post(`/api/v1/admin/doctors/${doctorId}/activate-discovery`);
  });
}

export async function removeAdminClinicDoctor(clinicId: string, doctorId: string) {
  return withAuthRetry(async () => {
    await api.delete(`/api/v1/admin/clinics/${clinicId}/doctors/${doctorId}`);
  });
}

export async function assignAdminClinicStaff(
  clinicId: string,
  body: { userId: string; roleName: string; branchId?: string | null; doctorIds?: string[] },
) {
  return withAuthRetry(async () => {
    await api.post(`/api/v1/admin/clinics/${clinicId}/staff`, body);
  });
}

export async function removeAdminClinicStaff(clinicId: string, userId: string) {
  return withAuthRetry(async () => {
    await api.delete(`/api/v1/admin/clinics/${clinicId}/staff/${userId}`);
  });
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

export type StaffInvitation = {
  id: string;
  phoneNumber: string;
  fullName: string;
  email: string | null;
  roleName: string;
  clinicId: string | null;
  branchId: string | null;
  status: string;
  createdAtUtc: string;
  expiresAtUtc: string;
  acceptedAtUtc: string | null;
  specializationId?: string | null;
  qualifications?: string | null;
  registrationNumber?: string | null;
  designation?: string | null;
  clinicIds?: string[] | null;
  doctorIds?: string[] | null;
  otpExpiresAtUtc?: string | null;
  developmentCode?: string | null;
  otpChallengeId?: string | null;
};

export type StaffInviteActivation = {
  invitationId: string;
  userId: string;
  fullName: string;
  phoneNumber: string;
  roleName: string;
  clinicIds: string[];
  doctorId?: string | null;
  doctorStatus?: string | null;
  isPubliclyDiscoverable?: boolean;
};

export async function listStaffInvitations(status?: string) {
  return withAuthRetry(
    async () => (await api.get<StaffInvitation[]>('/api/v1/admin/invitations', { params: { status } })).data,
  );
}

export async function inviteStaff(body: {
  fullName: string;
  phoneNumber: string;
  email?: string | null;
  roleName: string;
  clinicId?: string | null;
  branchId?: string | null;
  specializationId?: string | null;
  yearsOfExperience?: number | null;
  qualifications?: string | null;
  registrationNumber?: string | null;
  designation?: string | null;
  clinicIds?: string[];
  doctorIds?: string[];
}) {
  return withAuthRetry(async () => (await api.post<StaffInvitation>('/api/v1/admin/invitations', body)).data);
}

export async function resendStaffInvitation(invitationId: string) {
  return withAuthRetry(
    async () => (await api.post<StaffInvitation>(`/api/v1/admin/invitations/${invitationId}/resend`)).data,
  );
}

export async function verifyStaffInvitation(invitationId: string, code: string) {
  return withAuthRetry(
    async () =>
      (await api.post<StaffInviteActivation>(`/api/v1/admin/invitations/${invitationId}/verify`, { code })).data,
  );
}

export async function cancelStaffInvitation(invitationId: string) {
  return withAuthRetry(async () => {
    await api.post(`/api/v1/admin/invitations/${invitationId}/cancel`);
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
  issueType?: string;
  createdByRole?: string;
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
