import AsyncStorage from '@react-native-async-storage/async-storage';

const KEY = 'prodoc.pendingReferralCode';

export async function savePendingReferralCode(code: string | null | undefined) {
  const normalized = (code ?? '').trim().toUpperCase();
  if (normalized.length < 4 || normalized.length > 16) {
    return;
  }
  await AsyncStorage.setItem(KEY, normalized);
}

export async function readPendingReferralCode(): Promise<string | null> {
  const value = await AsyncStorage.getItem(KEY);
  return value?.trim() ? value.trim().toUpperCase() : null;
}

export async function clearPendingReferralCode() {
  await AsyncStorage.removeItem(KEY);
}

/** Extract referral code from invite URLs such as https://prodoc.app/invite/CODE or prodoc://invite/CODE */
export function extractReferralCodeFromUrl(url: string | null | undefined): string | null {
  if (!url) {
    return null;
  }

  try {
    const match = url.match(/\/invite\/([A-Za-z0-9]{4,16})(?:[/?#]|$)/i);
    if (match?.[1]) {
      return match[1].toUpperCase();
    }

    const parsed = new URL(url.includes('://') ? url : `https://prodoc.app${url.startsWith('/') ? '' : '/'}${url}`);
    const fromQuery = parsed.searchParams.get('referralCode') ?? parsed.searchParams.get('code');
    if (fromQuery && /^[A-Za-z0-9]{4,16}$/.test(fromQuery)) {
      return fromQuery.toUpperCase();
    }
  } catch {
    return null;
  }

  return null;
}
