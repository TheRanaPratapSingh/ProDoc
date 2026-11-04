export type CacheSensitivity = 'public' | 'personal' | 'clinical';

export type CachePolicy = {
  key: string;
  sensitivity: CacheSensitivity;
  /** Time-to-live in milliseconds. */
  ttlMs: number;
  /** Never treat offline writes as confirmed for these domains. */
  allowOfflineMutation: boolean;
};

export const cachePolicies = {
  discoveryDoctors: {
    key: 'discovery:doctors',
    sensitivity: 'public',
    ttlMs: 24 * 60 * 60 * 1000,
    allowOfflineMutation: false,
  },
  discoveryClinics: {
    key: 'discovery:clinics',
    sensitivity: 'public',
    ttlMs: 24 * 60 * 60 * 1000,
    allowOfflineMutation: false,
  },
  specializations: {
    key: 'discovery:specializations',
    sensitivity: 'public',
    ttlMs: 7 * 24 * 60 * 60 * 1000,
    allowOfflineMutation: false,
  },
  appointmentsMine: {
    key: 'appointments:mine',
    sensitivity: 'personal',
    ttlMs: 6 * 60 * 60 * 1000,
    allowOfflineMutation: false,
  },
  prescriptionsMine: {
    key: 'prescriptions:mine',
    sensitivity: 'clinical',
    ttlMs: 2 * 60 * 60 * 1000,
    allowOfflineMutation: false,
  },
  reviewDraft: {
    key: 'draft:review',
    sensitivity: 'personal',
    ttlMs: 7 * 24 * 60 * 60 * 1000,
    allowOfflineMutation: true,
  },
  assistantDraft: {
    key: 'draft:assistant',
    sensitivity: 'personal',
    ttlMs: 24 * 60 * 60 * 1000,
    allowOfflineMutation: true,
  },
} as const satisfies Record<string, CachePolicy>;

export const blockedOfflineActions = [
  'booking.confirm',
  'booking.hold',
  'payment.create',
  'payment.verify',
  'clinical.chart.update',
  'clinical.prescription.finalize',
  'queue.check_in',
] as const;

export type BlockedOfflineAction = (typeof blockedOfflineActions)[number];

export function isOfflineActionBlocked(action: string): action is BlockedOfflineAction {
  return (blockedOfflineActions as readonly string[]).includes(action);
}

export type CacheEnvelope<T> = {
  version: 1;
  sensitivity: CacheSensitivity;
  savedAtUtc: string;
  expiresAtUtc: string;
  payload: T;
};

export function isExpired(expiresAtUtc: string, now = Date.now()) {
  return new Date(expiresAtUtc).getTime() <= now;
}

export function makeEnvelope<T>(sensitivity: CacheSensitivity, ttlMs: number, payload: T, now = Date.now()): CacheEnvelope<T> {
  return {
    version: 1,
    sensitivity,
    savedAtUtc: new Date(now).toISOString(),
    expiresAtUtc: new Date(now + ttlMs).toISOString(),
    payload,
  };
}
