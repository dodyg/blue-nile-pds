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

async function adminApiRequest<T>(method: string, path: string, body?: unknown): Promise<T> {
  const password = getAdminPassword();
  const headers: Record<string, string> = {};
  if (password) {
    headers['Authorization'] = 'Basic ' + btoa('admin:' + password);
  }
  if (body) {
    headers['Content-Type'] = 'application/json';
  }

  const res = await fetch(`/admin/api/${path}`, {
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
    throw new XrpcError(res.status, `admin/api/${path}`, detail.error, detail.message || res.statusText);
  }

  if (res.status === 204) return undefined as T;
  return res.json();
}

export function adminApiGet<T>(path: string, params?: Record<string, string>): Promise<T> {
  const qs = params ? '?' + new URLSearchParams(params).toString() : '';
  return adminApiRequest<T>('GET', path + qs);
}

export function adminApiPost<T>(path: string, body?: unknown): Promise<T> {
  return adminApiRequest<T>('POST', path, body);
}

export async function downloadAdminFile(path: string, params: Record<string, string>): Promise<void> {
  const password = getAdminPassword();
  const headers: Record<string, string> = {};
  if (password) {
    headers['Authorization'] = 'Basic ' + btoa('admin:' + password);
  }

  const qs = '?' + new URLSearchParams(params).toString();
  const res = await fetch(`/admin/api/${path}${qs}`, { headers });

  if (!res.ok) {
    let detail: { error?: string; message?: string } = {};
    try {
      detail = await res.json();
    } catch {
      // ignore parse errors
    }
    throw new XrpcError(res.status, `admin/api/${path}`, detail.error, detail.message || res.statusText);
  }

  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = params.fileName;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

export async function downloadXrpcFile(nsid: string, params: Record<string, string>, fileName: string): Promise<void> {
  const password = getAdminPassword();
  const headers: Record<string, string> = {};
  if (password) {
    headers['Authorization'] = 'Basic ' + btoa('admin:' + password);
  }

  const qs = '?' + new URLSearchParams(params).toString();
  const res = await fetch(`/xrpc/${nsid}${qs}`, { headers });

  if (!res.ok) {
    let detail: { error?: string; message?: string } = {};
    try {
      detail = await res.json();
    } catch {
      // ignore parse errors
    }
    throw new XrpcError(res.status, nsid, detail.error, detail.message || res.statusText);
  }

  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
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
