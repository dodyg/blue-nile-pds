import { useQuery } from '@tanstack/react-query';
import { xrpcGet } from '../api/client';
import { dashboardKeys } from '../api/queryKeys';

interface ListReposResponse {
  repos: unknown[];
}

export function useDashboardStats() {
  return useQuery({
    queryKey: dashboardKeys.stats,
    queryFn: async () => {
      const res = await xrpcGet<ListReposResponse>('com.atproto.sync.listRepos');
      return res.repos.length;
    },
    staleTime: 60_000,
  });
}
