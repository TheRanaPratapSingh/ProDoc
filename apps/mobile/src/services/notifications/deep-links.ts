import * as Linking from 'expo-linking';

import { isSafeAppPath, resolveNotificationRoute } from '@/services/notifications/notification-routes';
import { extractReferralCodeFromUrl, savePendingReferralCode } from '@/services/referral-intake';

export { resolveNotificationRoute } from '@/services/notifications/notification-routes';

type AppRouter = {
  push: (href: never) => void;
};

export function parseProdocUrl(url: string | null | undefined): string | null {
  if (!url) {
    return null;
  }

  try {
    const inviteCode = extractReferralCodeFromUrl(url);
    if (inviteCode) {
      return `/invite/${inviteCode}`;
    }

    if (/^https?:\/\//i.test(url) && !url.startsWith('prodoc://') && !/prodoc\.app/i.test(url)) {
      return null;
    }

    const parsed = Linking.parse(url);
    const path = parsed.path ? `/${parsed.path.replace(/^\//, '')}` : null;
    return isSafeAppPath(path) ? path : null;
  } catch {
    return null;
  }
}

export function navigateFromDeepLink(router: AppRouter, url: string) {
  const inviteCode = extractReferralCodeFromUrl(url);
  if (inviteCode) {
    void savePendingReferralCode(inviteCode);
  }
  const path = parseProdocUrl(url);
  if (path) {
    router.push(path as never);
  }
}

export function navigateFromNotification(
  router: AppRouter,
  data: { kind?: string; subjectId?: string; path?: string } | undefined,
) {
  if (isSafeAppPath(data?.path)) {
    router.push(data.path as never);
    return;
  }
  const path = resolveNotificationRoute(data?.kind ?? '', data?.subjectId ?? null);
  router.push(path as never);
}
