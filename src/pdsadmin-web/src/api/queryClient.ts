import { QueryClient, QueryCache, MutationCache } from '@tanstack/react-query';
import { clearAdminPassword } from '../stores/auth';

function authRedirect() {
  clearAdminPassword();
  window.location.href = '/admin/login';
}

export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        retry: 1,
        refetchOnWindowFocus: false,
      },
      mutations: {
        retry: 0,
      },
    },
    queryCache: new QueryCache({
      onError: handleError,
    }),
    mutationCache: new MutationCache({
      onError: handleError,
    }),
  });
}

function handleError(error: unknown) {
  if (error instanceof XrpcError && error.status === 401) {
    authRedirect();
  }
}

export class XrpcError extends Error {
  status: number;
  nsid: string;
  error?: string;

  constructor(status: number, nsid: string, error?: string, message?: string) {
    super(message || error || `Request failed`);
    this.name = 'XrpcError';
    this.status = status;
    this.nsid = nsid;
    this.error = error;
  }
}
