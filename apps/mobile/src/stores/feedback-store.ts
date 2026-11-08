import { create } from 'zustand';

import type { ToastTone } from '@/components/ui/status-toast';

export type FeedbackToast = {
  id: string;
  message: string;
  tone: ToastTone;
  sticky?: boolean;
};

type FeedbackState = {
  toasts: FeedbackToast[];
  show: (input: {
    message: string;
    tone?: ToastTone;
    sticky?: boolean;
    dedupeKey?: string;
  }) => string;
  showSuccess: (message: string) => string;
  showError: (message: string, sticky?: boolean) => string;
  showWarning: (message: string) => string;
  showInfo: (message: string) => string;
  dismiss: (id: string) => void;
  clear: () => void;
};

let seq = 0;
const timers = new Map<string, ReturnType<typeof setTimeout>>();

function nextId() {
  seq += 1;
  return `toast-${seq}-${Date.now()}`;
}

function clearTimer(id: string) {
  const timer = timers.get(id);
  if (timer) {
    clearTimeout(timer);
    timers.delete(id);
  }
}

export const useFeedbackStore = create<FeedbackState>((set, get) => ({
  toasts: [],
  show: ({ message, tone = 'info', sticky = false, dedupeKey }) => {
    const trimmed = message.trim();
    if (!trimmed) {
      return '';
    }

    const existing = get().toasts.find(
      (toast) =>
        toast.message === trimmed &&
        toast.tone === tone &&
        (dedupeKey == null || toast.id.startsWith(`dedupe:${dedupeKey}`)),
    );
    if (existing) {
      clearTimer(existing.id);
      if (!sticky && !existing.sticky) {
        const timer = setTimeout(() => get().dismiss(existing.id), 4200);
        timers.set(existing.id, timer);
      }
      return existing.id;
    }

    const id = dedupeKey ? `dedupe:${dedupeKey}:${nextId()}` : nextId();
    const toast: FeedbackToast = { id, message: trimmed, tone, sticky };
    set((state) => ({ toasts: [...state.toasts, toast].slice(-4) }));

    if (!sticky) {
      const timer = setTimeout(() => get().dismiss(id), tone === 'error' ? 5600 : 4200);
      timers.set(id, timer);
    }
    return id;
  },
  showSuccess: (message) => get().show({ message, tone: 'success' }),
  showError: (message, sticky = false) => get().show({ message, tone: 'error', sticky }),
  showWarning: (message) => get().show({ message, tone: 'warning' }),
  showInfo: (message) => get().show({ message, tone: 'info' }),
  dismiss: (id) => {
    clearTimer(id);
    set((state) => ({ toasts: state.toasts.filter((toast) => toast.id !== id) }));
  },
  clear: () => {
    for (const id of timers.keys()) {
      clearTimer(id);
    }
    set({ toasts: [] });
  },
}));

export function notifySuccess(message: string) {
  return useFeedbackStore.getState().showSuccess(message);
}

export function notifyError(message: string, sticky = false) {
  return useFeedbackStore.getState().showError(message, sticky);
}

export function notifyWarning(message: string) {
  return useFeedbackStore.getState().showWarning(message);
}

export function notifyInfo(message: string) {
  return useFeedbackStore.getState().showInfo(message);
}
