import { useState } from 'react';
import Button from '../components/Button';
import Badge from '../components/Badge';
import PageHeader from '../components/PageHeader';
import Modal from '../components/Modal';
import { Card, CardHeader } from '../components/Card';
import { useAdvanceSequencer, useSequencerStatus } from '../hooks/useMaintenance';

export default function Maintenance() {
  const [target, setTarget] = useState('');
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  const statusQuery = useSequencerStatus();
  const advanceMutation = useAdvanceSequencer();

  const status = statusQuery.data;
  const current = status?.currentSeq ?? 0;
  const targetSeq = Number(target);

  const targetValid = Number.isInteger(targetSeq) && targetSeq > current;

  function handleReview() {
    setMessage('');
    setError('');
    if (targetValid) setConfirmOpen(true);
  }

  function handleConfirm(typed: string) {
    setConfirmOpen(false);
    if (typed !== String(targetSeq)) {
      setError(`Confirmation did not match — type ${targetSeq} exactly to confirm. No change was made.`);
      return;
    }
    advanceMutation.mutate(targetSeq, {
      onSuccess: (res) => {
        setMessage(`Sequence counter advanced from ${res.previousSeq ?? 0} to ${res.newSeq}. Next event will use seq ${res.newSeq + 1}.`);
        setTarget('');
      },
      onError: (e: Error) => setError(e.message),
    });
  }

  return (
    <div>
      <PageHeader
        eyebrow="operations · firehose"
        title="Maintenance"
        description="Sequencer and firehose operations. Use the sequence advance to recover consumers holding a cursor ahead of this host (e.g. after a database restore)."
      />

      {message && <p className="mb-4 text-sm text-success-deep dark:text-success">{message}</p>}
      {error && <p className="mb-4 text-sm text-danger">{error}</p>}
      {statusQuery.error && <p className="mb-4 text-sm text-danger">{statusQuery.error.message}</p>}

      <Card className="mb-6 p-5">
        <CardHeader title="Sequencer status" subtitle="Event sequence backing the firehose" />
        <div className="mt-3 grid grid-cols-1 gap-x-6 gap-y-3 sm:grid-cols-2">
          <div>
            <span className="font-mono text-[10px] font-semibold uppercase tracking-[0.16em] text-muted">Current Max Seq</span>
            <div className="mt-0.5 font-mono text-sm text-ink">{status ? (status.currentSeq ?? '—') : '…'}</div>
          </div>
          <div>
            <span className="font-mono text-[10px] font-semibold uppercase tracking-[0.16em] text-muted">Earliest Seq</span>
            <div className="mt-0.5 font-mono text-sm text-ink">{status ? (status.earliestSeq ?? '—') : '…'}</div>
          </div>
          <div>
            <span className="font-mono text-[10px] font-semibold uppercase tracking-[0.16em] text-muted">Event Count</span>
            <div className="mt-0.5 text-sm text-ink">{status?.eventCount ?? '…'}</div>
          </div>
          <div>
            <span className="font-mono text-[10px] font-semibold uppercase tracking-[0.16em] text-muted">Counter</span>
            <div className="mt-0.5 font-mono text-sm text-ink">{status ? (status.sequenceCounter ?? '—') : '…'}</div>
          </div>
          <div>
            <span className="font-mono text-[10px] font-semibold uppercase tracking-[0.16em] text-muted">Earliest Event</span>
            <div className="mt-0.5 text-sm text-ink">{status?.earliestTime ? new Date(status.earliestTime).toLocaleString() : '—'}</div>
          </div>
          <div>
            <span className="font-mono text-[10px] font-semibold uppercase tracking-[0.16em] text-muted">Latest Event</span>
            <div className="mt-0.5 text-sm text-ink">{status?.latestTime ? new Date(status.latestTime).toLocaleString() : '—'}</div>
          </div>
        </div>
      </Card>

      <Card className="mb-6 p-5">
        <CardHeader
          title="Advance sequence counter"
          subtitle="Fast-forward the counter so the next event continues after a cursor held by downstream consumers"
        />
        <div className="mt-3 flex items-center gap-2">
          <Badge tone="warning">irreversible</Badge>
          <span className="text-sm text-secondary">
            Skipped sequence numbers are never filled in. Only move forward, never backward.
          </span>
        </div>
        <div className="mt-4 flex flex-col gap-3 sm:flex-row">
          <input
            value={target}
            onChange={e => setTarget(e.target.value.replace(/[^0-9]/g, ''))}
            placeholder={`Greater than ${current}`}
            inputMode="numeric"
            className="flex-1 rounded-sm border border-subtle bg-page px-3 py-2 font-mono text-xs text-ink placeholder:text-muted focus:border-accent-ring focus:outline-none"
          />
          <Button variant="primary" onClick={handleReview} disabled={!targetValid || advanceMutation.isPending}>
            {advanceMutation.isPending ? 'Advancing…' : 'Review…'}
          </Button>
        </div>
        {target && !targetValid && (
          <p className="mt-2 text-sm text-danger">Target must be an integer greater than the current max seq ({current}).</p>
        )}
      </Card>

      <Modal
        open={confirmOpen}
        title="Advance sequence counter"
        label={`Type ${targetSeq} to confirm — moves ${current} → ${targetSeq}, skipping ${targetSeq - current} number(s), cannot be undone`}
        placeholder={String(targetSeq)}
        onConfirm={handleConfirm}
        onClose={() => setConfirmOpen(false)}
      />
    </div>
  );
}
