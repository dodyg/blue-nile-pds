import { useSyncExternalStore } from 'react';

const ACCESS_KEY = 'pds_pending_access_jwt';
const REFRESH_KEY = 'pds_pending_refresh_jwt';

let accessJwt: string | null = null;
let refreshJwt: string | null = null;
const listeners = new Set<() => void>();

function readStored(key: string): string | null {
  try {
    return sessionStorage.getItem(key);
  } catch {
    return null;
  }
}

function emit() {
  for (const listener of listeners) listener();
}

export function subscribePending(listener: () => void) {
  listeners.add(listener);
  return () => { listeners.delete(listener); };
}

export function getPendingAccessJwt(): string | null {
  return accessJwt ?? readStored(ACCESS_KEY);
}

export function getPendingRefreshJwt(): string | null {
  return refreshJwt ?? readStored(REFRESH_KEY);
}

export function setPendingTokens(access: string, refresh: string) {
  accessJwt = access;
  refreshJwt = refresh;
  sessionStorage.setItem(ACCESS_KEY, access);
  sessionStorage.setItem(REFRESH_KEY, refresh);
  emit();
}

export function clearPendingTokens() {
  accessJwt = null;
  refreshJwt = null;
  sessionStorage.removeItem(ACCESS_KEY);
  sessionStorage.removeItem(REFRESH_KEY);
  emit();
}

export function useIsPendingSignedIn() {
  return useSyncExternalStore(
    subscribePending,
    () => getPendingAccessJwt() !== null,
    () => false,
  );
}
