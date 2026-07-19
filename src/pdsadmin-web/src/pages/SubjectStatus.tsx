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
          className="flex-1 px-3 py-2 rounded bg-white border border-gray-300 text-gray-900 focus:border-blue-500 focus:outline-none"
        />
        <button type="submit" className="px-4 py-2 bg-blue-600 text-white rounded text-sm font-medium hover:bg-blue-700">
          Search
        </button>
      </form>
      {message && <p className="text-green-600 mb-4">{message}</p>}
      {error && <p className="text-red-600 mb-4">{error.message}</p>}
      {isPending && searchDid && <p className="text-gray-500">Loading...</p>}
      {status && (
        <div className="bg-white border border-gray-200 shadow-sm rounded-lg p-5 space-y-3 mb-4">
          <div>
            <span className="text-gray-500 text-sm">Subject DID</span>
            <div className="font-mono text-xs mt-0.5 text-gray-900">
              {status.subject.did ? <DidLink did={status.subject.did} /> : status.subject.uri}
            </div>
          </div>
          <div>
            <span className="text-gray-500 text-sm">Takedown</span>
            <div className="mt-0.5 text-gray-900">{status.takedown?.applied ? `Applied (ref: ${status.takedown.ref ?? 'default'})` : 'None'}</div>
          </div>
          <div>
            <span className="text-gray-500 text-sm">Deactivated</span>
            <div className="mt-0.5 text-gray-900">{status.deactivated?.applied ? 'Yes' : 'No'}</div>
          </div>
          <div className="flex gap-2">
            {!status.takedown?.applied && (
              <button
                onClick={() => setConfirm({
                  title: 'Apply takedown',
                  message: `Apply takedown for ${searchDid}? This hides the subject from public views.`,
                  confirmLabel: 'Apply takedown',
                  confirmClass: 'bg-red-600 hover:bg-red-700',
                  action: handleTakedown,
                })}
                className="px-4 py-2 bg-red-600 text-white rounded text-sm hover:bg-red-700"
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
                  confirmClass: 'bg-gray-600 hover:bg-gray-700',
                  action: handleUntakedown,
                })}
                className="px-4 py-2 bg-gray-100 text-gray-700 border border-gray-300 rounded text-sm hover:bg-gray-200"
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
