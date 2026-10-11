import { useEffect } from 'react';
import { Link } from 'react-router-dom';
import type { InfiniteData, UseInfiniteQueryResult, UseQueryResult } from '@tanstack/react-query';
import JsonTree from './JsonTree';
import FlipChip from './FlipChip';
import { Card, CardHeader } from './Card';
import { TableBoard, Table, Th, Tr, Td } from './Table';
import EmptyState from './EmptyState';
import type { GetRecordResponse, ListRecordsResponse } from '../types/admin';

function rkeyFromUri(uri: string) {
  const parts = uri.split('/');
  return parts[parts.length - 1];
}

interface RepoCollectionsProps {
  collections: string[];
  basePath: string;
}

export function RepoCollections({ collections, basePath }: RepoCollectionsProps) {
  if (collections.length === 0) return null;
  return (
    <Card className="mb-6">
      <CardHeader title="Repo Collections" subtitle={`${collections.length} collections`} />
      <div className="flex flex-wrap gap-2 p-4">
        {[...collections].sort().map(col => (
          <Link
            key={col}
            to={`${basePath}/collections/${encodeURIComponent(col)}`}
            className="rounded-sm border border-subtle bg-page px-3 py-1.5 font-mono text-xs text-ghost transition-colors hover:border-accent-ring hover:bg-accent-soft hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
          >
            {col}
          </Link>
        ))}
      </div>
    </Card>
  );
}

interface CollectionRecordsViewProps {
  did: string;
  query: UseInfiniteQueryResult<InfiniteData<ListRecordsResponse>, Error>;
  recordLink: (rkey: string) => string;
}

export function CollectionRecordsView({ did, query, recordLink }: CollectionRecordsViewProps) {
  const { data, isPending, error, fetchNextPage, hasNextPage, isFetchingNextPage } = query;

  useEffect(() => {
    if (hasNextPage && !isFetchingNextPage) {
      fetchNextPage();
    }
  }, [hasNextPage, isFetchingNextPage, fetchNextPage]);

  const records = data?.pages.flatMap(p => p.records) ?? [];

  return (
    <>
      {error && <p className="mb-4 text-sm text-danger">{error.message}</p>}

      {records.length === 0 && !isPending ? (
        <EmptyState
          title="No records in this collection"
          description="This collection has no stored records."
        />
      ) : (
        <TableBoard>
          <Table>
            <thead>
              <Tr>
                <Th className="w-0">#</Th>
                <Th className="w-0">Rkey</Th>
                <Th className="w-0">CID</Th>
                <Th>Value</Th>
              </Tr>
            </thead>
            <tbody>
              {records.map((r, i) => {
                const rkey = rkeyFromUri(r.uri);
                return (
                  <Tr key={r.uri}>
                    <Td className="w-0 whitespace-nowrap text-xs text-muted">{i + 1}</Td>
                    <Td className="w-0 whitespace-nowrap">
                      <Link
                        to={recordLink(rkey)}
                        className="font-mono text-xs text-primary underline decoration-subtle underline-offset-2 hover:text-primary-hover hover:decoration-accent"
                      >
                        {rkey}
                      </Link>
                    </Td>
                    <Td className="w-0 whitespace-nowrap">
                      <span className="font-mono text-xs text-muted">{r.cid.slice(0, 12)}…</span>
                    </Td>
                    <Td>
                      <JsonTree value={r.value} did={did} />
                    </Td>
                  </Tr>
                );
              })}
            </tbody>
          </Table>
        </TableBoard>
      )}

      {isPending && records.length === 0 && <p className="text-sm text-secondary">Loading...</p>}
    </>
  );
}

interface RecordDetailViewProps {
  did?: string;
  raw: boolean;
  query: UseQueryResult<GetRecordResponse, Error>;
}

export function RecordDetailView({ did, raw, query }: RecordDetailViewProps) {
  const { data, isPending, error } = query;

  return (
    <>
      {data?.cid && (
        <div className="mb-4">
          <FlipChip tone="accent" title="Content identifier">
            CID: {data.cid}
          </FlipChip>
        </div>
      )}

      {error && <p className="mb-4 text-sm text-danger">{error.message}</p>}
      {isPending && <p className="text-sm text-secondary">Loading...</p>}

      {data && (
        raw ? (
          <Card className="overflow-auto p-4">
            <pre className="whitespace-pre-wrap break-all font-mono text-xs leading-relaxed">
              {JSON.stringify(data.value, null, 2)}
            </pre>
          </Card>
        ) : (
          <Card className="p-4">
            <JsonTree value={data.value} did={did} />
          </Card>
        )
      )}
    </>
  );
}
