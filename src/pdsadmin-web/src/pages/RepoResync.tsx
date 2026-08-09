import { useState } from 'react';
import Button from '../components/Button';
import Badge from '../components/Badge';
import PageHeader from '../components/PageHeader';
import { Card, CardHeader } from '../components/Card';
import { useRepoResyncStatus, useStartRepoResync } from '../hooks/useRepoResync';

export default function RepoResync() {
  const [did, setDid] = useState('');
  const [message, setMessage] = useState('');

  const statusQuery = useRepoResyncStatus();
  const startMutation = useStartRepoResync();

  const status = statusQuery.data;
  const isRunning = status?.status === 'running' || startMutation.isPending;

  function handleStart() {
    setMessage('');
    startMutation.mutate(did.trim(), {
      onSuccess: () => setMessage(`Repo resync started for ${did.trim()}`),
      onError: (e: Error) => setMessage(e.message),
    });
  }

  return (
    <div>
      <PageHeader
        eyebrow="operations · repair"
        title="Repo Resync"
        description="Re-encode a DID's record blocks in canonical DAG-CBOR and rebuild the repo. Rewrites repo history; use only to repair canonical-format divergence."
      />

      {message && <p className="mb-4 text-sm text-success-deep dark:text-success">{message}</p>}
      {startMutation.error && <p className="mb-4 text-sm text-danger">{startMutation.error.message}</p>}
      {statusQuery.error && <p className="mb-4 text-sm text-danger">{statusQuery.error.message}</p>}

      <Card className="mb-6 p-5">
        <CardHeader title="Start a resync" subtitle="Target a single DID by did:plc:… or did:web:…" />
        <div className="mt-4 flex flex-col gap-3 sm:flex-row">
          <input
            value={did}
            onChange={e => setDid(e.target.value)}
            placeholder="did:plc:…"
            className="flex-1 rounded-sm border border-subtle bg-page px-3 py-2 font-mono text-xs text-ink placeholder:text-muted focus:border-accent-ring focus:outline-none"
          />
          <Button variant="primary" onClick={handleStart} disabled={isRunning || !did.trim()}>
            {isRunning ? 'Resyncing…' : 'Start resync'}
          </Button>
        </div>
      </Card>

      <Card className="mb-6 p-5">
        <CardHeader title="Latest run" />
        <div className="mt-3 grid grid-cols-1 gap-x-6 gap-y-3 sm:grid-cols-2">
          <div>
            <span className="font-mono text-[10px] font-semibold uppercase tracking-[0.16em] text-muted">Status</span>
            <div className="mt-0.5">
              {status ? <Badge tone={status.status === 'failed' ? 'danger' : status.status === 'running' ? 'warning' : 'success'}>{status.status}</Badge> : '—'}
            </div>
          </div>
          <div>
            <span className="font-mono text-[10px] font-semibold uppercase tracking-[0.16em] text-muted">DID</span>
            <div className="mt-0.5 break-all font-mono text-xs text-ink">{status?.did || '—'}</div>
          </div>
          <div>
            <span className="font-mono text-[10px] font-semibold uppercase tracking-[0.16em] text-muted">Started At</span>
            <div className="mt-0.5 text-sm text-ink">{status?.startedAt ? new Date(status.startedAt).toLocaleString() : '—'}</div>
          </div>
          <div>
            <span className="font-mono text-[10px] font-semibold uppercase tracking-[0.16em] text-muted">Completed At</span>
            <div className="mt-0.5 text-sm text-ink">{status?.completedAt ? new Date(status.completedAt).toLocaleString() : '—'}</div>
          </div>
          <div>
            <span className="font-mono text-[10px] font-semibold uppercase tracking-[0.16em] text-muted">Records Scanned</span>
            <div className="mt-0.5 text-sm text-ink">{status?.recordsScanned ?? '—'}</div>
          </div>
          <div>
            <span className="font-mono text-[10px] font-semibold uppercase tracking-[0.16em] text-muted">Records Rewritten</span>
            <div className="mt-0.5 text-sm text-ink">{status?.recordsRewritten ?? '—'}</div>
          </div>
        </div>
        {status?.status === 'running' && (
          <div className="mt-4 flex items-center gap-3 rounded-md border border-subtle bg-surface p-4 shadow-card">
            <span className="h-4 w-4 animate-spin rounded-full border-2 border-primary border-t-transparent" aria-hidden="true" />
            <span className="font-mono text-xs uppercase tracking-[0.12em] text-neutral">Resync in progress · page refreshes automatically</span>
          </div>
        )}
        {status?.status === 'failed' && status.error && (
          <div className="mt-4 flex items-center gap-2 rounded-md border border-danger/40 bg-danger/10 p-4">
            <Badge tone="danger">failed</Badge>
            <span className="text-sm text-danger-deep dark:text-danger">{status.error}</span>
          </div>
        )}
      </Card>
    </div>
  );
}
