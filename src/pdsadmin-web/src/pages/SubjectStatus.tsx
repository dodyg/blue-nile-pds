import { useState } from 'react';
import DidLink from '../components/DidLink';
import ConfirmDialog from '../components/ConfirmDialog';
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
      <h1 className="text-2xl font-bold mb-4">Subject Status</h1>
      <form onSubmit={handleSearch} className="flex gap-2 mb-4">
        <input
          type="text"
          placeholder="DID..."
          value={did}
          onChange={e => setDid(e.target.value)}
          className="flex-1 px-3 py-2 rounded-md bg-surface border border-input text-ink focus:border-focus-ring focus:outline-none"
        />
        <button type="submit" className="px-4 py-2 bg-primary text-surface rounded-md text-sm font-medium hover:bg-primary-hover focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring">
          Search
        </button>
      </form>
      {message && <p className="text-success mb-4">{message}</p>}
      {error && <p className="text-danger mb-4">{error.message}</p>}
      {isPending && searchDid && <p className="text-secondary">Loading...</p>}
      {status && (
        <div className="bg-surface border border-subtle shadow-card rounded-md p-5 space-y-3 mb-4">
          <div>
            <span className="text-secondary text-sm">Subject DID</span>
            <div className="font-mono text-xs mt-0.5 text-ink">
              {status.subject.did ? <DidLink did={status.subject.did} /> : status.subject.uri}
            </div>
          </div>
          <div>
            <span className="text-secondary text-sm">Takedown</span>
            <div className="mt-0.5 text-ink">{status.takedown?.applied ? `Applied (ref: ${status.takedown.ref ?? 'default'})` : 'None'}</div>
          </div>
          <div>
            <span className="text-secondary text-sm">Deactivated</span>
            <div className="mt-0.5 text-ink">{status.deactivated?.applied ? 'Yes' : 'No'}</div>
          </div>
          <div className="flex gap-2">
            {!status.takedown?.applied && (
              <button
                onClick={() => setConfirm({
                  title: 'Apply takedown',
                  message: `Apply takedown for ${searchDid}? This hides the subject from public views.`,
                  confirmLabel: 'Apply takedown',
                  confirmClass: 'bg-danger hover:bg-danger-hover',
                  action: handleTakedown,
                })}
                className="px-4 py-2 bg-danger text-surface rounded-md text-sm hover:bg-danger-hover focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
              >
                Apply takedown
              </button>
            )}
            {status.takedown?.applied && (
              <button
                onClick={() => setConfirm({
                  title: 'Remove takedown',
                  message: `Remove takedown for ${searchDid}?`,
                  confirmLabel: 'Remove takedown',
                  confirmClass: 'bg-neutral hover:bg-ghost',
                  action: handleUntakedown,
                })}
                className="px-4 py-2 bg-hover text-ghost border border-input rounded-md text-sm hover:bg-subtle focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
              >
                Remove takedown
              </button>
            )}
          </div>
        </div>
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
