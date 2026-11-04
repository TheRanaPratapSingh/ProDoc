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

export type AssistantConsent = {
  recordsAccessAllowed: boolean;
  consentVersion: string;
  recordsConsentAtUtc: string | null;
  disclaimer: string;
  emergencyGuidance: string;
};

export type AssistantConversationSummary = {
  id: string;
  title: string;
  language: string;
  updatedAtUtc: string;
  hasPendingAction: boolean;
};

export type AssistantSourceRef = { kind: string; label: string; id: string | null };

export type AssistantDoctorRecommendation = {
  doctorId: string;
  scheduleId: string;
  displayName: string;
  specialization: string;
  clinicName: string | null;
  city: string | null;
  consultationFee: number;
  yearsOfExperience: number;
  languages: string[];
  distanceKm: number | null;
  explanation: string;
  ratingNote: string | null;
};

export type AssistantPendingAction = {
  actionId: string;
  type: string;
  summary: string;
  scheduleId: string | null;
  appointmentId: string | null;
  date: string | null;
  start: string | null;
  doctorName: string | null;
};

export type AssistantMessage = {
  id: string;
  role: string;
  content: string;
  sources: AssistantSourceRef[];
  createdAtUtc: string;
  toolName: string | null;
};

export type AssistantConversation = {
  id: string;
  title: string;
  language: string;
  promptVersion: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  pendingAction: AssistantPendingAction | null;
  messages: AssistantMessage[];
  recommendations: AssistantDoctorRecommendation[];
};

export type AssistantTurn = {
  conversationId: string;
  assistantMessage: AssistantMessage;
  pendingAction: AssistantPendingAction | null;
  recommendations: AssistantDoctorRecommendation[];
  isEmergency: boolean;
  disclaimer: string;
};

export type ConfirmAssistantAction = {
  conversationId: string;
  actionType: string;
  appointmentId: string | null;
  status: string;
  message: string;
};

export async function getAssistantConsent(language?: string) {
  return withAuthRetry(async () => (await api.get<AssistantConsent>('/api/v1/assistant/consent', { params: { language } })).data);
}

export async function setAssistantConsent(allowRecordsAccess: boolean, language?: string) {
  return withAuthRetry(
    async () => (await api.put<AssistantConsent>('/api/v1/assistant/consent', { allowRecordsAccess, language })).data,
  );
}

export async function listAssistantConversations() {
  return withAuthRetry(async () => (await api.get<AssistantConversationSummary[]>('/api/v1/assistant/conversations')).data);
}

export async function startAssistantConversation(language?: string) {
  return withAuthRetry(async () => (await api.post<AssistantConversation>('/api/v1/assistant/conversations', { language })).data);
}

export async function getAssistantConversation(conversationId: string) {
  return withAuthRetry(async () => (await api.get<AssistantConversation>(`/api/v1/assistant/conversations/${conversationId}`)).data);
}

export async function deleteAssistantConversation(conversationId: string) {
  return withAuthRetry(async () => {
    await api.delete(`/api/v1/assistant/conversations/${conversationId}`);
  });
}

export async function sendAssistantMessage(
  conversationId: string,
  body: { content: string; city?: string; latitude?: number; longitude?: number },
) {
  return withAuthRetry(
    async () => (await api.post<AssistantTurn>(`/api/v1/assistant/conversations/${conversationId}/messages`, body)).data,
  );
}

export async function confirmAssistantAction(conversationId: string, actionId: string) {
  return withAuthRetry(
    async () =>
      (await api.post<ConfirmAssistantAction>(`/api/v1/assistant/conversations/${conversationId}/actions/${actionId}/confirm`))
        .data,
  );
}

export async function getAssistantRecommendations(params?: { q?: string; city?: string; latitude?: number; longitude?: number }) {
  return withAuthRetry(
    async () => (await api.get<AssistantDoctorRecommendation[]>('/api/v1/assistant/recommendations', { params })).data,
  );
}
