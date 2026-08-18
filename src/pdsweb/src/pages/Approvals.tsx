import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { usePendingAccounts, useApproveAccount, useRejectAccount } from '../hooks/useApprovals';
import PageHeader from '../components/PageHeader';
import Button from '../components/Button';
import Badge from '../components/Badge';
import { TableBoard, Table, Th, Tr, Td } from '../components/Table';
import EmptyState from '../components/EmptyState';
import ConfirmDialog from '../components/ConfirmDialog';

export default function Approvals() {
  const [approving, setApproving] = useState<string | null>(null);
  const [rejecting, setRejecting] = useState<string | null>(null);
  const navigate = useNavigate();

  const { data, isPending, error, fetchNextPage, hasNextPage, isFetchingNextPage } = usePendingAccounts();
  const approveMutation = useApproveAccount();
  const rejectMutation = useRejectAccount();

  const accounts = data?.pages.flatMap(p => p.accounts) ?? [];

  return (
    <div>
      <PageHeader
        eyebrow="approvals · queue"
        title="Approvals"
        description="Accounts waiting for admin approval."
      />

      {error && <p className="mb-4 text-sm text-danger">{error.message}</p>}
      {isPending && accounts.length === 0 && <p className="mb-4 text-sm text-secondary">Loading...</p>}

      {accounts.length === 0 && !isPending ? (
        <EmptyState title="No pending approvals" description="All caught up — no accounts are waiting for review." />
      ) : (
        <TableBoard>
          <Table>
            <thead>
              <Tr>
                <Th>Handle</Th>
                <Th className="hidden md:table-cell">Email</Th>
                <Th className="hidden lg:table-cell">Location</Th>
                <Th className="hidden lg:table-cell">Type</Th>
                <Th className="hidden xl:table-cell">Requested</Th>
                <Th />
              </Tr>
            </thead>
            <tbody>
              {accounts.map(acc => (
                <Tr key={acc.did}>
                  <Td>
                    <button
                      onClick={() => navigate(`/admin/approvals/${encodeURIComponent(acc.did)}`)}
                      className="text-left focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
                    >
                      <div className="font-medium text-ink hover:text-primary">{acc.handle}</div>
                      <div className="max-w-[200px] truncate font-mono text-xs text-muted">{acc.did}</div>
                    </button>
                  </Td>
                  <Td className="hidden text-secondary md:table-cell">{acc.email || '—'}</Td>
                  <Td className="hidden text-secondary lg:table-cell">{acc.location || '—'}</Td>
                  <Td className="hidden lg:table-cell">
                    <Badge tone="accent">{acc.accountType || 'individual'}</Badge>
                  </Td>
                  <Td className="hidden whitespace-nowrap text-secondary xl:table-cell">
                    {new Date(acc.createdAt).toLocaleString()}
                  </Td>
                  <Td className="whitespace-nowrap">
                    <div className="flex items-center gap-2">
                      <Button
                        variant="primary"
                        size="sm"
                        onClick={() => setApproving(acc.did)}
                        disabled={approveMutation.isPending}
                      >
                        Approve
                      </Button>
                      <Button variant="ghost" size="sm" onClick={() => setRejecting(acc.did)}>
                        Reject
                      </Button>
                    </div>
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

      <ConfirmDialog
        open={approving !== null}
        title="Approve account?"
        message={
          approving
            ? 'This will activate the account and allow the user to log in. A confirmation email will be sent.'
            : ''
        }
        confirmLabel="Approve"
        confirmClass="bg-accent-soft text-primary hover:opacity-90"
        onConfirm={() => {
          if (approving) {
            approveMutation.mutate(approving, {
              onSettled: () => setApproving(null),
            });
          }
        }}
        onCancel={() => setApproving(null)}
      />

      <ConfirmDialog
        open={rejecting !== null}
        title="Reject account?"
        message={
          rejecting
            ? 'The account will remain disabled and the user will be notified. This cannot be undone from this screen.'
            : ''
        }
        confirmLabel="Reject"
        onConfirm={() => {
          if (rejecting) {
            rejectMutation.mutate(rejecting, {
              onSettled: () => setRejecting(null),
            });
          }
        }}
        onCancel={() => setRejecting(null)}
      />
    </div>
  );
}
