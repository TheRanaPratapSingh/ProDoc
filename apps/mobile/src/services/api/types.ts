export type LanguageCode = 'en' | 'hi';

export type Account = {
  userId: string;
  phoneNumber: string;
  fullName: string | null;
  preferredLanguage: LanguageCode;
  roles: string[];
  permissions: string[];
  deletionRequestedAtUtc: string | null;
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
};
