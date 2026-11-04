import { api } from '@/services/api/client';
import type { Account, AuthSession, LanguageCode, OtpRequestResult } from '@/services/api/types';

function languageOf(value: string): LanguageCode {
  return value === 'hi' ? 'hi' : 'en';
}

function accountFrom(account: Account): Account {
  return { ...account, preferredLanguage: languageOf(account.preferredLanguage) };
}

export async function requestOtp(phoneNumber: string) {
  const response = await api.post<OtpRequestResult>('/api/v1/auth/otp/request', { phoneNumber });
  return response.data;
}

export async function verifyOtp(phoneNumber: string, code: string, deviceName: string) {
  const response = await api.post<AuthSession>('/api/v1/auth/otp/verify', {
    phoneNumber,
    code,
    deviceName,
  });
  return { ...response.data, account: accountFrom(response.data.account) };
}

export async function logout(refreshToken: string) {
  await api.post('/api/v1/auth/logout', { refreshToken });
}

export async function getAccount() {
  const response = await api.get<Account>('/api/v1/account/me');
  return accountFrom(response.data);
}

export async function updateProfile(fullName: string, preferredLanguage: LanguageCode) {
  const response = await api.put<Account>('/api/v1/account/profile', {
    fullName: fullName.length === 0 ? null : fullName,
    preferredLanguage,
  });
  return accountFrom(response.data);
}

export async function requestDeletion() {
  await api.post('/api/v1/account/deletion-request');
}
