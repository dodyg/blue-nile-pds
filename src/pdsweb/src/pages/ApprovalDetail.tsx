import { useState, type ReactNode } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import Button from '../components/Button';
import Badge from '../components/Badge';
import { Card } from '../components/Card';
import PageHeader from '../components/PageHeader';
import ConfirmDialog from '../components/ConfirmDialog';
import { useAccountInfo } from '../hooks/useAccounts';
import { usePendingAccount, useApproveAccount, useRejectAccount } from '../hooks/useApprovals';

export default function ApprovalDetail() {
  const { did } = useParams<{ did: string }>();
  const navigate = useNavigate();
  const [approving, setApproving] = useState(false);
  const [rejecting, setRejecting] = useState(false);

  const { data: info, isPending, error: infoError, refetch } = useAccountInfo(did ?? '');
  const { account: pendingAccount } = usePendingAccount(did);
  const approveMutation = useApproveAccount();
  const rejectMutation = useRejectAccount();

  if (isPending && !info) return <div className="text-sm text-secondary">Loading...</div>;
  if (infoError && !info) return <div className="text-sm text-danger">{infoError.message}</div>;
  if (!info) return <div className="text-sm text-secondary">Account not found</div>;

  const stillPending = !!pendingAccount;
  const emailConfirmed = !!info.emailConfirmedAt;

  return (
    <div>
      <Button variant="ghost" size="sm" className="mb-3 -ml-2" onClick={() => navigate('/admin/approvals')}>
        ← Back to approvals
      </Button>

      <PageHeader
        eyebrow="approvals · passenger record"
        title={info.handle}
        description={info.did}
        actions={(
          stillPending ? <Badge tone="warning">pending</Badge> : <Badge tone="success">approved</Badge>
        )}
      />

      <Card className="mb-6 p-5">
        <div className="grid grid-cols-1 gap-x-6 gap-y-3 sm:grid-cols-2">
          <Field label="DID" mono>{info.did}</Field>
          <Field label="Handle">{info.handle}</Field>
          <Field label="Email">{info.email || '—'}</Field>
          <Field label="Email Confirmed">
            {emailConfirmed ? <Badge tone="success">confirmed</Badge> : <Badge tone="warning">not confirmed</Badge>}
          </Field>
          <Field label="Location">{pendingAccount?.location || '—'}</Field>
          <Field label="Account Type">{pendingAccount?.accountType || 'individual'}</Field>
          <Field label="Requested">{new Date(info.indexedAt).toLocaleString()}</Field>
        </div>
      </Card>

      {stillPending ? (
        <div className="flex items-center gap-3">
          <Button
            variant="primary"
            onClick={() => setApproving(true)}
            disabled={approveMutation.isPending}
          >
            {approveMutation.isPending ? 'Approving…' : 'Approve account'}
          </Button>
          <Button
            variant="danger"
            onClick={() => setRejecting(true)}
            disabled={rejectMutation.isPending}
          >
            {rejectMutation.isPending ? 'Rejecting…' : 'Reject account'}
          </Button>
        </div>
      ) : (
        <p className="text-sm text-success-deep dark:text-success">
          This account has been reviewed and is no longer pending approval.
        </p>
      )}

      {approveMutation.error && <p className="mt-4 text-sm text-danger">{approveMutation.error.message}</p>}
      {rejectMutation.error && <p className="mt-4 text-sm text-danger">{rejectMutation.error.message}</p>}

      <div className="mt-6">
        <Button variant="ghost" size="sm" onClick={() => refetch()}>
          Refresh
        </Button>
      </div>

      <ConfirmDialog
        open={approving}
        title="Approve account?"
        message="This will activate the account and allow the user to log in. A confirmation email will be sent."
        confirmLabel="Approve"
        confirmClass="bg-accent-soft text-primary hover:opacity-90"
        onConfirm={() => {
          if (did) {
            approveMutation.mutate(did, { onSettled: () => setApproving(false) });
          }
        }}
        onCancel={() => setApproving(false)}
      />

      <ConfirmDialog
        open={rejecting}
        title="Reject account?"
        message="The account will remain disabled and the user will be notified. This cannot be undone from this screen."
        confirmLabel="Reject"
        onConfirm={() => {
          if (did) {
            rejectMutation.mutate(did, { onSettled: () => setRejecting(false) });
          }
        }}
        onCancel={() => setRejecting(false)}
      />
    </div>
  );
}

function Field({ label, mono, children }: { label: string; mono?: boolean; children: ReactNode }) {
  return (
    <div>
      <span className="font-mono text-[10px] font-semibold uppercase tracking-[0.16em] text-muted">{label}</span>
      <div className={`mt-0.5 text-sm text-ink ${mono ? 'font-mono text-xs break-all' : ''}`}>{children}</div>
    </div>
  );
}