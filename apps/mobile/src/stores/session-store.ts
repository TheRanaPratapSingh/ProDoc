import { create } from 'zustand';

import { refreshSession } from '@/services/api/client';
import type { Account, AuthSession } from '@/services/api/types';
import { unregisterPushDevice } from '@/services/notifications/push';
import { clearSensitiveCaches } from '@/services/offline/secure-cache';
import { clearRefreshToken, readRefreshToken, saveRefreshToken } from '@/services/token-storage';

type PendingSignIn = {
  phoneNumber: string;
  developmentCode: string | null;
};

type SessionState = {
  hydrated: boolean;
  accessToken: string | null;
  refreshToken: string | null;
  account: Account | null;
  pending: PendingSignIn | null;
  setSession: (session: AuthSession) => void;
  setAccount: (account: Account) => void;
  setPending: (pending: PendingSignIn | null) => void;
  clear: () => void;
  markHydrated: () => void;
};

export const useSessionStore = create<SessionState>((set) => ({
  hydrated: false,
  accessToken: null,
  refreshToken: null,
  account: null,
  pending: null,
  setSession: (session) =>
    set({
      accessToken: session.accessToken,
      refreshToken: session.refreshToken,
      account: session.account,
      pending: null,
    }),
  setAccount: (account) => set({ account }),
  setPending: (pending) => set({ pending }),
  clear: () => set({ accessToken: null, refreshToken: null, account: null, pending: null }),
  markHydrated: () => set({ hydrated: true }),
}));

export async function persistSession(session: AuthSession) {
  await saveRefreshToken(session.refreshToken);
  useSessionStore.getState().setSession(session);
}

export async function clearSession() {
  try {
    await unregisterPushDevice();
  } catch {
    // ignore
  }
  await clearSensitiveCaches();
  await clearRefreshToken();
  useSessionStore.getState().clear();
}

export async function hydrateSession() {
  try {
    const refreshToken = await readRefreshToken();
    if (refreshToken) {
      const session = await refreshSession(refreshToken);
      await persistSession(session);
    }
  } catch {
    await clearRefreshToken();
    useSessionStore.getState().clear();
  } finally {
    useSessionStore.getState().markHydrated();
  }
}
