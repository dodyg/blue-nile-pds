import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { xrpcGet, xrpcPost } from '../api/client';
import DidLink from '../components/DidLink';
import type { InviteCode } from '../types/admin';

export default function InviteCodes() {
  const navigate = useNavigate();
  const [codes, setCodes] = useState<InviteCode[]>([]);
  const [cursor, setCursor] = useState<string | undefined>();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [expandedCode, setExpandedCode] = useState<string | null>(null);

  useEffect(() => { fetchCodes(); }, []);

  async function fetchCodes(cursorVal?: string) {
    setLoading(true);
    setError('');
    try {
      const params: Record<string, string> = { limit: '100' };
      if (cursorVal) params.cursor = cursorVal;
      const res = await xrpcGet<{ codes: InviteCode[]; cursor?: string }>('com.atproto.admin.getInviteCodes', params);
      setCodes(prev => cursorVal ? [...prev, ...res.codes] : res.codes);
      setCursor(res.cursor);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Failed to load invite codes');
    } finally {
      setLoading(false);
    }
  }

  async function disableCode(code: string) {
    setMessage('');
    try {
      await xrpcPost('com.atproto.admin.disableInviteCodes', { codes: [code] });
      setMessage(`Code ${code} disabled`);
      fetchCodes();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Failed to disable code');
    }
  }

  return (
    <div>
      <div className="flex items-center justify-between mb-4">
        <h1 className="text-2xl font-bold">Invite Codes</h1>
        <button
          onClick={() => navigate('/invites/create')}
          className="px-4 py-2 bg-blue-600 text-white rounded text-sm font-medium hover:bg-blue-700"
        >
          Create invite codes
        </button>
      </div>
      {message && <p className="text-green-600 mb-4">{message}</p>}
      {error && <p className="text-red-600 mb-4">{error}</p>}
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-gray-200 text-left text-gray-500">
              <th className="p-3 font-medium">Code</th>
              <th className="p-3 font-medium">Available</th>
              <th className="p-3 font-medium">Disabled</th>
              <th className="p-3 font-medium">Uses</th>
              <th className="p-3 font-medium">For Account</th>
              <th className="p-3 font-medium">Created By</th>
              <th className="p-3 font-medium">Created At</th>
              <th className="p-3 font-medium" />
            </tr>
          </thead>
          <tbody>
            {codes.map(c => (
              <>
                <tr key={c.code} className="border-b border-gray-200">
                  <td className="p-3 font-mono text-xs">
                    <button
                      onClick={() => setExpandedCode(expandedCode === c.code ? null : c.code)}
                      className="mr-2 text-gray-400 hover:text-gray-600 text-xs"
                    >
                      {expandedCode === c.code ? '▼' : '▶'}
                    </button>
                    {c.code}
                  </td>
                  <td className="p-3">{c.available}</td>
                  <td className="p-3">{c.disabled ? 'Yes' : 'No'}</td>
                  <td className="p-3">{c.uses?.length || 0}</td>
                  <td className="p-3 font-mono text-xs truncate max-w-[150px]" title={c.forAccount}>
                    {c.forAccount ? <DidLink did={c.forAccount} /> : '—'}
                  </td>
                  <td className="p-3 font-mono text-xs truncate max-w-[150px]" title={c.createdBy}>
                    {c.createdBy ? <DidLink did={c.createdBy} /> : '—'}
                  </td>
                  <td className="p-3 text-xs">{c.createdAt ? new Date(c.createdAt).toLocaleString() : '—'}</td>
                  <td className="p-3">
                    {!c.disabled && (
                      <button
                        onClick={() => disableCode(c.code)}
                        className="text-red-600 hover:text-red-700 text-xs font-medium"
                      >
                        Disable
                      </button>
                    )}
                  </td>
                </tr>
                {expandedCode === c.code && c.uses && c.uses.length > 0 && (
                  <tr key={`${c.code}-uses`} className="bg-gray-50">
                    <td colSpan={8} className="p-3">
                      <table className="w-full text-xs">
                        <thead>
                          <tr className="text-left text-gray-500 border-b border-gray-200">
                            <th className="pb-1 pr-4 font-medium">Used By</th>
                            <th className="pb-1 font-medium">Used At</th>
                          </tr>
                        </thead>
                        <tbody>
                          {c.uses.map((u, i) => (
                            <tr key={i} className="border-b border-gray-100">
                              <td className="py-1.5 pr-4 font-mono">
                                <DidLink did={u.usedBy} />
                              </td>
                              <td className="py-1.5">{new Date(u.usedAt).toLocaleString()}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </td>
                  </tr>
                )}
              </>
            ))}
            {codes.length === 0 && !loading && (
              <tr><td colSpan={8} className="p-6 text-center text-gray-400">No invite codes found</td></tr>
            )}
          </tbody>
        </table>
      </div>
      {cursor && (
        <button
          onClick={() => fetchCodes(cursor)}
          disabled={loading}
          className="mt-4 px-4 py-2 bg-white border border-gray-300 rounded text-sm hover:bg-gray-50 disabled:opacity-50"
        >
          {loading ? 'Loading...' : 'Load more'}
        </button>
      )}
    </div>
  );
}
