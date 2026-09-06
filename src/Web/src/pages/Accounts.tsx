import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useSearchAccounts } from '../hooks/useAccounts';
import PageHeader from '../components/PageHeader';
import Button from '../components/Button';
import { Input } from '../components/Input';
import Badge from '../components/Badge';
import { TableBoard, Table, Th, Tr, Td } from '../components/Table';
import EmptyState from '../components/EmptyState';

export default function Accounts() {
  const navigate = useNavigate();
  const [query, setQuery] = useState('');
  const [email, setEmail] = useState('');

  const { data, isPending, error, fetchNextPage, hasNextPage, isFetchingNextPage } = useSearchAccounts(email);

  const accounts = data?.pages.flatMap(p => p.accounts) ?? [];

  function handleSearch(e: React.FormEvent) {
    e.preventDefault();
    setEmail(query);
  }

  return (
    <div>
      <PageHeader
        eyebrow="accounts · manifest"
        title="Accounts"
        description="Search registered accounts by email."
        actions={
          <form onSubmit={handleSearch} className="flex gap-2">
            <div className="w-64">
              <Input
                type="text"
                placeholder="Search by email..."
                value={query}
                onChange={e => setQuery(e.target.value)}
              />
            </div>
            <Button variant="primary" type="submit">Search</Button>
          </form>
        }
      />

      {error && <p className="mb-4 text-sm text-danger">{error.message}</p>}
      {isPending && accounts.length === 0 && <p className="mb-4 text-sm text-secondary">Loading...</p>}

      {accounts.length === 0 && !isPending ? (
        <EmptyState
          title="No accounts found"
          description="Try a different email search, or clear the query to list all."
        />
      ) : (
        <TableBoard>
          <Table>
            <thead>
              <Tr>
                <Th>DID</Th>
                <Th>Handle / Email</Th>
                <Th className="hidden md:table-cell">Email</Th>
                <Th>Status</Th>
                <Th />
              </Tr>
            </thead>
            <tbody>
              {accounts.map(acc => (
                <Tr key={acc.did}>
                  <Td className="max-w-[200px] truncate font-mono text-xs">{acc.did}</Td>
                  <Td>
                    <div className="font-medium text-ink">{acc.handle}</div>
                    <div className="text-xs text-muted md:hidden">{acc.email || '—'}</div>
                  </Td>
                  <Td className="hidden text-secondary md:table-cell">{acc.email || '—'}</Td>
                  <Td>
                    <div className="flex flex-wrap gap-1">
                      {acc.invitesDisabled && <Badge tone="warning">invites off</Badge>}
                      {acc.deactivatedAt && <Badge tone="neutral">deactivated</Badge>}
                      {!acc.invitesDisabled && !acc.deactivatedAt && <Badge tone="success">active</Badge>}
                    </div>
                  </Td>
                  <Td className="whitespace-nowrap">
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => navigate(`/admin/accounts/${encodeURIComponent(acc.did)}`)}
                    >
                      View
                    </Button>
                  </Td>
                </Tr>
              ))}
            </tbody>
          </Table>
        </TableBoard>
      )}

      {hasNextPage && (
        <Button
          variant="secondary"
          className="mt-4"
          onClick={() => fetchNextPage()}
          disabled={isFetchingNextPage}
        >
          {isFetchingNextPage ? 'Loading...' : 'Load more'}
        </Button>
      )}
    </div>
  );
}
