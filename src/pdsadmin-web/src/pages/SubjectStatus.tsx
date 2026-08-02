import { useState } from 'react';
import DidLink from '../components/DidLink';
import ConfirmDialog from '../components/ConfirmDialog';
import Button from '../components/Button';
import Badge from '../components/Badge';
import { Card } from '../components/Card';
import PageHeader from '../components/PageHeader';
import { Input } from '../components/Input';
import { useSubjectStatus } from '../hooks/useAccounts';
import { useUpdateSubjectStatus } from '../hooks/useAccounts';

export default function SubjectStatus() {
  const [did, setDid] = useState('');
  const [searchDid, setSearchDid] = useState('');
  const [message, setMessage] = useState('');
  const [confirm, setConfirm] = useState<{
    title: string;
    message: string;
    confirmLabel: string;
    confirmClass: string;
    action: () => void;
  } | null>(null);

  const { data: status, isPending, error } = useSubjectStatus(searchDid);
  const updateMutation = useUpdateSubjectStatus();

  function handleSearch(e: React.FormEvent) {
    e.preventDefault();
    setSearchDid(did);
    setMessage('');
  }

  function handleTakedown() {
    updateMutation.mutate(
      { subject: { did: searchDid }, takedown: { applied: true } },
      { onSuccess: () => setMessage('Takedown applied') },
    );
  }

  function handleUntakedown() {
    updateMutation.mutate(
      { subject: { did: searchDid }, takedown: { applied: false } },
      { onSuccess: () => setMessage('Takedown removed') },
    );
  }

  return (
    <div>
      <PageHeader
        eyebrow="moderation · subject"
        title="Subject Status"
        description="Look up and change the moderation state of any subject."
      />

      <form onSubmit={handleSearch} className="mb-4 flex max-w-xl gap-2">
        <div className="flex-1">
          <Input
            type="text"
            placeholder="DID..."
            value={did}
            onChange={e => setDid(e.target.value)}
          />
        </div>
        <Button variant="primary" type="submit">Search</Button>
      </form>

      {message && <p className="mb-4 text-sm text-success-deep dark:text-success">{message}</p>}
      {error && <p className="mb-4 text-sm text-danger">{error.message}</p>}
      {isPending && searchDid && <p className="text-sm text-secondary">Loading...</p>}

      {status && (
        <Card className="mb-4 max-w-xl space-y-3 p-5">
          <div>
            <span className="font-mono text-[10px] font-semibold uppercase tracking-[0.16em] text-muted">Subject DID</span>
            <div className="mt-0.5 font-mono text-xs text-ink">
              {status.subject.did ? <DidLink did={status.subject.did} /> : status.subject.uri}
            </div>
          </div>
          <div className="flex items-center gap-2">
            <span className="font-mono text-[10px] font-semibold uppercase tracking-[0.16em] text-muted">Takedown</span>
            {status.takedown?.applied ? <Badge tone="danger">applied</Badge> : <Badge tone="success">none</Badge>}
          </div>
          {status.takedown?.applied && (
            <div className="text-xs text-secondary">
              Ref: <span className="font-mono">{status.takedown.ref ?? 'default'}</span>
            </div>
          )}
          <div className="flex items-center gap-2">
            <span className="font-mono text-[10px] font-semibold uppercase tracking-[0.16em] text-muted">Deactivated</span>
            {status.deactivated?.applied ? <Badge tone="warning">yes</Badge> : <Badge tone="success">no</Badge>}
          </div>
          <div className="flex gap-2 pt-1">
            {!status.takedown?.applied && (
              <Button
                variant="danger"
                onClick={() => setConfirm({
                  title: 'Apply takedown',
                  message: `Apply takedown for ${searchDid}? This hides the subject from public views.`,
                  confirmLabel: 'Apply takedown',
                  confirmClass: 'bg-danger text-white dark:bg-danger-deep hover:opacity-90',
                  action: handleTakedown,
                })}
              >
                Apply takedown
              </Button>
            )}
            {status.takedown?.applied && (
              <Button
                variant="secondary"
                onClick={() => setConfirm({
                  title: 'Remove takedown',
                  message: `Remove takedown for ${searchDid}?`,
                  confirmLabel: 'Remove takedown',
                  confirmClass: 'bg-neutral text-white hover:opacity-90',
                  action: handleUntakedown,
                })}
              >
                Remove takedown
              </Button>
            )}
          </div>
        </Card>
      )}
      {confirm && (
        <ConfirmDialog
          open
          title={confirm.title}
          message={confirm.message}
          confirmLabel={confirm.confirmLabel}
          confirmClass={confirm.confirmClass}
          onConfirm={() => {
            confirm.action();
            setConfirm(null);
          }}
          onCancel={() => setConfirm(null)}
        />
      )}
    </div>
  );
}
