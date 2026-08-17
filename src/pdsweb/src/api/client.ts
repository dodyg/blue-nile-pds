import { getAccessJwt } from '../stores/userAuth';
import { XrpcError } from './queryClient';

async function request<T>(method: string, nsid: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = {};
  const jwt = getAccessJwt();
  if (jwt) {
    headers['Authorization'] = `Bearer ${jwt}`;
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
  const text = await res.text();
  if (!text) return undefined as T;
  return JSON.parse(text) as T;
}

export function xrpcGet<T>(nsid: string, params?: Record<string, string>): Promise<T> {
  const qs = params ? '?' + new URLSearchParams(params).toString() : '';
  return request<T>('GET', nsid + qs);
}

export function xrpcPost<T>(nsid: string, body?: unknown): Promise<T> {
  return request<T>('POST', nsid, body);
}

export interface UploadBlobResponse {
  blob: {
    $type: 'blob';
    ref: { $link: string };
    mimeType: string;
    size: number;
  };
}

export async function uploadBlob(file: Blob, mimeType: string): Promise<UploadBlobResponse> {
  const headers: Record<string, string> = { 'Content-Type': mimeType };
  const jwt = getAccessJwt();
  if (jwt) {
    headers['Authorization'] = `Bearer ${jwt}`;
  }

  const res = await fetch('/xrpc/com.atproto.repo.uploadBlob', {
    method: 'POST',
    headers,
    body: file,
  });

  if (!res.ok) {
    let detail: { error?: string; message?: string } = {};
    try {
      detail = await res.json();
    } catch {
      // ignore parse errors
    }
    throw new XrpcError(res.status, 'com.atproto.repo.uploadBlob', detail.error, detail.message || res.statusText);
  }

  return res.json();
}