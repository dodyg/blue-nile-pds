import { useEffect, useState } from 'react';
import { xrpcGet, xrpcPost } from '../api/client';
import type { InviteCode } from '../types/admin';

export default function InviteCodes() {
  const [codes, setCodes] = useState<InviteCode[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  useEffect(() => { fetchCodes(); }, []);

  async function fetchCodes() {
    setLoading(true);
    setError('');
    try {
      const res = await xrpcGet<{ codes: InviteCode[] }>('com.atproto.admin.getInviteCodes');
      setCodes(res.codes);
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

  if (loading) return <div className="text-gray-500">Loading...</div>;

  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">Invite Codes</h1>
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
              <th className="p-3 font-medium" />
            </tr>
          </thead>
          <tbody>
            {codes.map(c => (
              <tr key={c.code} className="border-b border-gray-200">
                <td className="p-3 font-mono text-xs">{c.code}</td>
                <td className="p-3">{c.available}</td>
                <td className="p-3">{c.disabled ? 'Yes' : 'No'}</td>
                <td className="p-3">{c.uses?.length || 0}</td>
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
            ))}
            {codes.length === 0 && (
              <tr><td colSpan={5} className="p-6 text-center text-gray-400">No invite codes found</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
