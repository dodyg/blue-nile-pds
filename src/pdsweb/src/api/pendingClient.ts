import { getPendingAccessJwt } from '../stores/pendingAuth';
import { XrpcError } from './queryClient';

async function pendingRequest<T>(method: string, path: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = {};
  const jwt = getPendingAccessJwt();
  if (jwt) {
    headers['Authorization'] = `Bearer ${jwt}`;
  }
  if (body) {
    headers['Content-Type'] = 'application/json';
  }

  const res = await fetch(`/api/pending/${path}`, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
  });

  if (!res.ok) {
    let detail: { error?: string; message?: string } = {};
    try {
      detail = await res.json();
    } catch {
      // ignore parse errors
    }
    throw new XrpcError(res.status, `api/pending/${path}`, detail.error, detail.message || detail.error || res.statusText);
  }

  if (res.status === 204) return undefined as T;
  const text = await res.text();
  if (!text) return undefined as T;
  return JSON.parse(text) as T;
}

export function pendingGet<T>(path: string, params?: Record<string, string>): Promise<T> {
  const qs = params ? '?' + new URLSearchParams(params).toString() : '';
  return pendingRequest<T>('GET', path + qs);
}

export function pendingPost<T>(path: string, body?: unknown): Promise<T> {
  return pendingRequest<T>('POST', path, body);
}

export function pendingPut<T>(path: string, body?: unknown): Promise<T> {
  return pendingRequest<T>('PUT', path, body);
}

// Public (no auth) — for register / login / validate-invite / config
async function publicPendingRequest<T>(method: string, path: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = {};
  if (body) {
    headers['Content-Type'] = 'application/json';
  }

  const res = await fetch(`/api/pending/${path}`, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
  });

  if (!res.ok) {
    let detail: { error?: string; message?: string } = {};
    try {
      detail = await res.json();
    } catch {
      // ignore parse errors
    }
    throw new XrpcError(res.status, `api/pending/${path}`, detail.error, detail.message || detail.error || res.statusText);
  }

  if (res.status === 204) return undefined as T;
  const text = await res.text();
  if (!text) return undefined as T;
  return JSON.parse(text) as T;
}

export function publicPendingPost<T>(path: string, body?: unknown): Promise<T> {
  return publicPendingRequest<T>('POST', path, body);
}

export function publicPendingGet<T>(path: string, params?: Record<string, string>): Promise<T> {
  const qs = params ? '?' + new URLSearchParams(params).toString() : '';
  return publicPendingRequest<T>('GET', path + qs);
}
