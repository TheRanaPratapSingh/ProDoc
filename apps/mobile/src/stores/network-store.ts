import NetInfo from '@react-native-community/netinfo';
import { create } from 'zustand';

type NetworkState = {
  isOnline: boolean;
  isInternetReachable: boolean | null;
  type: string;
  setFromNetInfo: (online: boolean, reachable: boolean | null, type: string) => void;
};

export const useNetworkStore = create<NetworkState>((set) => ({
  isOnline: true,
  isInternetReachable: true,
  type: 'unknown',
  setFromNetInfo: (isOnline, isInternetReachable, type) => set({ isOnline, isInternetReachable, type }),
}));

let unsubscribe: (() => void) | null = null;

export function startNetworkMonitoring() {
  if (unsubscribe) {
    return unsubscribe;
  }

  unsubscribe = NetInfo.addEventListener((state) => {
    const online = state.isConnected !== false;
    useNetworkStore.getState().setFromNetInfo(online, state.isInternetReachable ?? null, state.type);
  });

  void NetInfo.fetch().then((state) => {
    useNetworkStore
      .getState()
      .setFromNetInfo(state.isConnected !== false, state.isInternetReachable ?? null, state.type);
  });

  return () => {
    unsubscribe?.();
    unsubscribe = null;
  };
}

export function selectIsEffectivelyOnline(state: NetworkState) {
  if (!state.isOnline) {
    return false;
  }
  if (state.isInternetReachable === false) {
    return false;
  }
  return true;
}
