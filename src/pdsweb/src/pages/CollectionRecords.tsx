import { useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { useListRecords } from '../hooks/useRepo';
import JsonTree from '../components/JsonTree';
import PageHeader from '../components/PageHeader';
import Button from '../components/Button';
import FlipChip from '../components/FlipChip';
import { TableBoard, Table, Th, Tr, Td } from '../components/Table';
import EmptyState from '../components/EmptyState';

export default function CollectionRecords() {
  const { did, collection } = useParams<{ did: string; collection: string }>();
  const navigate = useNavigate();

  const { data, isPending, error, fetchNextPage, hasNextPage, isFetchingNextPage } = useListRecords(did ?? '', collection ?? '');

  useEffect(() => {
    if (hasNextPage && !isFetchingNextPage) {
      fetchNextPage();
    }
  }, [hasNextPage, isFetchingNextPage, fetchNextPage]);

  const records = data?.pages.flatMap(p => p.records) ?? [];

  function rkeyFromUri(uri: string) {
    const parts = uri.split('/');
    return parts[parts.length - 1];
  }

  return (
    <div>
      <Button variant="ghost" size="sm" className="mb-3 -ml-2" onClick={() => navigate(`/admin/accounts/${encodeURIComponent(did ?? '')}`)}>
        ← Back to account
      </Button>

      <PageHeader
        eyebrow="repo · collection"
        title={collection}
        description={did}
        actions={<FlipChip tone="accent">{records.length} records</FlipChip>}
      />

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
                        to={`/admin/accounts/${encodeURIComponent(did ?? '')}/collections/${encodeURIComponent(collection ?? '')}/${encodeURIComponent(rkey)}`}
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
    </div>
  );
}
