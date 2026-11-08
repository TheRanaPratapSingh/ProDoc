import { api } from '@/services/api/client';
import type { Account, AuthSession, LanguageCode, OtpRequestResult } from '@/services/api/types';

function languageOf(value: string): LanguageCode {
  return value === 'hi' ? 'hi' : 'en';
}

function accountFrom(account: Account): Account {
  const gender =
    account.gender === 'male' || account.gender === 'female' || account.gender === 'other'
      ? account.gender
      : null;
  return {
    ...account,
    preferredLanguage: languageOf(account.preferredLanguage),
    homeSurface: account.homeSurface || 'patient',
    availableSurfaces: account.availableSurfaces?.length
      ? account.availableSurfaces
      : account.homeSurface
        ? [account.homeSurface]
        : ['patient'],
    ageYears: account.ageYears ?? null,
    gender,
    profileCompleted: Boolean(account.profileCompleted),
  };
}

export async function requestOtp(phoneNumber: string, purpose: 'patient' | 'admin' = 'patient') {
  const response = await api.post<OtpRequestResult>('/api/v1/auth/otp/request', { phoneNumber, purpose });
  return response.data;
}

export async function verifyOtp(
  phoneNumber: string,
  code: string,
  deviceName: string,
  purpose: 'patient' | 'admin' = 'patient',
  referralCode?: string | null,
) {
  const response = await api.post<AuthSession>('/api/v1/auth/otp/verify', {
    phoneNumber,
    code,
    deviceName,
    purpose,
    referralCode: referralCode?.trim() || undefined,
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

export async function updateProfile(
  fullName: string,
  preferredLanguage: LanguageCode,
  options?: { ageYears?: number | null; gender?: string | null },
) {
  const response = await api.put<Account>('/api/v1/account/profile', {
    fullName: fullName.length === 0 ? null : fullName,
    preferredLanguage,
    ageYears: options?.ageYears ?? undefined,
    gender: options?.gender || undefined,
  });
  return accountFrom(response.data);
}

export async function completePatientProfile(body: {
  fullName: string;
  ageYears: number;
  gender?: string | null;
  preferredLanguage: LanguageCode;
}) {
  const response = await api.post<Account>('/api/v1/account/profile/complete', {
    fullName: body.fullName,
    ageYears: body.ageYears,
    gender: body.gender || null,
    preferredLanguage: body.preferredLanguage,
  });
  return accountFrom(response.data);
}

export async function requestDeletion() {
  await api.post('/api/v1/account/deletion-request');
}
