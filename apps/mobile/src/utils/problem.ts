import axios, { type AxiosError } from 'axios';

import { notifyError } from '@/stores/feedback-store';

type ProblemBody = {
  title?: string;
  detail?: string;
  message?: string;
  errors?: Record<string, string[]>;
  type?: string;
  code?: string;
  success?: boolean;
};

export type ApiErrorKind =
  | 'network'
  | 'timeout'
  | 'unauthorized'
  | 'forbidden'
  | 'not_found'
  | 'conflict'
  | 'validation'
  | 'rate_limit'
  | 'server'
  | 'cancelled'
  | 'unknown';

const CODE_FALLBACKS: Record<string, string> = {
  otp_invalid: 'Incorrect OTP. Please try again.',
  otp_expired: 'Your OTP has expired. Please request a new OTP.',
  otp_used: 'This OTP was already used. Please request a new OTP.',
  otp_attempts_exceeded: 'Too many incorrect OTP attempts. Please request a new OTP.',
  otp_cooldown: 'A code was just sent. Wait a moment before requesting another.',
  otp_rate_limited: 'Too many codes were requested. Please try again later.',
  otp_format: 'Please enter the 6-digit OTP.',
  phone_invalid: 'Please enter a valid 10-digit mobile number.',
  name_mandatory: 'Name is mandatory.',
  age_mandatory: 'Age is mandatory.',
  age_invalid: 'Please enter a valid age.',
  name_invalid: 'Enter a name using letters, spaces, apostrophes, or hyphens.',
  patient_name_mandatory: 'Patient name is mandatory.',
  booking_mode_invalid: 'Choose TimedSlots or HourlyQueue.',
  booking_mode_change_requires_confirmation:
    'Future bookings use the current mode and will keep their original times or tokens. Confirm to switch mode for new bookings only.',
  gender_invalid: 'Choose Male, Female, or Other.',
  date_of_birth_invalid: 'Date of birth cannot be in the future.',
  date_invalid: 'Please select a valid date.',
  date_range_invalid: 'Start date cannot be later than end date.',
  date_range_too_large: 'Choose a shorter date range.',
  appointment_date_past: 'Please select an available future appointment date.',
  appointment_date_outside_window: 'Appointments are available only within the permitted booking period.',
  follow_up_date_invalid: 'Follow-up date must be in the future.',
  appointment_not_found: 'That appointment was not found.',
  medical_record_not_found: 'That medical record was not found.',
  schedule_not_found: 'That schedule is not available.',
  time_invalid: 'Please select a valid time.',
  admin_login_denied: 'Super Admin access is restricted.',
  account_deletion_pending: 'This account is scheduled for deletion and cannot sign in.',
  refresh_reuse: 'Your session was revoked for security. Please sign in again.',
  refresh_invalid: 'Your session expired. Please sign in again.',
  slot_unavailable: 'This time slot is no longer available. Please select another slot.',
  slot_taken: 'This time slot was just taken. Please select another slot.',
  hold_expired: 'Your hold expired. Please select the time again.',
  duplicate_appointment:
    'You already have an appointment with this doctor on this date. Please select another date.',
  validation_failed: 'Please complete all required fields.',
};

export function classifyApiError(error: unknown): ApiErrorKind {
  if (!axios.isAxiosError(error)) {
    return 'unknown';
  }

  if (error.code === 'ERR_CANCELED') {
    return 'cancelled';
  }

  if (!error.response) {
    if (error.code === 'ECONNABORTED' || /timeout/i.test(error.message)) {
      return 'timeout';
    }
    return 'network';
  }

  switch (error.response.status) {
    case 401:
      return 'unauthorized';
    case 403:
      return 'forbidden';
    case 404:
      return 'not_found';
    case 408:
      return 'timeout';
    case 409:
      return 'conflict';
    case 422:
    case 400:
      return 'validation';
    case 429:
      return 'rate_limit';
    default:
      return error.response.status >= 500 ? 'server' : 'unknown';
  }
}

export function isRetryableApiError(error: unknown): boolean {
  const kind = classifyApiError(error);
  return kind === 'network' || kind === 'timeout' || kind === 'server' || kind === 'rate_limit';
}

export function problemCode(error: unknown): string | null {
  if (!axios.isAxiosError<ProblemBody>(error)) {
    return null;
  }

  const data = error.response?.data;
  if (!data) {
    return null;
  }

  if (typeof data.code === 'string' && data.code.trim()) {
    return data.code.trim();
  }

  const type = data.type;
  if (typeof type === 'string' && type.startsWith('urn:prodoc:')) {
    return type.slice('urn:prodoc:'.length) || null;
  }

  return null;
}

export function problemMessage(error: unknown, fallback: string) {
  if (!axios.isAxiosError<ProblemBody>(error)) {
    if (error instanceof Error && error.message.trim().length > 0) {
      return error.message;
    }

    return fallback;
  }

  // No HTTP response: network / DNS / cleartext blocked / wrong API host.
  if (!error.response) {
    if (error.code === 'ECONNABORTED' || /timeout/i.test(error.message)) {
      return 'The server took too long to respond. Please try again.';
    }

    if (__DEV__) {
      return (
        'Unable to connect. On a physical phone, set EXPO_PUBLIC_API_URL to your computer’s LAN IP ' +
        `(not localhost), e.g. http://192.168.1.10:5080. Current base: ${error.config?.baseURL ?? 'unknown'}.`
      );
    }

    return 'Unable to connect. Please check your internet connection.';
  }

  const code = problemCode(error);
  const coded = code ? CODE_FALLBACKS[code] : undefined;
  // Prefer stable product copy for known business codes (OTP, booking, etc.).
  if (coded) {
    return coded;
  }

  const status = error.response.status;
  if (status === 401) {
    return error.response.data?.title ?? 'Your session expired. Sign in again.';
  }
  if (status === 403) {
    return error.response.data?.title ?? 'You do not have permission for this action.';
  }
  if (status === 429) {
    return error.response.data?.title ?? 'Too many attempts. Wait a moment and try again.';
  }
  if (status === 409) {
    return error.response.data?.title ?? 'This record changed. Refresh and try again.';
  }
  if (status >= 500) {
    return error.response.data?.title ?? 'Something went wrong. Please try again.';
  }

  const errors = error.response.data?.errors;
  if (errors) {
    const first = Object.values(errors)[0]?.[0];
    if (first) {
      return first;
    }
  }

  return (
    error.response.data?.message ??
    error.response.data?.title ??
    error.response.data?.detail ??
    fallback
  );
}

export function problemFieldErrors(error: unknown): Record<string, string> {
  if (!axios.isAxiosError<ProblemBody>(error)) {
    return {};
  }

  const errors = error.response?.data?.errors;
  if (!errors) {
    return {};
  }

  const mapped: Record<string, string> = {};
  for (const [key, messages] of Object.entries(errors)) {
    const first = messages?.[0];
    if (first) {
      // ASP.NET often prefixes with property path — keep the leaf name usable for forms.
      const leaf = key.includes('.') ? key.slice(key.lastIndexOf('.') + 1) : key;
      mapped[leaf] = first;
      mapped[key] = first;
      mapped[leaf.charAt(0).toLowerCase() + leaf.slice(1)] = first;
    }
  }
  return mapped;
}

/** Show a toast for API failures; skips cancelled requests. */
export function notifyApiError(error: unknown, fallback: string, sticky = false) {
  if (classifyApiError(error) === 'cancelled') {
    return;
  }
  notifyError(problemMessage(error, fallback), sticky);
}

export function asAxiosError(error: unknown): AxiosError<ProblemBody> | null {
  return axios.isAxiosError<ProblemBody>(error) ? error : null;
}
