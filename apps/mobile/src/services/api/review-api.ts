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

export type RatingBucket = { stars: number; count: number };

export type RatingSummary = {
  publishedReviewCount: number;
  averageOverall: number | null;
  averageIsPublic: boolean;
  displayNote: string;
  distribution: RatingBucket[];
  averageCommunication: number | null;
  averageCleanliness: number | null;
  averageStaffBehaviour: number | null;
  averageWaitingTime: number | null;
};

export type ReviewResponse = {
  id: string;
  responderKind: string;
  body: string;
  editCount: number;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type ReviewDetails = {
  id: string;
  appointmentId: string;
  targetKind: string;
  doctorId: string | null;
  clinicId: string | null;
  overallRating: number;
  communicationRating: number | null;
  cleanlinessRating: number | null;
  staffBehaviourRating: number | null;
  waitingTimeRating: number | null;
  body: string | null;
  isVerifiedVisit: boolean;
  status: string;
  createdAtUtc: string;
  editedAtUtc: string | null;
  canEdit: boolean;
  responses: ReviewResponse[];
};

export type EligibleReviewTarget = {
  appointmentId: string;
  appointmentReference: string;
  doctorId: string;
  doctorName: string;
  clinicId: string;
  clinicName: string;
  completedAtUtc: string;
  canReviewDoctor: boolean;
  canReviewClinic: boolean;
};

export type ReviewModerationItem = {
  id: string;
  appointmentId: string;
  targetKind: string;
  doctorId: string | null;
  clinicId: string | null;
  overallRating: number;
  body: string | null;
  status: string;
  openReportCount: number;
  createdAtUtc: string;
};

export async function listEligibleReviews() {
  return withAuthRetry(async () => (await api.get<EligibleReviewTarget[]>('/api/v1/reviews/eligible')).data);
}

export async function createReview(body: {
  appointmentId: string;
  targetKind: string;
  overallRating: number;
  communicationRating?: number;
  cleanlinessRating?: number;
  staffBehaviourRating?: number;
  waitingTimeRating?: number;
  body?: string;
}) {
  return withAuthRetry(async () => (await api.post<ReviewDetails>('/api/v1/reviews', body)).data);
}

export async function updateReview(
  reviewId: string,
  body: {
    overallRating: number;
    communicationRating?: number;
    cleanlinessRating?: number;
    staffBehaviourRating?: number;
    waitingTimeRating?: number;
    body?: string;
  },
) {
  return withAuthRetry(async () => (await api.put<ReviewDetails>(`/api/v1/reviews/${reviewId}`, body)).data);
}

export async function listDoctorReviews(doctorId: string, page = 1) {
  return withAuthRetry(async () => (await api.get<ReviewDetails[]>(`/api/v1/doctors/${doctorId}/reviews`, { params: { page } })).data);
}

export async function listClinicReviews(clinicId: string, page = 1) {
  return withAuthRetry(async () => (await api.get<ReviewDetails[]>(`/api/v1/clinics/${clinicId}/reviews`, { params: { page } })).data);
}

export async function getDoctorRatingSummary(doctorId: string) {
  return withAuthRetry(async () => (await api.get<RatingSummary>(`/api/v1/doctors/${doctorId}/rating-summary`)).data);
}

export async function getClinicRatingSummary(clinicId: string) {
  return withAuthRetry(async () => (await api.get<RatingSummary>(`/api/v1/clinics/${clinicId}/rating-summary`)).data);
}

export async function listProviderReviewInbox() {
  return withAuthRetry(async () => (await api.get<ReviewDetails[]>('/api/v1/reviews/inbox')).data);
}

export async function respondToReview(reviewId: string, body: string) {
  return withAuthRetry(async () => (await api.post<ReviewDetails>(`/api/v1/reviews/${reviewId}/responses`, { body })).data);
}

export async function reportReview(reviewId: string, reason: string) {
  return withAuthRetry(async () => {
    await api.post(`/api/v1/reviews/${reviewId}/reports`, { reason });
  });
}

export async function listModerationReviews(status?: string) {
  return withAuthRetry(async () => (await api.get<ReviewModerationItem[]>('/api/v1/admin/reviews', { params: { status } })).data);
}

export async function moderateReview(reviewId: string, status: string, reason: string) {
  return withAuthRetry(async () => (await api.post<ReviewDetails>(`/api/v1/admin/reviews/${reviewId}/moderate`, { status, reason })).data);
}

export async function submitFeedback(body: { category: string; rating?: number; body: string; appointmentId?: string }) {
  return withAuthRetry(async () => (await api.post('/api/v1/feedback', body)).data);
}
