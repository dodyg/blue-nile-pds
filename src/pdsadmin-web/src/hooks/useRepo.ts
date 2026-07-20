import { useQuery, useInfiniteQuery } from '@tanstack/react-query';
import { xrpcGet } from '../api/client';
import type { DescribeRepoResponse, ListRecordsResponse, GetRecordResponse } from '../types/admin';

export const repoKeys = {
  all: ['repo'] as const,
  describe: (did: string) => ['repo', 'describe', did] as const,
  listRecords: (did: string, collection: string) => ['repo', 'listRecords', did, collection] as const,
  record: (did: string, collection: string, rkey: string) => ['repo', 'record', did, collection, rkey] as const,
};

export function useDescribeRepo(did: string) {
  return useQuery({
    queryKey: repoKeys.describe(did),
    queryFn: () => xrpcGet<DescribeRepoResponse>('com.atproto.repo.describeRepo', { repo: did }),
    enabled: !!did,
  });
}

export function useListRecords(did: string, collection: string) {
  return useInfiniteQuery({
    queryKey: repoKeys.listRecords(did, collection),
    queryFn: async ({ pageParam }: { pageParam: string | undefined }) => {
      const params: Record<string, string> = { repo: did, collection, limit: '100' };
      if (pageParam) params.cursor = pageParam;
      return xrpcGet<ListRecordsResponse>('com.atproto.repo.listRecords', params);
    },
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.cursor,
    enabled: !!did && !!collection,
  });
}

export function useGetRecord(did: string, collection: string, rkey: string) {
  return useQuery({
    queryKey: repoKeys.record(did, collection, rkey),
    queryFn: () => xrpcGet<GetRecordResponse>('com.atproto.repo.getRecord', { repo: did, collection, rkey }),
    enabled: !!did && !!collection && !!rkey,
  });
}
