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

export type FollowUpReminderDetails = {
  id: string;
  reminderType: string;
  scheduledAtUtc: string;
  deliveryStatus: string;
  sentAtUtc: string | null;
  idempotencyKey: string;
};

export type FollowUpDetails = {
  id: string;
  patientUserId: string;
  familyMemberId: string | null;
  patientName: string;
  doctorId: string;
  doctorName: string;
  clinicId: string;
  clinicName: string;
  branchId: string;
  sourceAppointmentId: string | null;
  sourceConsultationId: string | null;
  recommendedDate: string;
  followUpNote: string | null;
  requiresBooking: boolean;
  status: string;
  displayStatus: string;
  linkedAppointmentId: string | null;
  hasBookedAppointment: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  cancelledAtUtc: string | null;
  completedAtUtc: string | null;
  timeZoneId: string;
  reminders: FollowUpReminderDetails[];
};

export type CreateFollowUpBody = {
  sourceAppointmentId?: string;
  sourceConsultationId?: string;
  patientUserId?: string;
  familyMemberId?: string;
  patientName?: string;
  clinicId?: string;
  branchId?: string;
  recommendedDate: string;
  followUpNote?: string;
  requiresBooking?: boolean;
};

export function createFollowUp(body: CreateFollowUpBody) {
  return withAuthRetry(async () => (await api.post<FollowUpDetails>('/api/v1/follow-ups', body)).data);
}

export function updateFollowUp(
  followUpId: string,
  body: { recommendedDate: string; followUpNote?: string; requiresBooking?: boolean },
) {
  return withAuthRetry(async () => (await api.put<FollowUpDetails>(`/api/v1/follow-ups/${followUpId}`, body)).data);
}

export function cancelFollowUp(followUpId: string) {
  return withAuthRetry(async () => (await api.post<FollowUpDetails>(`/api/v1/follow-ups/${followUpId}/cancellation`)).data);
}

export function completeFollowUp(followUpId: string) {
  return withAuthRetry(async () => (await api.post<FollowUpDetails>(`/api/v1/follow-ups/${followUpId}/completion`)).data);
}

export function listDoctorFollowUps(params?: {
  from?: string;
  to?: string;
  patient?: string;
  status?: string;
  clinicId?: string;
  take?: number;
}) {
  return withAuthRetry(
    async () => (await api.get<FollowUpDetails[]>('/api/v1/follow-ups/doctor', { params })).data,
  );
}

export function listMyFollowUps() {
  return withAuthRetry(async () => (await api.get<FollowUpDetails[]>('/api/v1/follow-ups/me')).data);
}

export function listReceptionFollowUps(params: {
  clinicId: string;
  from?: string;
  to?: string;
  patient?: string;
  status?: string;
  doctorId?: string;
  take?: number;
}) {
  return withAuthRetry(
    async () => (await api.get<FollowUpDetails[]>('/api/v1/follow-ups/reception', { params })).data,
  );
}

export function linkFollowUpAppointment(followUpId: string, appointmentId: string) {
  return withAuthRetry(async () => {
    await api.post(`/api/v1/follow-ups/${followUpId}/link-appointment`, { appointmentId });
  });
}

/** Add days to an ISO date (yyyy-MM-dd) in local calendar math. */
export function addDaysIso(isoDate: string, days: number): string {
  const [y, m, d] = isoDate.split('-').map(Number);
  const date = new Date(y!, (m ?? 1) - 1, d ?? 1);
  date.setDate(date.getDate() + days);
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

export function formatFollowUpDate(isoDate: string): string {
  const [y, m, d] = isoDate.split('-').map(Number);
  const date = new Date(y!, (m ?? 1) - 1, d ?? 1);
  return date.toLocaleDateString('en-IN', { day: 'numeric', month: 'long', year: 'numeric' });
}
