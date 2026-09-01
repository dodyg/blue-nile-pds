import { useState } from 'react';
import { usePendingAdminList, usePendingApprove, usePendingReject } from '../hooks/useAdminPending';
import PageHeader from '../components/PageHeader';
import Button from '../components/Button';
import Badge from '../components/Badge';
import { Card } from '../components/Card';
import { TableBoard, Table, Th, Tr, Td } from '../components/Table';
import EmptyState from '../components/EmptyState';
import ConfirmDialog from '../components/ConfirmDialog';
import { XrpcError } from '../api/queryClient';

function errMessage(err: unknown): string | null {
  if (err instanceof XrpcError) return err.message || err.error || 'Something went wrong';
  return 'Something went wrong';
}

export default function Approvals() {
  const { data, isPending, error, fetchNextPage, hasNextPage, isFetchingNextPage } = usePendingAdminList();
  const approve = usePendingApprove();
  const reject = usePendingReject();

  const [approveId, setApproveId] = useState<number | null>(null);
  const [rejectId, setRejectId] = useState<number | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const items = data?.pages.flatMap(p => p.items) ?? [];

  if (isPending) return <p className="text-sm text-secondary">Loading…</p>;
  if (error) return <p className="text-sm text-danger">{errMessage(error)}</p>;

  return (
    <div>
      <PageHeader eyebrow="moderation · approvals" title="Pending approvals" description="Review pending registrations and approve or reject them." />

      {items.length === 0 ? (
        <EmptyState title="No pending registrations" description="All caught up — no accounts are waiting for review." />
      ) : (
        <TableBoard>
          <Table>
            <thead>
              <tr>
                <Th>Handle</Th>
                <Th>Email</Th>
                <Th>Location</Th>
                <Th>Type</Th>
                <Th>Requested</Th>
                <Th>Actions</Th>
              </tr>
            </thead>
            <tbody>
              {items.map((item) => (
                <Tr key={item.id}>
                  <Td className="font-mono text-xs">{item.handle}</Td>
                  <Td className="font-mono text-xs">{item.email}</Td>
                  <Td>{item.location || '—'}</Td>
                  <Td>{item.accountType || 'individual'}</Td>
                  <Td className="text-xs text-secondary">{new Date(item.createdAt).toLocaleString()}</Td>
                  <Td>
                    <div className="flex items-center gap-2">
                      <Button variant="primary" size="sm" onClick={() => setApproveId(item.id)} disabled={approve.isPending || reject.isPending}>
                        Approve
                      </Button>
                      <Button variant="ghost" size="sm" onClick={() => setRejectId(item.id)} disabled={approve.isPending || reject.isPending}>
                        Reject
                      </Button>
                      {item.emailConfirmedAt == null && <Badge tone="warning">unconfirmed</Badge>}
                    </div>
                  </Td>
                </Tr>
              ))}
            </tbody>
          </Table>
        </TableBoard>
      )}

      {hasNextPage && (
        <div className="mt-4 flex justify-center">
          <Button variant="secondary" onClick={() => fetchNextPage()} disabled={isFetchingNextPage}>
            {isFetchingNextPage ? 'Loading…' : 'Load more'}
          </Button>
        </div>
      )}

      {actionError && <p className="mt-4 text-sm text-danger">{actionError}</p>}
      {approve.error && <p className="mt-2 text-sm text-danger">{errMessage(approve.error)}</p>}
      {reject.error && <p className="mt-2 text-sm text-danger">{errMessage(reject.error)}</p>}

      <ConfirmDialog
        open={approveId != null}
        title="Approve account?"
        message="This will create the PDS account and send a confirmation email to the user."
        confirmLabel="Approve"
        onConfirm={() => {
          if (approveId == null) return;
          setActionError(null);
          approve.mutate({ id: approveId }, { onSuccess: () => setApproveId(null), onError: (e) => setActionError(errMessage(e)) });
        }}
        onCancel={() => setApproveId(null)}
      />

      <Card className={rejectId == null ? 'hidden' : 'mt-4 p-4'}>
        <p className="text-sm font-medium text-ink">Reject account?</p>
        <p className="mt-1 text-sm text-secondary">The user will be notified by email.</p>
        <div className="mt-3 flex items-center gap-2">
          <Button variant="danger" onClick={() => {
            if (rejectId == null) return;
            setActionError(null);
            reject.mutate({ id: rejectId }, { onSuccess: () => setRejectId(null), onError: (e) => setActionError(errMessage(e)) });
          }} disabled={reject.isPending}>
            {reject.isPending ? 'Rejecting…' : 'Reject'}
          </Button>
          <Button variant="ghost" onClick={() => setRejectId(null)}>Cancel</Button>
        </div>
      </Card>
    </div>
  );
}
