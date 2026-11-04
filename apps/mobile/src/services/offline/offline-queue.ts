import AsyncStorage from '@react-native-async-storage/async-storage';

import { isOfflineActionBlocked } from '@/services/offline/cache-policy';

const QUEUE_KEY = '@prodoc/offline-queue/v1';

export type OfflineDraftJob = {
  id: string;
  kind: 'draft.review' | 'draft.assistant';
  createdAtUtc: string;
  payload: Record<string, unknown>;
  attempts: number;
  lastError?: string;
};

export async function listOfflineDrafts(): Promise<OfflineDraftJob[]> {
  const raw = await AsyncStorage.getItem(QUEUE_KEY);
  if (!raw) {
    return [];
  }
  try {
    return JSON.parse(raw) as OfflineDraftJob[];
  } catch {
    return [];
  }
}

async function save(jobs: OfflineDraftJob[]) {
  await AsyncStorage.setItem(QUEUE_KEY, JSON.stringify(jobs));
}

export async function enqueueOfflineDraft(
  kind: OfflineDraftJob['kind'],
  payload: Record<string, unknown>,
): Promise<OfflineDraftJob> {
  const jobs = await listOfflineDrafts();
  const job: OfflineDraftJob = {
    id: `${kind}-${Date.now()}`,
    kind,
    createdAtUtc: new Date().toISOString(),
    payload,
    attempts: 0,
  };
  await save([job, ...jobs].slice(0, 20));
  return job;
}

export async function removeOfflineDraft(id: string) {
  const jobs = await listOfflineDrafts();
  await save(jobs.filter((job) => job.id !== id));
}

export async function markOfflineDraftFailed(id: string, error: string) {
  const jobs = await listOfflineDrafts();
  await save(
    jobs.map((job) =>
      job.id === id ? { ...job, attempts: job.attempts + 1, lastError: error.slice(0, 200) } : job,
    ),
  );
}

/** Hard guard — bookings, payments, and clinical writes must never be queued as success. */
export function assertMutationAllowedOnline(action: string, isOnline: boolean) {
  if (!isOnline && isOfflineActionBlocked(action)) {
    throw new Error('offline_action_blocked');
  }
}
