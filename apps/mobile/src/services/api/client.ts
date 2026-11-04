import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios';
import Constants from 'expo-constants';
import { Platform } from 'react-native';

import type { AuthSession } from '@/services/api/types';

function expoDevHost(): string | null {
  const candidates = [
    Constants.expoConfig?.hostUri,
    Constants.expoGoConfig?.debuggerHost,
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (Constants as any).manifest2?.extra?.expoClient?.hostUri as string | undefined,
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (Constants as any).manifest?.debuggerHost as string | undefined,
  ];

  for (const candidate of candidates) {
    if (!candidate || typeof candidate !== 'string') {
      continue;
    }

    const cleaned = candidate
      .replace(/^exp:\/\//i, '')
      .replace(/^https?:\/\//i, '')
      .split('/')[0];
    const host = cleaned.split(':')[0]?.trim();
    if (host && host !== 'localhost' && host !== '127.0.0.1') {
      return host;
    }
  }

  return null;
}

/** Resolve API base URL for web, emulators, and physical devices on the same LAN. */
export function resolveApiBaseUrl(): string {
  const configured = process.env.EXPO_PUBLIC_API_URL?.trim();
  if (configured) {
    return configured.replace(/\/$/, '');
  }

  const lanHost = expoDevHost();
  if (lanHost) {
    return `http://${lanHost}:5080`;
  }

  // Android emulator loopback to the development machine.
  if (Platform.OS === 'android') {
    return 'http://10.0.2.2:5080';
  }

  return 'http://localhost:5080';
}

export const apiBaseUrl = resolveApiBaseUrl();

if (__DEV__) {
  // Helps diagnose device login failures without logging secrets.
  console.info(`[ProDoc] API base URL: ${apiBaseUrl}`);
}

export const api = axios.create({
  baseURL: apiBaseUrl,
  timeout: 15000,
  headers: { 'Content-Type': 'application/json' },
});

type RetryConfig = InternalAxiosRequestConfig & { retried?: boolean };

type AuthBridge = {
  getAccessToken: () => string | null;
  getRefreshToken: () => string | null;
  onSession: (session: AuthSession) => void;
  onSignedOut: () => void;
};

let bridge: AuthBridge | null = null;
let refreshTask: Promise<AuthSession | null> | null = null;

export function installAuthBridge(next: AuthBridge) {
  bridge = next;
}

export async function refreshSession(refreshToken: string) {
  const response = await axios.post<AuthSession>(
    `${apiBaseUrl}/api/v1/auth/refresh`,
    { refreshToken },
    { timeout: 15000 },
  );
  return response.data;
}

async function refreshOnce(refreshToken: string) {
  if (!refreshTask) {
    refreshTask = refreshSession(refreshToken)
      .then((session) => {
        bridge?.onSession(session);
        return session;
      })
      .catch(() => {
        bridge?.onSignedOut();
        return null;
      })
      .finally(() => {
        refreshTask = null;
      });
  }

  return refreshTask;
}

api.interceptors.request.use((config) => {
  const token = bridge?.getAccessToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const config = error.config as RetryConfig | undefined;
    const refreshToken = bridge?.getRefreshToken();
    const isRefresh = config?.url?.includes('/auth/refresh');
    if (error.response?.status !== 401 || !config || config.retried || isRefresh || !refreshToken) {
      throw error;
    }

    config.retried = true;
    const session = await refreshOnce(refreshToken);
    if (!session) {
      throw error;
    }

    config.headers.Authorization = `Bearer ${session.accessToken}`;
    return api(config);
  },
);
