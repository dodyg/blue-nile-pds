import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminApiGet, adminApiPost } from '../api/adminClient';
import { repoResyncKeys } from '../api/queryKeys';
import type { RepoResyncCreateResponse, RepoResyncStatus } from '../types/admin';

export function useRepoResyncStatus() {
  return useQuery({
    queryKey: repoResyncKeys.status,
    queryFn: () => adminApiGet<RepoResyncStatus>('repo/resync/status'),
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      return status === 'running' ? 2000 : false;
    },
  });
}

export function useStartRepoResync() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (did: string) => adminApiPost<RepoResyncCreateResponse>('repo/resync', { did }),
    onSuccess: () => {
      queryClient.setQueryData(repoResyncKeys.status, { status: 'running' } as RepoResyncStatus);
      queryClient.invalidateQueries({ queryKey: repoResyncKeys.all });
    },
  });
}
