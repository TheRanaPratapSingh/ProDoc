import Constants from 'expo-constants';
import * as Device from 'expo-device';
import * as Notifications from 'expo-notifications';
import { Platform } from 'react-native';

import { api } from '@/services/api/client';

Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldShowBanner: true,
    shouldShowList: true,
    shouldPlaySound: false,
    shouldSetBadge: false,
    // Lock-screen content must stay non-clinical; server sends generic copy.
  }),
});

export type PushRegistration = {
  expoPushToken: string | null;
  permission: Notifications.PermissionStatus;
};

export async function ensurePushPermissions(): Promise<Notifications.PermissionStatus> {
  if (Platform.OS === 'web') {
    return Notifications.PermissionStatus.DENIED;
  }

  const current = await Notifications.getPermissionsAsync();
  if (current.granted || current.ios?.status === Notifications.IosAuthorizationStatus.PROVISIONAL) {
    return current.status;
  }

  const requested = await Notifications.requestPermissionsAsync();
  return requested.status;
}

export async function getExpoPushToken(): Promise<string | null> {
  if (Platform.OS === 'web' || !Device.isDevice) {
    return null;
  }

  const permission = await ensurePushPermissions();
  if (permission !== Notifications.PermissionStatus.GRANTED) {
    return null;
  }

  const projectId =
    Constants.easConfig?.projectId ??
    (Constants.expoConfig?.extra as { eas?: { projectId?: string } } | undefined)?.eas?.projectId;

  try {
    const token = await Notifications.getExpoPushTokenAsync(
      projectId ? { projectId } : undefined,
    );
    return token.data;
  } catch {
    return null;
  }
}

export async function registerPushDevice(accessTokenPresent: boolean) {
  if (!accessTokenPresent) {
    return null;
  }

  const token = await getExpoPushToken();
  if (!token) {
    return null;
  }

  await api.post('/api/v1/devices/push', {
    platform: Platform.OS,
    token,
    appVersion: Constants.expoConfig?.version ?? '1.0.0',
  });
  return token;
}

export async function unregisterPushDevice() {
  try {
    await api.delete('/api/v1/devices/push');
  } catch {
    // Best-effort on sign-out.
  }
}

/** Build a lock-screen-safe local notification — never include diagnoses or Rx text. */
export function sanitizeNotificationContent(input: { title: string; body: string; kind: string }) {
  const genericBodies: Record<string, string> = {
    'appointment.confirmed': 'Your visit was updated. Open ProDoc for details.',
    'appointment.reminder': 'You have an upcoming visit. Open ProDoc for details.',
    'queue.updated': 'Your queue status changed. Open ProDoc for details.',
    'queue.called': 'It may be your turn soon. Open ProDoc for details.',
    'prescription.available': 'A prescription is available in ProDoc.',
    'payment.status': 'A payment update is available in ProDoc.',
    'payment.paid': 'A payment update is available in ProDoc.',
    'payment.failed': 'A payment update is available in ProDoc.',
  };

  const kindKey = Object.keys(genericBodies).find((key) => input.kind.startsWith(key)) ?? '';
  return {
    title: input.title.length > 48 ? 'ProDoc' : input.title,
    body: genericBodies[kindKey] ?? 'Open ProDoc for a secure update. Details are not shown here.',
  };
}
