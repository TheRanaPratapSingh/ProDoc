import { useCallback, useRef, useState } from 'react';

/**
 * Pull-to-refresh state that is true only while the user-initiated refresh runs.
 * Never bind RefreshControl to query `isFetching`/`isRefetching` — background
 * polling and remounts would flash the spinner and look like a full screen blink.
 */
export function useManualRefresh(refresh: () => Promise<unknown>) {
  const [refreshing, setRefreshing] = useState(false);
  const inFlight = useRef(false);

  const onRefresh = useCallback(async () => {
    if (inFlight.current) {
      return;
    }
    inFlight.current = true;
    setRefreshing(true);
    try {
      await refresh();
    } finally {
      inFlight.current = false;
      setRefreshing(false);
    }
  }, [refresh]);

  return { refreshing, onRefresh };
}
