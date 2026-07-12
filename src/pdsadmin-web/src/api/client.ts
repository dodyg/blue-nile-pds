import { getAdminPassword, clearAdminPassword } from '../stores/auth';

async function request<T>(method: string, nsid: string, body?: unknown): Promise<T> {
  const password = getAdminPassword();
  const headers: Record<string, string> = {};
  if (password) {
    headers['Authorization'] = 'Basic ' + btoa('admin:' + password);
  }
  if (body) {
    headers['Content-Type'] = 'application/json';
  }

  const res = await fetch(`/xrpc/${nsid}`, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
  });

  if (res.status === 401) {
    clearAdminPassword();
    window.location.href = '/login';
    throw new Error('Unauthorized');
  }

  if (!res.ok) {
    const text = await res.text();
    throw new Error(`${res.status} ${nsid}: ${text}`);
  }

  if (res.status === 204) return undefined as T;
  return res.json();
}

export function xrpcGet<T>(nsid: string, params?: Record<string, string>): Promise<T> {
  const qs = params ? '?' + new URLSearchParams(params).toString() : '';
  return request<T>('GET', nsid + qs);
}

export function xrpcPost<T>(nsid: string, body?: unknown): Promise<T> {
  return request<T>('POST', nsid, body);
}

export async function validatePassword(password: string): Promise<boolean> {
  try {
    const headers = { Authorization: 'Basic ' + btoa('admin:' + password) };
    const res = await fetch('/xrpc/com.atproto.admin.searchAccounts', { headers });
    return res.ok;
  } catch {
    return false;
  }
}
