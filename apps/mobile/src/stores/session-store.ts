import { create } from 'zustand';

import { refreshSession } from '@/services/api/client';
import type { Account, AuthSession } from '@/services/api/types';
import {
  clearActiveSurface,
  readActiveSurface,
  saveActiveSurface,
} from '@/services/active-surface-storage';
import { unregisterPushDevice } from '@/services/notifications/push';
import { clearSensitiveCaches } from '@/services/offline/secure-cache';
import { clearRefreshToken, readRefreshToken, saveRefreshToken } from '@/services/token-storage';
import { accountCanAccessSurface, normalizeHomeSurface } from '@/utils/home-surface';

type PendingSignIn = {
  phoneNumber: string;
  developmentCode: string | null;
  purpose: 'patient' | 'admin';
  expiresAtUtc?: string | null;
};

type SessionState = {
  hydrated: boolean;
  accessToken: string | null;
  refreshToken: string | null;
  account: Account | null;
  pending: PendingSignIn | null;
  /** Explicit dashboard choice for multi-role accounts. */
  activeSurface: string | null;
  setSession: (session: AuthSession, activeSurface?: string | null) => void;
  setAccount: (account: Account) => void;
  setPending: (pending: PendingSignIn | null) => void;
  setActiveSurface: (surface: string | null) => void;
  clear: () => void;
  markHydrated: () => void;
};

/** Stable fallback — never allocate `[]` inside a Zustand selector (causes infinite re-renders). */
export const EMPTY_PERMISSIONS: readonly string[] = Object.freeze([]);

export function selectAccountPermissions(state: SessionState): readonly string[] {
  return state.account?.permissions ?? EMPTY_PERMISSIONS;
}

export const useSessionStore = create<SessionState>((set, get) => ({
  hydrated: false,
  accessToken: null,
  refreshToken: null,
  account: null,
  pending: null,
  activeSurface: null,
  setSession: (session, activeSurface) => {
    const previousUserId = get().account?.userId;
    const nextSurface =
      activeSurface !== undefined
        ? activeSurface
        : previousUserId === session.account.userId
          ? get().activeSurface
          : null;
    set({
      accessToken: session.accessToken,
      refreshToken: session.refreshToken,
      account: session.account,
      pending: null,
      activeSurface: nextSurface,
    });
  },
  setAccount: (account) => set({ account }),
  setPending: (pending) => set({ pending }),
  setActiveSurface: (surface) => {
    const account = get().account;
    const normalized = surface ? normalizeHomeSurface(surface) : null;
    if (normalized && account && !accountCanAccessSurface(account, normalized)) {
      return;
    }
    set({ activeSurface: normalized });
    if (account?.userId) {
      if (normalized) {
        void saveActiveSurface(account.userId, normalized);
      } else {
        void clearActiveSurface(account.userId);
      }
    }
  },
  clear: () =>
    set({
      accessToken: null,
      refreshToken: null,
      account: null,
      pending: null,
      activeSurface: null,
    }),
  markHydrated: () => set({ hydrated: true }),
}));

/** Cached permissions snapshot safe for Zustand subscriptions. */
export function useAccountPermissions(): readonly string[] {
  return useSessionStore(selectAccountPermissions);
}

export function useHasPermission(code: string): boolean {
  return useSessionStore((state) => state.account?.permissions?.includes(code) ?? false);
}

export async function persistSession(session: AuthSession, activeSurface?: string | null) {
  await saveRefreshToken(session.refreshToken);
  const current = useSessionStore.getState();
  // Skip Zustand writes when nothing meaningful changed — prevents global re-render blinks.
  if (
    activeSurface === undefined &&
    current.accessToken === session.accessToken &&
    current.refreshToken === session.refreshToken &&
    current.account?.userId === session.account.userId &&
    current.account?.fullName === session.account.fullName &&
    current.account?.homeSurface === session.account.homeSurface &&
    current.account?.phoneNumber === session.account.phoneNumber
  ) {
    const sameRoles =
      (current.account?.roles?.length ?? 0) === (session.account.roles?.length ?? 0) &&
      (current.account?.roles ?? []).every((role, index) => role === session.account.roles[index]);
    const samePermissions =
      (current.account?.permissions?.length ?? 0) === (session.account.permissions?.length ?? 0) &&
      (current.account?.permissions ?? []).every(
        (code, index) => code === session.account.permissions[index],
      );
    if (sameRoles && samePermissions) {
      return;
    }
  }
  useSessionStore.getState().setSession(session, activeSurface);
}

export async function clearSession() {
  const userId = useSessionStore.getState().account?.userId;
  try {
    await unregisterPushDevice();
  } catch {
    // ignore
  }
  await clearSensitiveCaches();
  await clearRefreshToken();
  await clearActiveSurface(userId);
  useSessionStore.getState().clear();
}

export async function hydrateSession() {
  try {
    const refreshToken = await readRefreshToken();
    if (refreshToken) {
      const session = await refreshSession(refreshToken);
      const storedSurface = await readActiveSurface(session.account.userId);
      const validSurface =
        storedSurface && accountCanAccessSurface(session.account, storedSurface) ? storedSurface : null;
      await persistSession(session, validSurface);
    }
  } catch {
    await clearRefreshToken();
    useSessionStore.getState().clear();
  } finally {
    useSessionStore.getState().markHydrated();
  }
}
