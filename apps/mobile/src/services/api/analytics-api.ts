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

export type AnalyticsNamedCount = { label: string; count: number };
export type AnalyticsNamedAmount = { label: string; amount: number };
export type AnalyticsNamedRate = { label: string; ratePercent: number; numerator: number; denominator: number };

export type ExecutiveAnalytics = {
  from: string;
  to: string;
  timeZoneNote: string;
  totalPatients: number;
  verifiedDoctors: number;
  totalClinics: number;
  newPatientsInRange: number;
  newDoctorsInRange: number;
  appointmentsInRange: number;
  completedAppointments: number;
  cancelledAppointments: number;
  expiredOrMissedAppointments: number;
  completionRatePercent: number;
  cancellationRatePercent: number;
  missedRatePercent: number;
  grossBookingValue: number;
  commissionRevenue: number;
  refunds: number;
  pendingPayouts: number;
  patientRetentionPercent: number;
  repeatBookers: number;
  appointmentTrend: AnalyticsNamedCount[];
  commissionTrend: AnalyticsNamedAmount[];
  topClinicsByCompletions: AnalyticsNamedCount[];
  topDoctorsByCompletions: AnalyticsNamedCount[];
  dataFreshnessNote: string;
};

export type DoctorUtilizationRow = {
  doctorId: string;
  doctorName: string;
  completedVisits: number;
  scheduledVisits: number;
  utilizationPercent: number;
};

export type BranchPerformanceRow = {
  branchId: string;
  clinicName: string;
  branchName: string;
  city: string | null;
  completions: number;
  cancellations: number;
  averageWaitMinutes: number | null;
};

export type OperationalAnalytics = {
  from: string;
  to: string;
  averageWaitMinutes: number | null;
  averageConsultationMinutes: number | null;
  slotUtilizationPercent: number;
  walkInBookings: number;
  onlineBookings: number;
  peakBookingHoursUtc: AnalyticsNamedCount[];
  demandByCity: AnalyticsNamedCount[];
  demandBySpecialization: AnalyticsNamedCount[];
  doctorUtilization: DoctorUtilizationRow[];
  branchPerformance: BranchPerformanceRow[];
  methodologyNote: string;
};

export type ForecastPoint = {
  label: string;
  predictedValue: number;
  confidencePercent: number;
  basis: string;
};

export type PredictiveInsights = {
  asOf: string;
  demandForecastNext7Days: ForecastPoint[];
  expectedVolumeByClinic: ForecastPoint[];
  noShowRiskByHourUtc: AnalyticsNamedRate[];
  availabilityPlanningHints: AnalyticsNamedCount[];
  disclaimer: string;
};

export async function getExecutiveAnalytics(params?: { from?: string; to?: string }) {
  return withAuthRetry(async () => (await api.get<ExecutiveAnalytics>('/api/v1/analytics/executive', { params })).data);
}

export async function getOperationalAnalytics(params?: { from?: string; to?: string }) {
  return withAuthRetry(async () => (await api.get<OperationalAnalytics>('/api/v1/analytics/operational', { params })).data);
}

export async function getPredictiveInsights() {
  return withAuthRetry(async () => (await api.get<PredictiveInsights>('/api/v1/analytics/predictive')).data);
}

export function analyticsExportUrl(params: { reportType: string; format: string; from?: string; to?: string }) {
  const query = new URLSearchParams();
  query.set('reportType', params.reportType);
  query.set('format', params.format);
  if (params.from) query.set('from', params.from);
  if (params.to) query.set('to', params.to);
  return `${apiBaseUrl}/api/v1/analytics/export?${query.toString()}`;
}
