import { useSyncExternalStore } from 'react';

const KEY = 'pds_user_jwt';

let jwt: string | null = null;
const listeners = new Set<() => void>();

function readStored() {
  try {
    return sessionStorage.getItem(KEY);
  } catch {
    return null;
  }
}

function emit() {
  for (const listener of listeners) listener();
}

export function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function getAccessJwt(): string | null {
  return jwt ?? readStored();
}

export function setAccessJwt(value: string) {
  jwt = value;
  sessionStorage.setItem(KEY, value);
  emit();
}

export function clearAccessJwt() {
  jwt = null;
  sessionStorage.removeItem(KEY);
  emit();
}

export function useIsSignedIn() {
  return useSyncExternalStore(
    subscribe,
    () => getAccessJwt() !== null,
    () => false,
  );
}