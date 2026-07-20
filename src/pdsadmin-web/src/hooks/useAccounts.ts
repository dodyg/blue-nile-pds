import { useQuery, useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { xrpcGet, xrpcPost } from '../api/client';
import { accountKeys } from '../api/queryKeys';
import type { GetAccountInfoResponse, SearchAccountsResponse, SubjectStatus } from '../types/admin';

export function useAccountInfo(did: string) {
  return useQuery({
    queryKey: accountKeys.detail(did),
    queryFn: () => xrpcGet<GetAccountInfoResponse>('com.atproto.admin.getAccountInfo', { did }),
    enabled: !!did,
  });
}

export function useSubjectStatus(did: string) {
  return useQuery({
    queryKey: accountKeys.subjectStatus(did),
    queryFn: () => xrpcGet<SubjectStatus>('com.atproto.admin.getSubjectStatus', { did }),
    enabled: !!did,
  });
}

export function useAccountSubjectStatus(did: string) {
  return useSubjectStatus(did);
}

export function useSearchAccounts(email: string) {
  return useInfiniteQuery({
    queryKey: accountKeys.search(email),
    queryFn: async ({ pageParam }: { pageParam: string | undefined }) => {
      const params: Record<string, string> = { limit: '50' };
      if (email) params.email = email;
      if (pageParam) params.cursor = pageParam;
      return xrpcGet<SearchAccountsResponse>('com.atproto.admin.searchAccounts', params);
    },
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.cursor,
    enabled: true,
  });
}

export function useUpdateSubjectStatus() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: unknown) =>
      xrpcPost('com.atproto.admin.updateSubjectStatus', body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: accountKeys.all });
    },
  });
}

export function useDeleteAccount() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (did: string) =>
      xrpcPost('com.atproto.admin.deleteAccount', { did }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: accountKeys.all });
    },
  });
}

export function useEnableInvites() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (account: string) =>
      xrpcPost('com.atproto.admin.enableAccountInvites', { account }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: accountKeys.all });
    },
  });
}

export function useDisableInvites() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (account: string) =>
      xrpcPost('com.atproto.admin.disableAccountInvites', { account }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: accountKeys.all });
    },
  });
}

export function useUpdateAccountPassword() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ did, password }: { did: string; password: string }) =>
      xrpcPost('com.atproto.admin.updateAccountPassword', { did, password }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: accountKeys.all });
    },
  });
}

export function useUpdateAccountEmail() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ account, email }: { account: string; email: string }) =>
      xrpcPost('com.atproto.admin.updateAccountEmail', { account, email }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: accountKeys.all });
    },
  });
}

export function useUpdateAccountHandle() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ did, handle }: { did: string; handle: string }) =>
      xrpcPost('com.atproto.admin.updateAccountHandle', { did, handle }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: accountKeys.all });
    },
  });
}
