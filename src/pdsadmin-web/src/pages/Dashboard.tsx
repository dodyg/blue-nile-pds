import { useEffect, useState } from 'react';
import { xrpcGet } from '../api/client';
import type { ListReposResponse } from '../types/admin';

export default function Dashboard() {
  const [totalAccounts, setTotalAccounts] = useState<number | null>(null);
  const [error, setError] = useState('');

  useEffect(() => {
    xrpcGet<ListReposResponse>('com.atproto.sync.listRepos', { limit: '1' })
      .then(() => {
        xrpcGet<ListReposResponse>('com.atproto.sync.listRepos')
          .then(res => setTotalAccounts(res.repos.length))
          .catch(e => setError(e.message));
      })
      .catch(e => setError(e.message));
  }, []);

  return (
    <div>
      <h1 className="text-2xl font-bold mb-6">Dashboard</h1>
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div className="bg-white border border-gray-200 shadow-sm rounded-lg p-5">
          <div className="text-sm text-gray-500 mb-1">Total Accounts</div>
          <div className="text-3xl font-bold">
            {totalAccounts !== null ? totalAccounts : '...'}
          </div>
        </div>
      </div>
      {error && <p className="text-red-600 mt-4">{error}</p>}
    </div>
  );
}
