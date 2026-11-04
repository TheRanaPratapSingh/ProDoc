import axios from 'axios';

type ProblemBody = {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
  type?: string;
};

export function problemMessage(error: unknown, fallback: string) {
  if (!axios.isAxiosError<ProblemBody>(error)) {
    if (error instanceof Error && error.message.trim().length > 0) {
      return error.message;
    }

    return fallback;
  }

  // No HTTP response: network / DNS / cleartext blocked / wrong API host (common on devices).
  if (!error.response) {
    if (error.code === 'ECONNABORTED' || /timeout/i.test(error.message)) {
      return 'The server took too long to respond. Confirm the API is running and try again.';
    }

    return (
      'Cannot reach the ProDoc API. On a physical phone, set EXPO_PUBLIC_API_URL to your computer’s LAN IP ' +
      `(not localhost), e.g. http://192.168.1.10:5080. Current base: ${error.config?.baseURL ?? 'unknown'}.`
    );
  }

  const errors = error.response.data?.errors;
  if (errors) {
    const first = Object.values(errors)[0]?.[0];
    if (first) {
      return first;
    }
  }

  return error.response.data?.title ?? error.response.data?.detail ?? fallback;
}
