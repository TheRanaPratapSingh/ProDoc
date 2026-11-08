import AsyncStorage from '@react-native-async-storage/async-storage';

const keyFor = (userId: string) => `prodoc.activeSurface.${userId}`;

export async function readActiveSurface(userId: string): Promise<string | null> {
  try {
    return await AsyncStorage.getItem(keyFor(userId));
  } catch {
    return null;
  }
}

export async function saveActiveSurface(userId: string, surface: string): Promise<void> {
  try {
    await AsyncStorage.setItem(keyFor(userId), surface);
  } catch {
    // ignore storage failures — in-memory session still works
  }
}

export async function clearActiveSurface(userId?: string | null): Promise<void> {
  if (!userId) return;
  try {
    await AsyncStorage.removeItem(keyFor(userId));
  } catch {
    // ignore
  }
}
