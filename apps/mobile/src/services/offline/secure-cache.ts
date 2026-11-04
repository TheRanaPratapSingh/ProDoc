import AsyncStorage from '@react-native-async-storage/async-storage';
import * as Crypto from 'expo-crypto';
import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

import {
  isExpired,
  makeEnvelope,
  type CacheEnvelope,
  type CacheSensitivity,
} from '@/services/offline/cache-policy';

const KEY_NAME = 'prodoc.cache.v1';
const PREFIX = '@prodoc/cache/';

async function getOrCreateKey(): Promise<string> {
  if (Platform.OS === 'web') {
    const existing = globalThis.sessionStorage?.getItem(KEY_NAME);
    if (existing) {
      return existing;
    }
    const created = Crypto.randomUUID().replace(/-/g, '') + Crypto.randomUUID().replace(/-/g, '');
    globalThis.sessionStorage?.setItem(KEY_NAME, created);
    return created;
  }

  const existing = await SecureStore.getItemAsync(KEY_NAME);
  if (existing) {
    return existing;
  }

  const created = Crypto.randomUUID().replace(/-/g, '') + Crypto.randomUUID().replace(/-/g, '');
  await SecureStore.setItemAsync(KEY_NAME, created);
  return created;
}

function toBytes(text: string) {
  const bytes = new Uint8Array(text.length);
  for (let i = 0; i < text.length; i += 1) {
    bytes[i] = text.charCodeAt(i) & 0xff;
  }
  return bytes;
}

function fromBytes(bytes: Uint8Array) {
  let text = '';
  for (let i = 0; i < bytes.length; i += 1) {
    text += String.fromCharCode(bytes[i]!);
  }
  return text;
}

function b64encode(bytes: Uint8Array) {
  if (typeof globalThis.btoa === 'function') {
    return globalThis.btoa(fromBytes(bytes));
  }
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';
  let output = '';
  for (let i = 0; i < bytes.length; i += 3) {
    const a = bytes[i]!;
    const b = i + 1 < bytes.length ? bytes[i + 1]! : 0;
    const c = i + 2 < bytes.length ? bytes[i + 2]! : 0;
    const triple = (a << 16) | (b << 8) | c;
    output += alphabet[(triple >> 18) & 63];
    output += alphabet[(triple >> 12) & 63];
    output += i + 1 < bytes.length ? alphabet[(triple >> 6) & 63] : '=';
    output += i + 2 < bytes.length ? alphabet[triple & 63] : '=';
  }
  return output;
}

function b64decode(value: string) {
  if (typeof globalThis.atob === 'function') {
    return toBytes(globalThis.atob(value));
  }
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';
  const clean = value.replace(/=+$/, '');
  const bytes: number[] = [];
  for (let i = 0; i < clean.length; i += 4) {
    const n =
      (alphabet.indexOf(clean[i]!) << 18) |
      (alphabet.indexOf(clean[i + 1]!) << 12) |
      ((alphabet.indexOf(clean[i + 2]!) & 63) << 6) |
      (alphabet.indexOf(clean[i + 3]!) & 63);
    bytes.push((n >> 16) & 255);
    if (clean[i + 2] && clean[i + 2] !== '=') bytes.push((n >> 8) & 255);
    if (clean[i + 3] && clean[i + 3] !== '=') bytes.push(n & 255);
  }
  return Uint8Array.from(bytes);
}

/** Device-local obfuscation for cache-at-rest. Not a substitute for transport TLS. */
async function scramble(plain: string, key: string, salt: string) {
  const material = await Crypto.digestStringAsync(Crypto.CryptoDigestAlgorithm.SHA256, `${key}:${salt}`);
  const keyBytes = toBytes(material);
  const input = toBytes(plain);
  const output = new Uint8Array(input.length);
  for (let i = 0; i < input.length; i += 1) {
    output[i] = input[i]! ^ keyBytes[i % keyBytes.length]!;
  }
  return b64encode(output);
}

async function unscramble(cipher: string, key: string, salt: string) {
  const material = await Crypto.digestStringAsync(Crypto.CryptoDigestAlgorithm.SHA256, `${key}:${salt}`);
  const keyBytes = toBytes(material);
  const input = b64decode(cipher);
  const output = new Uint8Array(input.length);
  for (let i = 0; i < input.length; i += 1) {
    output[i] = input[i]! ^ keyBytes[i % keyBytes.length]!;
  }
  return fromBytes(output);
}

export async function writeCache<T>(
  cacheKey: string,
  sensitivity: CacheSensitivity,
  ttlMs: number,
  payload: T,
): Promise<void> {
  const envelope = makeEnvelope(sensitivity, ttlMs, payload);
  const raw = JSON.stringify(envelope);
  const storageKey = PREFIX + cacheKey;

  if (sensitivity === 'public') {
    await AsyncStorage.setItem(storageKey, raw);
    return;
  }

  const key = await getOrCreateKey();
  const salt = Crypto.randomUUID();
  const cipher = await scramble(raw, key, salt);
  await AsyncStorage.setItem(storageKey, JSON.stringify({ v: 1, salt, cipher, sensitivity }));
}

export async function readCache<T>(cacheKey: string): Promise<CacheEnvelope<T> | null> {
  const storageKey = PREFIX + cacheKey;
  const stored = await AsyncStorage.getItem(storageKey);
  if (!stored) {
    return null;
  }

  try {
    const parsed = JSON.parse(stored) as CacheEnvelope<T> | { v: 1; salt: string; cipher: string; sensitivity: CacheSensitivity };
    if ('cipher' in parsed) {
      const key = await getOrCreateKey();
      const plain = await unscramble(parsed.cipher, key, parsed.salt);
      const envelope = JSON.parse(plain) as CacheEnvelope<T>;
      if (isExpired(envelope.expiresAtUtc)) {
        await AsyncStorage.removeItem(storageKey);
        return null;
      }
      return envelope;
    }

    if (isExpired(parsed.expiresAtUtc)) {
      await AsyncStorage.removeItem(storageKey);
      return null;
    }
    return parsed;
  } catch {
    await AsyncStorage.removeItem(storageKey);
    return null;
  }
}

export async function clearSensitiveCaches() {
  const keys = await AsyncStorage.getAllKeys();
  const ours = keys.filter((key) => key.startsWith(PREFIX));
  for (const key of ours) {
    const value = await AsyncStorage.getItem(key);
    if (!value) continue;
    try {
      const parsed = JSON.parse(value) as { sensitivity?: string };
      if (parsed.sensitivity === 'personal' || parsed.sensitivity === 'clinical' || 'cipher' in (parsed as object)) {
        await AsyncStorage.removeItem(key);
      }
    } catch {
      await AsyncStorage.removeItem(key);
    }
  }
}

export async function clearAllCaches() {
  const keys = await AsyncStorage.getAllKeys();
  const ours = keys.filter((key) => key.startsWith(PREFIX));
  if (ours.length) {
    await AsyncStorage.multiRemove(ours);
  }
}
