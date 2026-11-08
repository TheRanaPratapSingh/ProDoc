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

export type SharedAppointmentSummary = {
  bookingReference: string;
  status: string;
  patientName: string;
  doctorName: string;
  specialty: string | null;
  clinicName: string;
  branchName: string | null;
  address: string | null;
  mapsUrl: string | null;
  startsAtUtc: string;
  endsAtUtc: string;
  timeZoneId: string;
  bookingMode: string;
  tokenLabel: string | null;
  tokenNumber: number | null;
  linkExpiresAtUtc: string | null;
};

export type AppointmentShareLinkDetails = {
  shareId: string;
  appointmentId: string;
  shareUrl: string;
  rawToken: string;
  createdAtUtc: string;
  expiresAtUtc: string;
  isActive: boolean;
};

export type AppointmentShareLinkStatus = {
  shareId: string;
  appointmentId: string;
  createdAtUtc: string;
  expiresAtUtc: string;
  revokedAtUtc: string | null;
  isActive: boolean;
  accessCount: number;
};

export async function getAppointmentSharePreview(appointmentId: string) {
  const response = await withAuthRetry(() =>
    api.get<SharedAppointmentSummary>(`/api/v1/appointments/${appointmentId}/share-preview`),
  );
  return response.data;
}

export async function listAppointmentShareLinks(appointmentId: string) {
  const response = await withAuthRetry(() =>
    api.get<AppointmentShareLinkStatus[]>(`/api/v1/appointments/${appointmentId}/share-links`),
  );
  return response.data;
}

export async function createAppointmentShareLink(appointmentId: string, lifetimeHours?: number) {
  const response = await withAuthRetry(() =>
    api.post<AppointmentShareLinkDetails>(`/api/v1/appointments/${appointmentId}/share-links`, {
      lifetimeHours: lifetimeHours ?? null,
    }),
  );
  return response.data;
}

export async function revokeAppointmentShareLink(appointmentId: string, shareId: string) {
  await withAuthRetry(() =>
    api.post(`/api/v1/appointments/${appointmentId}/share-links/${shareId}/revoke`),
  );
}

export async function revokeAllAppointmentShareLinks(appointmentId: string) {
  await withAuthRetry(() => api.post(`/api/v1/appointments/${appointmentId}/share-links/revoke-all`));
}

export async function getPublicSharedAppointment(token: string) {
  const response = await api.get<SharedAppointmentSummary>(
    `/api/v1/public/appointment-shares/${encodeURIComponent(token)}`,
  );
  return response.data;
}
