import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

const refreshKey = 'prodoc.refreshToken';

function browserStorage() {
  return typeof sessionStorage === 'undefined' ? null : sessionStorage;
}

export async function readRefreshToken() {
  if (Platform.OS === 'web') {
    return browserStorage()?.getItem(refreshKey) ?? null;
  }

  return SecureStore.getItemAsync(refreshKey);
}

export async function saveRefreshToken(token: string) {
  if (Platform.OS === 'web') {
    browserStorage()?.setItem(refreshKey, token);
    return;
  }

  await SecureStore.setItemAsync(refreshKey, token);
}

export async function clearRefreshToken() {
  if (Platform.OS === 'web') {
    browserStorage()?.removeItem(refreshKey);
    return;
  }

  await SecureStore.deleteItemAsync(refreshKey);
}
