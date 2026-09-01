import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminApiGet, adminApiPost } from '../api/adminClient';
import { pendingKeys } from '../api/queryKeys';

export interface PendingAdminItem {
  id: number;
  email: string;
  handle: string;
  status: string;
  location?: string;
  accountType?: string;
  displayName?: string;
  description?: string;
  inviteCode?: string;
  createdAt: string;
  updatedAt?: string;
  emailConfirmed?: boolean;
  emailConfirmedAt?: string;
}

export interface PendingAdminDetail {
  id: number;
  email: string;
  handle: string;
  status: string;
  displayName?: string;
  description?: string;
  location?: string;
  accountType?: string;
  inviteCode?: string;
  emailConfirmed: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface PendingAdminListResponse {
  items: PendingAdminItem[];
  cursor?: string;
}

export function usePendingAdminList() {
  return useInfiniteQuery({
    queryKey: pendingKeys.list(),
    queryFn: ({ pageParam }: { pageParam?: string }) =>
      adminApiGet<PendingAdminListResponse>('pending/list', pageParam ? { cursor: pageParam, limit: '20' } : { limit: '20' }),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.cursor,
  });
}

export function usePendingAdminDetail(id: number | null) {
  return useQuery({
    queryKey: [...pendingKeys.list(), 'detail', id],
    queryFn: () => adminApiGet<PendingAdminDetail>(`pending/${id}`),
    enabled: id != null,
  });
}

export function usePendingApprove() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { id: number }) =>
      adminApiPost<{ did: string; handle: string }>('pending/approve', body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: pendingKeys.list() });
    },
  });
}

export function usePendingReject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { id: number; reason?: string }) =>
      adminApiPost<void>('pending/reject', body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: pendingKeys.list() });
    },
  });
}
