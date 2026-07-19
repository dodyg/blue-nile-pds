import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { xrpcGet, xrpcPost } from '../api/client';
import { inviteKeys } from '../api/queryKeys';
import type { GetInviteCodesResponse } from '../types/admin';

export function useInviteCodes() {
  return useInfiniteQuery({
    queryKey: inviteKeys.all,
    queryFn: async ({ pageParam }: { pageParam: string | undefined }) => {
      const params: Record<string, string> = { limit: '100' };
      if (pageParam) params.cursor = pageParam;
      return xrpcGet<GetInviteCodesResponse>('com.atproto.admin.getInviteCodes', params);
    },
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.cursor,
  });
}

export function useDisableInviteCode() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (code: string) =>
      xrpcPost('com.atproto.admin.disableInviteCodes', { codes: [code] }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: inviteKeys.all });
    },
  });
}

export function useCreateInviteCode() {
  return useMutation({
    mutationFn: (body: { useCount: number; forAccount: string }) =>
      xrpcPost<{ code: string }>('com.atproto.server.createInviteCode', body),
  });
}

export function useCreateInviteCodes() {
  return useMutation({
    mutationFn: (body: { useCount: number; codeCount: number; forAccounts: string[] }) =>
      xrpcPost<{ codes: { account: string; codes: string[] }[] }>('com.atproto.server.createInviteCodes', body),
  });
}
