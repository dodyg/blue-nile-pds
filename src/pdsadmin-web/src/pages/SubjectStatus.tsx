import { useState } from 'react';
import { xrpcGet, xrpcPost } from '../api/client';
import type { GetSubjectStatusResponse, SubjectStatus } from '../types/admin';

export default function SubjectStatus() {
  const [did, setDid] = useState('');
  const [status, setStatus] = useState<SubjectStatus | SubjectStatus[] | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  async function handleSearch(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError('');
    setStatus(null);
    try {
      const res = await xrpcGet<GetSubjectStatusResponse>('com.atproto.admin.getSubjectStatus', { did });
      setStatus(res.subjectStatus);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Failed to get subject status');
    } finally {
      setLoading(false);
    }
  }

  async function handleTakedown() {
    setMessage('');
    setError('');
    try {
      await xrpcPost('com.atproto.admin.updateSubjectStatus', {
        subject: { did },
        takedown: { applied: true },
      });
      setMessage('Takedown applied');
      handleSearch({ preventDefault: () => {} } as React.FormEvent);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Takedown failed');
    }
  }

  async function handleUntakedown() {
    setMessage('');
    setError('');
    try {
      await xrpcPost('com.atproto.admin.updateSubjectStatus', {
        subject: { did },
        takedown: { applied: false },
      });
      setMessage('Takedown removed');
      handleSearch({ preventDefault: () => {} } as React.FormEvent);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Remove takedown failed');
    }
  }

  const items = status ? (Array.isArray(status) ? status : [status]) : [];

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
      {error && <p className="text-red-600 mb-4">{error}</p>}
      {loading && <p className="text-gray-500">Loading...</p>}
      {items.map((s, i) => (
        <div key={i} className="bg-white border border-gray-200 shadow-sm rounded-lg p-5 space-y-3 mb-4">
          <div>
            <span className="text-gray-500 text-sm">Subject DID</span>
            <div className="font-mono text-xs mt-0.5 text-gray-900">{s.subject.did || s.subject.uri}</div>
          </div>
          <div>
            <span className="text-gray-500 text-sm">Takedown</span>
            <div className="mt-0.5 text-gray-900">{s.takedown?.applied ? 'Applied' : 'None'}</div>
          </div>
          <div>
            <span className="text-gray-500 text-sm">Deactivated</span>
            <div className="mt-0.5 text-gray-900">{s.deactivated?.applied ? 'Yes' : 'No'}</div>
          </div>
          <div className="flex gap-2">
            {!s.takedown?.applied && (
              <button onClick={handleTakedown} className="px-4 py-2 bg-red-600 text-white rounded text-sm hover:bg-red-700">
                Apply takedown
              </button>
            )}
            {s.takedown?.applied && (
              <button onClick={handleUntakedown} className="px-4 py-2 bg-gray-100 text-gray-700 border border-gray-300 rounded text-sm hover:bg-gray-200">
                Remove takedown
              </button>
            )}
          </div>
        </div>
      ))}
    </div>
  );
}
