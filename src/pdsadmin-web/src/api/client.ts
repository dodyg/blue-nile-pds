import { getAdminPassword } from '../stores/auth';
import { XrpcError } from './queryClient';

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

  if (!res.ok) {
    let detail: { error?: string; message?: string } = {};
    try {
      detail = await res.json();
    } catch {
      // ignore parse errors
    }
    throw new XrpcError(res.status, nsid, detail.error, detail.message || res.statusText);
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
