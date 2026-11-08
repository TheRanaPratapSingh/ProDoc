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

export type DoctorAvailabilitySummary = {
  isAvailableToday: boolean;
  status: string;
  startsAt: string | null;
  endsAt: string | null;
  breakStartsAt: string | null;
  breakEndsAt: string | null;
  closureReason: string | null;
  scheduleId: string | null;
  slotDurationMinutes: number;
  closureId: string | null;
  timingsLabel: string | null;
};

export type DoctorDashboard = {
  doctorId: string;
  displayName: string;
  specializationName: string | null;
  photoDocumentId: string | null;
  verificationBadge: 'Pending' | 'Verified' | 'Rejected' | string;
  verificationStatus: string;
  profileCompletionPercent: number;
  clinicId: string | null;
  clinicName: string | null;
  unreadNotifications: number;
  appointments: {
    today: number;
    upcoming: number;
    completed: number;
    pending: number;
    cancelled: number;
    waiting: number;
    inConsultation: number;
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
  availability: DoctorAvailabilitySummary | null;
  rating: RatingSummary | null;
  clinics: DoctorClinicOption[];
  todayPreview: AppointmentDetails[];
  nextPatient: AppointmentDetails | null;
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
    doctorId: string;
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
    totalCommission: number;
    netPayable: number;
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

export function getDoctorDashboard(clinicId?: string | null) {
  return withAuthRetry(async () =>
    (await api.get<DoctorDashboard>('/api/v1/doctors/me/dashboard', { params: clinicId ? { clinicId } : undefined })).data,
  );
}

export function listDoctorClinics() {
  return withAuthRetry(async () => (await api.get<DoctorClinicOption[]>('/api/v1/doctors/me/clinics')).data);
}

export function getDoctorEarnings(clinicId?: string | null) {
  return withAuthRetry(async () =>
    (await api.get<DoctorEarningsDashboard>('/api/v1/doctors/me/earnings', { params: clinicId ? { clinicId } : undefined })).data,
  );
}

export function listDoctorPatients(clinicId?: string | null) {
  return withAuthRetry(async () =>
    (await api.get<DoctorPatientRow[]>('/api/v1/doctors/me/patients', { params: clinicId ? { clinicId } : undefined })).data,
  );
}

export function listDoctorPrescriptions(clinicId?: string | null) {
  return withAuthRetry(async () =>
    (await api.get<DoctorPrescriptionRow[]>('/api/v1/doctors/me/prescriptions', { params: clinicId ? { clinicId } : undefined })).data,
  );
}

export function listDoctorNotifications() {
  return withAuthRetry(async () => (await api.get<DoctorNotification[]>('/api/v1/doctors/me/notifications')).data);
}

export function listPracticeAppointmentsScoped(from: string, to: string, clinicId?: string | null, assignedOnly = true) {
  return withAuthRetry(async () =>
    (
      await api.get<AppointmentDetails[]>('/api/v1/appointments/practice', {
        params: {
          from,
          to,
          assignedOnly,
          ...(clinicId ? { clinicId } : {}),
        },
      })
    ).data,
  );
}

export type { ReviewDetails };
