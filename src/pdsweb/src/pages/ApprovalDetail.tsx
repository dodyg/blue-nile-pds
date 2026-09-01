import { useState, type ReactNode } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { usePendingAdminDetail, usePendingApprove, usePendingReject } from '../hooks/useAdminPending';
import Button from '../components/Button';
import Badge from '../components/Badge';
import { Card } from '../components/Card';
import PageHeader from '../components/PageHeader';
import ConfirmDialog from '../components/ConfirmDialog';
import { XrpcError } from '../api/queryClient';

function errMessage(err: unknown): string | null {
  if (err instanceof XrpcError) return err.message || err.error || 'Something went wrong';
  return 'Something went wrong';
}

export default function ApprovalDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const numericId = id != null ? Number(id) : null;
  const isValidId = numericId != null && Number.isInteger(numericId) && numericId > 0;

  const { data, isPending, error } = usePendingAdminDetail(isValidId ? numericId : null);
  const approve = usePendingApprove();
  const reject = usePendingReject();

  const [showApprove, setShowApprove] = useState(false);
  const [showReject, setShowReject] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);

  if (!isValidId) {
    return <p className="text-sm text-danger">Invalid approval id.</p>;
  }

  if (isPending) return <p className="text-sm text-secondary">Loading…</p>;
  if (error) return <p className="text-sm text-danger">{errMessage(error)}</p>;
  if (!data) return <p className="text-sm text-secondary">Registration not found.</p>;

  const isProcessed = data.status.toLowerCase() !== 'pending';

  return (
    <div>
      <Button variant="ghost" size="sm" className="mb-3 -ml-2" onClick={() => navigate('/admin/approvals')}>
        ← Back to approvals
      </Button>

      <PageHeader
        eyebrow="moderation · approvals"
        title={data.handle}
        description={data.email}
        actions={(
          <>
            <Badge tone={data.status.toLowerCase() === 'approved' ? 'success' : data.status.toLowerCase() === 'rejected' ? 'danger' : 'warning'}>
              {data.status}
            </Badge>
            {data.emailConfirmed ? <Badge tone="success">email confirmed</Badge> : <Badge tone="warning">email unconfirmed</Badge>}
          </>
        )}
      />

      <Card className="mb-6 p-5">
        <div className="grid grid-cols-1 gap-x-6 gap-y-3 sm:grid-cols-2">
          <Field label="Handle" mono>{data.handle}</Field>
          <Field label="Email" mono>{data.email}</Field>
          <Field label="Display name">{data.displayName || '—'}</Field>
          <Field label="Location">{data.location || '—'}</Field>
          <Field label="Account type">{data.accountType || 'individual'}</Field>
          <Field label="Invite code" mono>{data.inviteCode || '—'}</Field>
          <Field label="Email confirmed">{data.emailConfirmed ? 'Yes' : 'No'}</Field>
          <Field label="Status">{data.status}</Field>
          <Field label="Requested">{new Date(data.createdAt).toLocaleString()}</Field>
          <Field label="Updated">{new Date(data.updatedAt).toLocaleString()}</Field>
          {data.description && <Field label="Bio">{data.description}</Field>}
        </div>
      </Card>

      {!isProcessed && (
        <div className="mb-4 flex items-center gap-3">
          <Button
            variant="primary"
            onClick={() => setShowApprove(true)}
            disabled={approve.isPending || reject.isPending}
          >
            Approve
          </Button>
          <Button
            variant="ghost"
            onClick={() => setShowReject(true)}
            disabled={approve.isPending || reject.isPending}
          >
            Reject
          </Button>
          <Link to="/admin/approvals" className="text-sm text-secondary hover:text-ink hover:underline">
            Back to list
          </Link>
        </div>
      )}

      {isProcessed && (
        <p className="mb-4 text-sm text-secondary">
          This registration has already been {data.status.toLowerCase()}.
          <Link to="/admin/approvals" className="ml-2 text-primary hover:underline">Back to list</Link>
        </p>
      )}

      {actionSuccess && <p className="mb-2 text-sm text-success-deep">{actionSuccess}</p>}
      {actionError && <p className="mb-2 text-sm text-danger">{actionError}</p>}
      {approve.error && <p className="mb-2 text-sm text-danger">{errMessage(approve.error)}</p>}
      {reject.error && <p className="mb-2 text-sm text-danger">{errMessage(reject.error)}</p>}

      <ConfirmDialog
        open={showApprove}
        title="Approve account?"
        message={`This will create the PDS account for ${data.handle} and send a confirmation email.`}
        confirmLabel="Approve"
        onConfirm={() => {
          setActionError(null);
          setActionSuccess(null);
          approve.mutate({ id: data.id }, {
            onSuccess: () => {
              setShowApprove(false);
              setActionSuccess(`Approved ${data.handle}.`);
            },
            onError: (e) => setActionError(errMessage(e)),
          });
        }}
        onCancel={() => setShowApprove(false)}
      />

      <Card className={showReject ? 'mt-4 p-4' : 'hidden'}>
        <p className="text-sm font-medium text-ink">Reject account?</p>
        <p className="mt-1 text-sm text-secondary">The user will be notified by email.</p>
        <div className="mt-3 flex items-center gap-2">
          <Button
            variant="danger"
            onClick={() => {
              setActionError(null);
              setActionSuccess(null);
              reject.mutate({ id: data.id }, {
                onSuccess: () => {
                  setShowReject(false);
                  setActionSuccess(`Rejected ${data.handle}.`);
                },
                onError: (e) => setActionError(errMessage(e)),
              });
            }}
            disabled={reject.isPending}
          >
            {reject.isPending ? 'Rejecting…' : 'Reject'}
          </Button>
          <Button variant="ghost" onClick={() => setShowReject(false)}>Cancel</Button>
        </div>
      </Card>
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
