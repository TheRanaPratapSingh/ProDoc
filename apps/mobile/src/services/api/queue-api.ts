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

export type QueueEntry = {
  id: string;
  tokenLabel: string;
  tokenNumber: number;
  queuePosition: number;
  status: string;
  displayName: string;
  doctorId: string;
  doctorName: string;
  clinicId: string;
  clinicName: string;
  branchId: string;
  branchName: string;
  appointmentId: string | null;
  appointmentReference: string | null;
  isWalkIn: boolean;
  isLate: boolean;
  checkInAtUtc: string;
  appointmentStartsAtUtc: string | null;
  calledAtUtc: string | null;
  consultationStartedAtUtc: string | null;
  patientsAhead: number | null;
  estimatedWaitMinutes: number | null;
  statusMessage: string | null;
  updatedAtUtc: string;
  paymentStatus?: string | null;
  paymentMethod?: string | null;
};

export type QueueSummary = {
  totalToday: number;
  waiting: number;
  called: number;
  inConsultation: number;
  completed: number;
  walkIns: number;
  current: QueueEntry | null;
  next: QueueEntry | null;
  skipped?: number;
  paid?: number;
  unpaid?: number;
};

export type QueueDisplay = {
  clinicName: string;
  branchName: string;
  doctorName: string;
  nowConsulting: string | null;
  next: string | null;
  pleaseBeReady: string | null;
  status: string;
  updatedAtUtc: string;
};

export type CheckInToken = { appointmentId: string; token: string; expiresAtUtc: string };

export type AppointmentSearchItem = {
  id: string;
  reference: string | null;
  status: string;
  patientName: string;
  doctorId: string;
  doctorName: string;
  startsAtUtc: string;
  isWalkIn: boolean;
  tokenLabel: string | null;
};

export type DeskClinic = { id: string; name: string; branches: { id: string; name: string; isActive: boolean }[] };

export async function listDeskClinics() {
  const response = await withAuthRetry(() => api.get<DeskClinic[]>('/api/v1/clinics/desk'));
  return response.data;
}

export async function listDeskDoctors(clinicId: string, branchId: string) {
  const response = await withAuthRetry(() =>
    api.get<{ id: string; displayName: string; yearsOfExperience: number }[]>(
      `/api/v1/clinics/${clinicId}/branches/${branchId}/doctors`,
    ),
  );
  return response.data;
}

export async function mintCheckInQr(appointmentId: string) {
  const response = await withAuthRetry(() => api.post<CheckInToken>(`/api/v1/appointments/${appointmentId}/check-in/qr-token`));
  return response.data;
}

export async function checkInByQr(token: string) {
  const response = await withAuthRetry(() => api.post<QueueEntry>('/api/v1/check-ins/qr', { token }));
  return response.data;
}

export async function checkInAppointment(appointmentId: string) {
  const response = await withAuthRetry(() => api.post<QueueEntry>(`/api/v1/appointments/${appointmentId}/check-in`));
  return response.data;
}

export async function searchBranchAppointments(
  clinicId: string,
  branchId: string,
  params: { reference?: string; patientName?: string; phone?: string; doctorId?: string; date?: string },
) {
  const response = await withAuthRetry(() =>
    api.get<AppointmentSearchItem[]>(`/api/v1/clinics/${clinicId}/branches/${branchId}/appointments/search`, { params }),
  );
  return response.data;
}

export async function registerDeskWalkIn(
  clinicId: string,
  branchId: string,
  body: { doctorId: string; patientName: string; contactNumber?: string; reason?: string; consultationFee?: number },
) {
  const response = await withAuthRetry(() =>
    api.post<{ id: string; queueEntryId: string; tokenLabel: string; patientDisplayName: string }>(
      `/api/v1/clinics/${clinicId}/branches/${branchId}/walk-ins`,
      body,
    ),
  );
  return response.data;
}

export async function listBranchQueue(clinicId: string, branchId: string, date?: string) {
  const response = await withAuthRetry(() =>
    api.get<QueueEntry[]>(`/api/v1/clinics/${clinicId}/branches/${branchId}/queue`, {
      params: date ? { date } : undefined,
    }),
  );
  return response.data;
}

export async function listDoctorQueue(clinicId: string, branchId: string, doctorId: string) {
  const response = await withAuthRetry(() =>
    api.get<QueueEntry[]>(`/api/v1/clinics/${clinicId}/branches/${branchId}/doctors/${doctorId}/queue`),
  );
  return response.data;
}

export async function doctorQueueSummary(clinicId: string, branchId: string, doctorId: string) {
  const response = await withAuthRetry(() =>
    api.get<QueueSummary>(`/api/v1/clinics/${clinicId}/branches/${branchId}/doctors/${doctorId}/queue/summary`),
  );
  return response.data;
}

export async function queueDisplay(clinicId: string, branchId: string, doctorId: string) {
  const response = await withAuthRetry(() =>
    api.get<QueueDisplay>(`/api/v1/clinics/${clinicId}/branches/${branchId}/doctors/${doctorId}/queue/display`),
  );
  return response.data;
}

export async function callNext(clinicId: string, branchId: string, doctorId: string) {
  const response = await withAuthRetry(() =>
    api.post<QueueEntry | ''>(`/api/v1/clinics/${clinicId}/branches/${branchId}/doctors/${doctorId}/queue/call-next`),
  );
  return response.status === 204 ? null : response.data;
}

export async function queueAction(entryId: string, action: string, reason?: string) {
  const response = await withAuthRetry(() =>
    api.post<QueueEntry>(`/api/v1/queue-entries/${entryId}/${action}`, reason ? { reason } : {}),
  );
  return response.data;
}

export async function listMyQueue() {
  const response = await withAuthRetry(() => api.get<QueueEntry[]>('/api/v1/patients/me/queue'));
  return response.data;
}

export async function getQueueSettings(clinicId: string, branchId: string) {
  const response = await withAuthRetry(() =>
    api.get<{
      tokenPrefix: string;
      scheduledPatientPriority: boolean;
      earlyCheckInMinutes: number;
      lateCheckInMinutes: number;
      allowWalkIns: boolean;
      allowPriorityPatients: boolean;
    }>(`/api/v1/clinics/${clinicId}/branches/${branchId}/queue-settings`),
  );
  return response.data;
}

export async function saveQueueSettings(
  clinicId: string,
  branchId: string,
  body: {
    tokenPrefix: string;
    scheduledPatientPriority: boolean;
    earlyCheckInMinutes: number;
    lateCheckInMinutes: number;
    allowWalkIns: boolean;
    allowPriorityPatients: boolean;
  },
) {
  const response = await withAuthRetry(() => api.put(`/api/v1/clinics/${clinicId}/branches/${branchId}/queue-settings`, body));
  return response.data;
}

export async function listClinicStaff(clinicId: string) {
  const response = await withAuthRetry(() =>
    api.get<{ id: string; userId: string; phoneNumber: string | null; role: string; branchId: string | null; isActive: boolean }[]>(
      `/api/v1/clinics/${clinicId}/staff`,
    ),
  );
  return response.data;
}

export async function assignClinicStaff(clinicId: string, body: { phoneNumber: string; role: string; branchId?: string }) {
  const response = await withAuthRetry(() => api.post(`/api/v1/clinics/${clinicId}/staff`, body));
  return response.data;
}

export async function listAdminQueues(from: string, to: string) {
  const response = await withAuthRetry(() =>
    api.get<
      {
        clinicName: string;
        branchName: string;
        doctorName: string;
        operatingDate: string;
        waiting: number;
        inConsultation: number;
        completed: number;
        walkIns: number;
        skipped: number;
        averageWaitMinutes: number | null;
        averageConsultationMinutes: number | null;
      }[]
    >('/api/v1/admin/queues', { params: { from, to } }),
  );
  return response.data;
}
