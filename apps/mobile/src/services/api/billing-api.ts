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

export type PaymentDetails = {
  id: string;
  number: string;
  appointmentId: string;
  channel: string;
  status: string;
  amount: number;
  consultationFeeSnapshot: number;
  gatewayFeeEstimated: number;
  currency: string;
  provider: string;
  providerOrderId: string | null;
  checkoutPayload: string | null;
  paidAtUtc: string | null;
};

export type ClinicFinanceDashboard = {
  clinicId: string;
  grossEligibleFees: number;
  onlineCollected: number;
  payAtClinicCollected: number;
  commissionDeducted: number;
  refunds: number;
  pendingSettlement: number;
  netPayable: number;
  recentPayments: PaymentDetails[];
};

export type AdminFinanceDashboard = {
  grossPlatformBookingValue: number;
  onlineCollected: number;
  commissionEarned: number;
  refundsIssued: number;
  pendingClinicPayables: number;
  completedEligibleVisits: number;
};

export function createAppointmentPayment(
  appointmentId: string,
  paymentMethod: string,
  idempotencyKey: string,
  options?: { couponCode?: string; useRewardCredit?: boolean },
) {
  return withAuthRetry(async () =>
    (
      await api.post<PaymentDetails>(`/api/v1/appointments/${appointmentId}/payments`, {
        paymentMethod,
        idempotencyKey,
        couponCode: options?.couponCode || undefined,
        useRewardCredit: options?.useRewardCredit ?? false,
      })
    ).data,
  );
}

export function verifyPayment(paymentId: string, providerPaymentId?: string) {
  return withAuthRetry(async () =>
    (await api.post<PaymentDetails>(`/api/v1/payments/${paymentId}/verify`, { providerPaymentId })).data,
  );
}

export function recordClinicCollection(appointmentId: string) {
  return withAuthRetry(async () =>
    (await api.post<PaymentDetails>(`/api/v1/appointments/${appointmentId}/payments/clinic-collection`)).data,
  );
}

export function getClinicFinance(clinicId: string) {
  return withAuthRetry(async () => (await api.get<ClinicFinanceDashboard>(`/api/v1/clinics/${clinicId}/finance`)).data);
}

export function getAdminFinance() {
  return withAuthRetry(async () => (await api.get<AdminFinanceDashboard>('/api/v1/admin/finance')).data);
}

export function updateClinicPaymentSettings(
  clinicId: string,
  body: { policy: string; advancePercent: number; chargeCommissionOnWalkIns: boolean },
) {
  return withAuthRetry(async () => (await api.put(`/api/v1/clinics/${clinicId}/payment-settings`, body)).data);
}

export function getClinicPaymentSettings(clinicId: string) {
  return withAuthRetry(async () => (await api.get(`/api/v1/clinics/${clinicId}/payment-settings`)).data);
}
