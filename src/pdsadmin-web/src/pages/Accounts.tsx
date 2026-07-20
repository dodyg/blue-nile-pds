import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useSearchAccounts } from '../hooks/useAccounts';

export default function Accounts() {
  const navigate = useNavigate();
  const [query, setQuery] = useState('');
  const [email, setEmail] = useState('');

  const { data, isPending, error, fetchNextPage, hasNextPage, isFetchingNextPage } = useSearchAccounts(email);

  const accounts = data?.pages.flatMap(p => p.accounts) ?? [];

  function handleSearch(e: React.FormEvent) {
    e.preventDefault();
    setEmail(query);
  }

  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">Accounts</h1>
      <form onSubmit={handleSearch} className="flex gap-2 mb-4">
        <input
          type="text"
          placeholder="Search by email..."
          value={query}
          onChange={e => setQuery(e.target.value)}
          className="flex-1 px-3 py-2 rounded bg-white border border-gray-300 text-gray-900 focus:border-blue-500 focus:outline-none"
        />
        <button type="submit" className="px-4 py-2 bg-blue-600 text-white rounded text-sm font-medium hover:bg-blue-700">
          Search
        </button>
      </form>
      {error && <p className="text-red-600 mb-4">{error.message}</p>}
      {isPending && accounts.length === 0 && <p className="text-gray-500 mb-4">Loading...</p>}
      {accounts.length === 0 && !isPending && <p className="text-gray-400 mb-4">No accounts found</p>}
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-gray-200 text-left text-gray-500">
              <th className="p-3 font-medium">DID</th>
              <th className="p-3 font-medium">Handle</th>
              <th className="p-3 font-medium">Email</th>
              <th className="p-3 font-medium">Status</th>
              <th className="p-3 font-medium" />
            </tr>
          </thead>
          <tbody>
            {accounts.map(acc => (
              <tr key={acc.did} className="border-b border-gray-200 hover:bg-gray-50">
                <td className="p-3 font-mono text-xs truncate max-w-[200px]">{acc.did}</td>
                <td className="p-3">{acc.handle}</td>
                <td className="p-3 text-gray-500">{acc.email || '—'}</td>
                <td className="p-3">
                  <div className="flex gap-1 flex-wrap">
                    {acc.invitesDisabled && <span className="px-2 py-0.5 bg-yellow-100 text-yellow-800 rounded text-xs">invites off</span>}
                    {acc.deactivatedAt && <span className="px-2 py-0.5 bg-gray-100 text-gray-600 rounded text-xs">deactivated</span>}
                  </div>
                </td>
                <td className="p-3">
                  <button
                    onClick={() => navigate(`/accounts/${encodeURIComponent(acc.did)}`)}
                    className="text-blue-600 hover:text-blue-700 text-xs font-medium"
                  >
                    View
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {hasNextPage && (
        <button
          onClick={() => fetchNextPage()}
          disabled={isFetchingNextPage}
          className="mt-4 px-4 py-2 bg-white border border-gray-300 rounded text-sm hover:bg-gray-50 disabled:opacity-50"
        >
          {isFetchingNextPage ? 'Loading...' : 'Load more'}
        </button>
      )}
    </div>
  );
}
