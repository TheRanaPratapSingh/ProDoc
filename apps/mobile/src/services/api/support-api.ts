import axios from 'axios';

import { api, refreshSession } from '@/services/api/client';
import { persistSession, useSessionStore } from '@/stores/session-store';
import type { SupportTicketDetails, SupportTicketSummary } from '@/services/api/admin-api';

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

export type HelpCategory = {
  id: string;
  code: string;
  nameEn: string;
  nameHi: string;
  sortOrder: number;
  isActive: boolean;
};

export type HelpArticleSummary = {
  id: string;
  categoryId: string;
  categoryCode: string;
  slug: string;
  titleEn: string;
  titleHi: string;
  tags: string;
  isPublished: boolean;
  updatedAtUtc: string;
};

export type HelpArticleDetails = HelpArticleSummary & {
  bodyEn: string;
  bodyHi: string;
};

export type AgentWorkspace = {
  openCount: number;
  pendingCount: number;
  highPriorityCount: number;
  assignedToMeCount: number;
  overdueCount: number;
  tickets: SupportTicketSummary[];
};

export type DisputeCaseSummary = {
  id: string;
  number: string;
  supportTicketId: string;
  kind: string;
  status: string;
  decision: string;
  summary: string;
  assignedToUserId: string | null;
  linkedPaymentId: string | null;
  linkedAppointmentId: string | null;
  refundId: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  resolvedAtUtc: string | null;
};

export type DisputeCaseDetails = DisputeCaseSummary & {
  ticketNumber: string;
  evidenceRequest: string | null;
  decisionNotes: string | null;
  openedByUserId: string;
};

export type SupportAnalytics = {
  openTickets: number;
  resolvedLast30Days: number;
  escalatedOpen: number;
  openDisputes: number;
  averageFirstResponseHours: number;
  byCategory: { label: string; count: number }[];
  byStatus: { label: string; count: number }[];
};

export function listHelpCategories() {
  return withAuthRetry(async () => (await api.get<HelpCategory[]>('/api/v1/help/categories')).data);
}

export function searchHelpArticles(q?: string, category?: string) {
  return withAuthRetry(
    async () => (await api.get<HelpArticleSummary[]>('/api/v1/help/articles', { params: { q, category } })).data,
  );
}

export function getHelpArticle(slugOrId: string) {
  return withAuthRetry(async () => (await api.get<HelpArticleDetails>(`/api/v1/help/articles/${slugOrId}`)).data);
}

export function getAgentWorkspace(filter?: string, status?: string) {
  return withAuthRetry(
    async () =>
      (await api.get<AgentWorkspace>('/api/v1/admin/support/workspace', { params: { filter, status } })).data,
  );
}

export function manageSupportTicket(
  ticketId: string,
  body: {
    status?: string;
    priority?: string;
    assignedToUserId?: string;
    note?: string;
    internalNote?: boolean;
    resolutionDetails?: string;
    escalate?: boolean;
  },
) {
  return withAuthRetry(
    async () => (await api.put<SupportTicketDetails>(`/api/v1/admin/support/tickets/${ticketId}/manage`, body)).data,
  );
}

export function listDisputes(status?: string) {
  return withAuthRetry(
    async () => (await api.get<DisputeCaseSummary[]>('/api/v1/admin/support/disputes', { params: { status } })).data,
  );
}

export function openDispute(ticketId: string, kind: string, summary: string) {
  return withAuthRetry(
    async () =>
      (await api.post<DisputeCaseDetails>(`/api/v1/admin/support/tickets/${ticketId}/disputes`, { kind, summary }))
        .data,
  );
}

export function disputeAction(
  disputeId: string,
  body: {
    action: string;
    evidenceRequest?: string;
    decision?: string;
    decisionNotes?: string;
    refundAmount?: number;
  },
) {
  return withAuthRetry(
    async () => (await api.post<DisputeCaseDetails>(`/api/v1/admin/support/disputes/${disputeId}/actions`, body)).data,
  );
}

export function getSupportAnalytics() {
  return withAuthRetry(async () => (await api.get<SupportAnalytics>('/api/v1/admin/support/analytics')).data);
}

export type SupportIssueType = {
  code: string;
  labelEn: string;
  labelHi: string;
  ticketCategory: string;
  defaultPriority: string;
};

export type SupportTopic = {
  code: string;
  titleEn: string;
  titleHi: string;
  bodyEn: string;
  bodyHi: string;
  helpCategoryCode: string;
  issueTypes: SupportIssueType[];
  roles: string[];
};

export type SupportCatalog = {
  topics: SupportTopic[];
  supportEmail: string;
  supportPhone: string;
  liveAgentsAvailable: boolean;
};

export function getSupportCatalog() {
  return withAuthRetry(async () => (await api.get<SupportCatalog>('/api/v1/support/catalog')).data);
}

export function submitHelpArticleFeedback(slugOrId: string, wasHelpful: boolean) {
  return withAuthRetry(
    async () =>
      (await api.post<{ articleId: string; wasHelpful: boolean }>(`/api/v1/help/articles/${slugOrId}/feedback`, {
        wasHelpful,
      })).data,
  );
}

export function closeSupportTicket(ticketId: string) {
  return withAuthRetry(
    async () => (await api.post<SupportTicketDetails>(`/api/v1/support/tickets/${ticketId}/close`)).data,
  );
}

export function reopenSupportTicket(ticketId: string, body?: string) {
  return withAuthRetry(
    async () =>
      (await api.post<SupportTicketDetails>(`/api/v1/support/tickets/${ticketId}/reopen`, body ? { body } : {})).data,
  );
}
