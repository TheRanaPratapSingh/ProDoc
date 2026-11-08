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
    if (!refreshToken) throw error;
    await persistSession(await refreshSession(refreshToken));
    return run();
  }
}

export type ReceptionDashboard = {
  clinicId: string;
  clinicName: string;
  branchId: string | null;
  branchName: string | null;
  appointments: {
    today: number;
    waiting: number;
    checkedIn: number;
    inConsult: number;
    completed: number;
    cancelled: number;
    noShow: number;
    walkIn: number;
  };
  queue: {
    waiting: number;
    called: number;
    inConsultation: number;
    completed: number;
    walkIns: number;
  };
  payments: {
    pending: number;
    paid: number;
    pendingAmount: number;
    collectedToday: number;
  };
  doctors: {
    doctorId: string;
    displayName: string;
    todayAppointments: number;
    waiting: number;
    inConsultation: number;
    next: ReceptionAppointment | null;
  }[];
  timeline: ReceptionAppointment[];
  nextPatient: ReceptionAppointment | null;
  notifications: ReceptionNotification[];
  hourlyBookings: { label: string; value: number }[];
};

export type ReceptionAppointment = {
  id: string;
  reference: string | null;
  status: string;
  patientName: string;
  doctorId: string;
  doctorName: string;
  clinicId: string;
  branchId: string;
  startsAtUtc: string;
  endsAtUtc: string;
  consultationFee: number;
  currency: string;
  paymentStatus: string;
  paidAmount: number;
  isWalkIn: boolean;
};

export type ReceptionNotification = {
  id: string;
  kind: string;
  title: string;
  body: string;
  createdAtUtc: string;
  isRead: boolean;
  subjectId: string | null;
  deepLink: string;
};

export type ReceptionPatientRow = {
  accountUserId: string | null;
  fullName: string;
  phoneMasked: string | null;
  visitCount: number;
  lastVisitAtUtc: string;
  lastStatus: string;
  lastAppointmentId: string;
  doctorId: string;
  doctorName: string;
};

export type ReceptionClinicInfo = {
  clinicId: string;
  name: string;
  phoneNumber: string | null;
  type: string | null;
  branches: {
    id: string;
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
    formattedAddress?: string | null;
    mapsUrl?: string | null;
    directionsUrl?: string | null;
    locationVerified?: boolean;
    isPrimary: boolean;
    isActive: boolean;
  }[];
  doctors: { id: string; displayName: string }[];
};

export type ReceptionReportSummary = {
  clinicId: string;
  from: string;
  to: string;
  appointments: number;
  walkIns: number;
  noShows: number;
  completed: number;
  cancelled: number;
  collected: number;
  byDoctor: {
    doctorId: string;
    doctorName: string;
    appointments: number;
    walkIns: number;
    noShows: number;
    completed: number;
    collected: number;
  }[];
};

export type ReceptionDeskPaymentRow = {
  appointmentId: string;
  reference: string;
  patientName: string;
  doctorName: string;
  fee: number;
  currency: string;
  paymentStatus: string;
  startsAtUtc: string;
};

export type ReceptionRecordsStatus = {
  appointmentId: string;
  hasConsultation: boolean;
  consultationStatus: string | null;
  prescriptionReady: boolean;
  prescriptionStatus: string | null;
  documentCount: number;
};

export async function getReceptionDashboard(clinicId: string, branchId?: string | null) {
  const response = await withAuthRetry(() =>
    api.get<ReceptionDashboard>('/api/v1/reception/dashboard', {
      params: { clinicId, branchId: branchId || undefined },
    }),
  );
  return response.data;
}

export async function searchReceptionPatients(clinicId: string, branchId?: string | null, q?: string) {
  const response = await withAuthRetry(() =>
    api.get<ReceptionPatientRow[]>('/api/v1/reception/patients', {
      params: { clinicId, branchId: branchId || undefined, q: q || undefined },
    }),
  );
  return response.data;
}

export async function registerReceptionPatient(input: { clinicId: string; fullName: string; phone: string }) {
  const response = await withAuthRetry(() =>
    api.post<{ accountUserId: string | null; fullName: string; phoneMasked: string | null }>('/api/v1/reception/patients', input),
  );
  return response.data;
}

export async function getReceptionClinicInfo(clinicId: string) {
  const response = await withAuthRetry(() => api.get<ReceptionClinicInfo>(`/api/v1/reception/clinics/${clinicId}`));
  return response.data;
}

export async function getReceptionReport(clinicId: string, from: string, to: string) {
  const response = await withAuthRetry(() =>
    api.get<ReceptionReportSummary>('/api/v1/reception/reports', { params: { clinicId, from, to } }),
  );
  return response.data;
}

export async function listReceptionPendingPayments(clinicId: string, branchId?: string | null) {
  const response = await withAuthRetry(() =>
    api.get<ReceptionDeskPaymentRow[]>('/api/v1/reception/payments/pending', {
      params: { clinicId, branchId: branchId || undefined },
    }),
  );
  return response.data;
}

export async function collectReceptionPayment(appointmentId: string) {
  const response = await withAuthRetry(() => api.post(`/api/v1/reception/appointments/${appointmentId}/collect`));
  return response.data;
}

export async function getReceptionRecordsStatus(appointmentId: string) {
  const response = await withAuthRetry(() =>
    api.get<ReceptionRecordsStatus>(`/api/v1/reception/appointments/${appointmentId}/records-status`),
  );
  return response.data;
}

export async function listReceptionNotifications() {
  const response = await withAuthRetry(() => api.get<ReceptionNotification[]>('/api/v1/reception/notifications'));
  return response.data;
}

export async function deskBookAppointment(input: {
  scheduleId: string;
  date: string;
  start: string;
  patientName: string;
  phone?: string;
  reason?: string;
  notes?: string;
}) {
  const response = await withAuthRetry(() => api.post<ReceptionAppointment>('/api/v1/appointments/desk-bookings', input));
  return response.data;
}
