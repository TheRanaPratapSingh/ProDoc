export type LanguageCode = 'en' | 'hi';

export type Account = {
  userId: string;
  phoneNumber: string;
  fullName: string | null;
  preferredLanguage: LanguageCode;
  roles: string[];
  permissions: string[];
  deletionRequestedAtUtc: string | null;
  /** Backend-assigned primary dashboard surface: patient | doctor | clinic | reception | staff | admin */
  homeSurface: string;
  /** All dashboards this account may open (backend source of truth). */
  availableSurfaces?: string[];
  ageYears?: number | null;
  gender?: 'male' | 'female' | 'other' | null;
  /** Persistent backend flag — incomplete patients must finish Name + Age. */
  profileCompleted?: boolean;
};

export type AuthSession = {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
  account: Account;
};

export type OtpRequestResult = {
  challengeId: string;
  expiresAtUtc: string;
  developmentCode: string | null;
  purpose: string;
};

export type AuthPurpose = 'patient' | 'admin';
