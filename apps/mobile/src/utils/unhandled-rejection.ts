type RejectionHandler = (reason: unknown) => void;

let installed = false;

/**
 * Prevents silent unhandled promise rejections from vanishing unnoticed.
 * Does not replace React Native's ErrorUtils (ErrorBoundary still owns fatals).
 */
export function installUnhandledRejectionGuard(onRejection?: RejectionHandler) {
  if (installed) {
    return () => undefined;
  }
  installed = true;

  const report = (reason: unknown) => {
    if (__DEV__) {
      console.warn('[ProDoc] Unhandled promise rejection', reason);
    }
    onRejection?.(reason);
  };

  const webHandler = (event: PromiseRejectionEvent) => {
    report(event.reason);
  };

  const target = globalThis as typeof globalThis & {
    addEventListener?: Window['addEventListener'];
    removeEventListener?: Window['removeEventListener'];
  };

  target.addEventListener?.('unhandledrejection', webHandler);

  // Hermes / RN may expose tracking via process when available.
  const processRef = (globalThis as { process?: { on?: (event: string, listener: (...args: unknown[]) => void) => void; off?: (event: string, listener: (...args: unknown[]) => void) => void } }).process;
  const nodeHandler = (reason: unknown) => report(reason);
  processRef?.on?.('unhandledRejection', nodeHandler);

  return () => {
    installed = false;
    target.removeEventListener?.('unhandledrejection', webHandler);
    processRef?.off?.('unhandledRejection', nodeHandler);
  };
}
