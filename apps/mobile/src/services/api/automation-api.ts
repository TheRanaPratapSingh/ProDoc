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

export type AutomationCondition = { field: string; op: string; value: string };
export type AutomationAction = {
  type: string;
  titleTemplate?: string | null;
  bodyTemplate?: string | null;
  channelHint?: string | null;
  delayMinutes?: number | null;
  reminderEventType?: string | null;
};

export type AutomationWorkflow = {
  id: string;
  organizationId: string | null;
  name: string;
  triggerType: string;
  conditions: AutomationCondition[];
  actions: AutomationAction[];
  delayMinutes: number;
  isEnabled: boolean;
  updatedAtUtc: string;
};

export type AutomationRun = {
  id: string;
  workflowId: string;
  workflowName: string;
  triggerType: string;
  status: string;
  detailJson: string;
  failureDetails: string | null;
  startedAtUtc: string;
  completedAtUtc: string | null;
};

export type AutomationOutbox = {
  id: string;
  eventType: string;
  status: string;
  attemptCount: number;
  occurredAtUtc: string;
  availableAtUtc: string;
  processedAtUtc: string | null;
  lastError: string | null;
  deadLetterReason: string | null;
};

export type AutomationAnalytics = {
  enabledWorkflows: number;
  runsLast7Days: number;
  failedRunsLast7Days: number;
  deadLetters: number;
  pendingOutbox: number;
  runsByTrigger: { label: string; count: number }[];
};

export type NotificationPreferences = {
  inAppEnabled: boolean;
  emailEnabled: boolean;
  smsEnabled: boolean;
  pushEnabled: boolean;
  marketingEnabled: boolean;
  appointmentRemindersEnabled: boolean;
  queueUpdatesEnabled: boolean;
  updatedAtUtc: string;
};

export function getAutomationCatalog() {
  return withAuthRetry(async () => (await api.get<{ triggers: string[]; actions: string[]; conditionOperators: string[] }>('/api/v1/automation/catalog')).data);
}

export function listWorkflows(organizationId?: string) {
  return withAuthRetry(
    async () => (await api.get<AutomationWorkflow[]>('/api/v1/automation/workflows', { params: { organizationId } })).data,
  );
}

export function createWorkflow(body: {
  name: string;
  triggerType: string;
  conditions: AutomationCondition[];
  actions: AutomationAction[];
  delayMinutes?: number;
}) {
  return withAuthRetry(async () => (await api.post<AutomationWorkflow>('/api/v1/automation/workflows', body)).data);
}

export function setWorkflowEnabled(workflowId: string, enabled: boolean) {
  return withAuthRetry(
    async () => (await api.post<AutomationWorkflow>(`/api/v1/automation/workflows/${workflowId}/enabled`, { enabled })).data,
  );
}

export function listAutomationRuns(workflowId?: string) {
  return withAuthRetry(
    async () => (await api.get<AutomationRun[]>('/api/v1/automation/runs', { params: { workflowId } })).data,
  );
}

export function listAutomationOutbox(status?: string) {
  return withAuthRetry(
    async () => (await api.get<AutomationOutbox[]>('/api/v1/automation/outbox', { params: { status } })).data,
  );
}

export function getAutomationAnalytics() {
  return withAuthRetry(async () => (await api.get<AutomationAnalytics>('/api/v1/automation/analytics')).data);
}

export function getNotificationPreferences() {
  return withAuthRetry(async () => (await api.get<NotificationPreferences>('/api/v1/account/notification-preferences')).data);
}

export function updateNotificationPreferences(body: Omit<NotificationPreferences, 'updatedAtUtc'>) {
  return withAuthRetry(
    async () => (await api.put<NotificationPreferences>('/api/v1/account/notification-preferences', body)).data,
  );
}
