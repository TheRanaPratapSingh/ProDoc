import type { QueryClient } from '@tanstack/react-query';

import { cachePolicies } from '@/services/offline/cache-policy';
import { readCache, writeCache } from '@/services/offline/secure-cache';

/** Persist safe read models for offline viewing. Mutations are never cached as confirmed. */
export async function hydrateOfflineQueryCache(client: QueryClient) {
  const pairs: Array<{ queryKey: unknown[]; cacheKey: string }> = [
    { queryKey: ['specializations'], cacheKey: cachePolicies.specializations.key },
    { queryKey: ['appointments', 'mine'], cacheKey: cachePolicies.appointmentsMine.key },
  ];

  for (const pair of pairs) {
    const envelope = await readCache<unknown>(pair.cacheKey);
    if (envelope) {
      client.setQueryData(pair.queryKey, envelope.payload);
    }
  }
}

export async function persistQuerySnapshot(queryKey: unknown[], cacheKey: keyof typeof cachePolicies, data: unknown) {
  const policy = cachePolicies[cacheKey];
  await writeCache(policy.key, policy.sensitivity, policy.ttlMs, data);
}

export function attachQueryPersistence(client: QueryClient) {
  return client.getQueryCache().subscribe((event) => {
    if (event.type !== 'updated' || event.query.state.status !== 'success') {
      return;
    }

    const key = event.query.queryKey;
    if (key[0] === 'specializations') {
      void persistQuerySnapshot(key, 'specializations', event.query.state.data);
    }
    if (key[0] === 'appointments' && key[1] === 'mine') {
      // Strip clinical notes — keep visit schedule metadata only.
      const rows = (event.query.state.data as Array<Record<string, unknown>> | undefined) ?? [];
      const safe = rows.slice(0, 30).map((row) => ({
        id: row.id,
        status: row.status,
        startsAtUtc: row.startsAtUtc,
        endsAtUtc: row.endsAtUtc,
        timeZoneId: row.timeZoneId,
        doctorName: row.doctorName,
        doctorSpecialization: row.doctorSpecialization,
        doctorVerified: row.doctorVerified,
        patientName: row.patientName,
        clinicName: row.clinicName,
        branchName: row.branchName,
        reference: row.reference,
        tokenLabel: row.tokenLabel,
        tokenNumber: row.tokenNumber,
        consultationFee: row.consultationFee,
        paymentMethod: row.paymentMethod,
        paymentStatus: row.paymentStatus,
      }));
      void persistQuerySnapshot(key, 'appointmentsMine', safe);
    }
    if (key[0] === 'discovery' && key[1] === 'doctors') {
      void writeCache(
        `${cachePolicies.discoveryDoctors.key}:${JSON.stringify(key[2] ?? {})}`,
        'public',
        cachePolicies.discoveryDoctors.ttlMs,
        event.query.state.data,
      );
    }
    if (key[0] === 'discovery' && key[1] === 'clinics') {
      void writeCache(
        `${cachePolicies.discoveryClinics.key}:${JSON.stringify(key[2] ?? {})}`,
        'public',
        cachePolicies.discoveryClinics.ttlMs,
        event.query.state.data,
      );
    }
  });
}

export async function readDiscoveryCache<T>(kind: 'doctors' | 'clinics', filters: unknown): Promise<T | null> {
  const policy = kind === 'doctors' ? cachePolicies.discoveryDoctors : cachePolicies.discoveryClinics;
  const envelope = await readCache<T>(`${policy.key}:${JSON.stringify(filters ?? {})}`);
  return envelope?.payload ?? null;
}
