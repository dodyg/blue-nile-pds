import { useQuery, useInfiniteQuery } from '@tanstack/react-query';
import { xrpcGet } from '../api/client';
import { directoryKeys } from '../api/queryKeys';
import type {
  DescribeRepoResponse,
  GetRecordResponse,
  ListReposResponse,
  Person,
} from '../types/pds';

const PROFILE_NSID = 'app.bsky.actor.profile';

function toPerson(did: string, desc: DescribeRepoResponse, record?: GetRecordResponse): Person {
  const value = record?.value as
    | { displayName?: string; description?: string; avatar?: { ref: { $link: string } } }
    | undefined;
  return {
    did,
    handle: desc.handle,
    displayName: value?.displayName,
    description: value?.description,
    avatarCid: value?.avatar?.ref?.$link,
  };
}

async function fetchPerson(did: string): Promise<Person | null> {
  try {
    const desc = await xrpcGet<DescribeRepoResponse>('com.atproto.repo.describeRepo', { repo: did });
    const record = await xrpcGet<GetRecordResponse>('com.atproto.repo.getRecord', {
      repo: did,
      collection: PROFILE_NSID,
      rkey: 'self',
    }).catch(() => undefined);
    return toPerson(did, desc, record);
  } catch {
    return null;
  }
}

export function useDirectory() {
  return useInfiniteQuery({
    queryKey: directoryKeys.page(),
    queryFn: async ({ pageParam }: { pageParam: string | undefined }) => {
      const params: Record<string, string> = { limit: '24' };
      if (pageParam) params.cursor = pageParam;

      const res = await xrpcGet<ListReposResponse>('com.atproto.sync.listRepos', params);
      const people = await Promise.all(
        res.repos.filter((r) => r.active).map((r) => fetchPerson(r.did)),
      );
      return {
        people: people.filter((p): p is Person => p !== null),
        cursor: res.cursor,
      };
    },
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.cursor,
  });
}

export function useLookup(search: string) {
  return useQuery({
    queryKey: directoryKeys.lookup(search),
    queryFn: async (): Promise<Person | null> => {
      const query = search.trim();
      if (!query) return null;

      const did = query.startsWith('did:')
        ? query
        : (await xrpcGet<{ did: string }>('com.atproto.identity.resolveHandle', { handle: query })).did;

      return fetchPerson(did);
    },
    enabled: search.trim().length > 0,
  });
}