import axios from 'axios';

import { api, refreshSession } from '@/services/api/client';
import type { AppointmentDetails } from '@/services/api/provider-api';
import type { RatingSummary, ReviewDetails } from '@/services/api/review-api';
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

export type DoctorClinicOption = {
  clinicId: string;
  name: string;
  status: string;
  branchId: string | null;
  branchName: string | null;
  kind: string;
  isPrimary: boolean;
};

export type DoctorDashboard = {
  doctorId: string;
  displayName: string;
  photoDocumentId: string | null;
  verificationBadge: 'Verified' | 'Pending' | 'Rejected' | string;
  verificationStatus: string;
  profileCompletionPercent: number;
  clinicId: string | null;
  clinicName: string | null;
  appointments: {
    today: number;
    upcoming: number;
    completed: number;
    pending: number;
    cancelled: number;
    distinctPatients: number;
  };
  consultations: {
    inProgress: number;
    completedToday: number;
    withPrescriptionToday: number;
  };
  earnings: {
    paymentsEnabled: boolean;
    totalGross: number;
    todayGross: number;
    monthGross: number;
    commissionDeducted: number;
    netPayable: number;
    pendingSettlement: number;
  };
  rating: RatingSummary | null;
  clinics: DoctorClinicOption[];
  todayPreview: AppointmentDetails[];
};

export type DoctorEarningsDashboard = {
  doctorId: string;
  clinicId: string | null;
  paymentsEnabled: boolean;
  totalGross: number;
  todayGross: number;
  monthGross: number;
  commissionDeducted: number;
  netPayable: number;
  pendingSettlement: number;
  recentCommissions: {
    id: string;
    appointmentId: string;
    clinicId: string;
    consultationFee: number;
    commissionAmount: number;
    clinicPayableAmount: number;
    capturedAtUtc: string;
    settlementBatchId: string | null;
  }[];
  recentSettlements: {
    id: string;
    number: string;
    clinicId: string;
    netPayable: number;
    totalCommission: number;
    createdAtUtc: string;
    finalizedAtUtc: string | null;
  }[];
};

export type DoctorPatientRow = {
  accountUserId: string | null;
  patientName: string;
  visitCount: number;
  lastVisitAtUtc: string | null;
  lastStatus: string | null;
  lastAppointmentId: string | null;
};

export type DoctorPrescriptionRow = {
  appointmentId: string;
  appointmentReference: string | null;
  patientName: string;
  prescriptionId: string | null;
  prescriptionNumber: string | null;
  prescriptionStatus: string | null;
  diagnosis: string | null;
  startsAtUtc: string;
  clinicId: string;
  clinicName: string;
};

export type DoctorNotification = {
  id: string;
  kind: string;
  title: string;
  body: string;
  createdAtUtc: string;
  isRead: boolean;
  subjectId: string | null;
  deepLink: string;
};

function clinicParams(clinicId?: string | null) {
  return clinicId ? { clinicId } : undefined;
}

export function getDoctorDashboard(clinicId?: string | null) {
  return withAuthRetry(async () => (await api.get<DoctorDashboard>('/api/v1/doctors/me/dashboard', { params: clinicParams(clinicId) })).data);
}

export function listDoctorClinics() {
  return withAuthRetry(async () => (await api.get<DoctorClinicOption[]>('/api/v1/doctors/me/clinics')).data);
}

export function getDoctorEarnings(clinicId?: string | null) {
  return withAuthRetry(
    async () => (await api.get<DoctorEarningsDashboard>('/api/v1/doctors/me/earnings', { params: clinicParams(clinicId) })).data,
  );
}

export function listDoctorPatients(clinicId?: string | null) {
  return withAuthRetry(async () => (await api.get<DoctorPatientRow[]>('/api/v1/doctors/me/patients', { params: clinicParams(clinicId) })).data);
}

export function listDoctorPrescriptions(clinicId?: string | null) {
  return withAuthRetry(
    async () => (await api.get<DoctorPrescriptionRow[]>('/api/v1/doctors/me/prescriptions', { params: clinicParams(clinicId) })).data,
  );
}

export function listDoctorNotifications() {
  return withAuthRetry(async () => (await api.get<DoctorNotification[]>('/api/v1/doctors/me/notifications')).data);
}

export function listDoctorReviewsInbox() {
  return withAuthRetry(async () => (await api.get<ReviewDetails[]>('/api/v1/reviews/inbox')).data);
}
