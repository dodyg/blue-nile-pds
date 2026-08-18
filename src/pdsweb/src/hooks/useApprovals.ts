import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { xrpcGet, xrpcPost } from '../api/adminClient';
import { accountKeys } from '../api/queryKeys';
import type { ListPendingAccountsResponse } from '../types/admin';

export const approvalKeys = {
  all: ['approvals'] as const,
  list: ['approvals', 'list'] as const,
};

export function usePendingAccounts() {
  return useInfiniteQuery({
    queryKey: approvalKeys.list,
    queryFn: async ({ pageParam }: { pageParam: string | undefined }) => {
      const params: Record<string, string> = { limit: '50' };
      if (pageParam) params.cursor = pageParam;
      return xrpcGet<ListPendingAccountsResponse>('africa.bsky.admin.listPendingAccounts', params);
    },
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.cursor,
  });
}

export function usePendingAccount(did: string | undefined) {
  const query = usePendingAccounts();
  const pages = query.data?.pages ?? [];
  const account = did
    ? pages.flatMap(p => p.accounts).find(a => a.did === did)
    : undefined;

  return { ...query, account };
}

export function useApproveAccount() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (did: string) => xrpcPost('africa.bsky.admin.approveAccount', { did }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: approvalKeys.all });
      queryClient.invalidateQueries({ queryKey: accountKeys.all });
    },
  });
}

export function useRejectAccount() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (did: string) => xrpcPost('africa.bsky.admin.rejectAccount', { did }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: approvalKeys.all });
    },
  });
}
