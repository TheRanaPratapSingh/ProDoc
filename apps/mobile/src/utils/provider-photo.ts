import { apiBaseUrl } from '@/services/api/client';

/** Real provider photos via photoDocumentId — never fabricate profile images. */
export function photoUriFor(
  documentId: string | null | undefined,
  accessToken: string | null | undefined,
): { uri: string; headers: Record<string, string> } | null {
  if (!documentId || !accessToken) {
    return null;
  }
  return {
    uri: `${apiBaseUrl}/api/v1/provider-documents/${documentId}`,
    headers: { Authorization: `Bearer ${accessToken}` },
  };
}
