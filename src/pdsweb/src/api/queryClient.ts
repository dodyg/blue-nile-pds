import { QueryClient, QueryCache, MutationCache } from '@tanstack/react-query';

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
    queryCache: new QueryCache({}),
    mutationCache: new MutationCache({}),
  });
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