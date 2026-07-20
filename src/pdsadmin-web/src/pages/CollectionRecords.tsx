import { useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { useListRecords } from '../hooks/useRepo';
import JsonTree from '../components/JsonTree';

export default function CollectionRecords() {
  const { did, collection } = useParams<{ did: string; collection: string }>();
  const navigate = useNavigate();

  const { data, isPending, error, fetchNextPage, hasNextPage, isFetchingNextPage } = useListRecords(did ?? '', collection ?? '');

  useEffect(() => {
    if (hasNextPage && !isFetchingNextPage) {
      fetchNextPage();
    }
  }, [hasNextPage, isFetchingNextPage, fetchNextPage]);

  const records = data?.pages.flatMap(p => p.records) ?? [];

  function rkeyFromUri(uri: string) {
    const parts = uri.split('/');
    return parts[parts.length - 1];
  }

  return (
    <div>
      <button
        onClick={() => navigate(`/accounts/${encodeURIComponent(did ?? '')}`)}
        className="text-blue-600 hover:text-blue-700 text-sm mb-2 font-medium"
      >
        ← Back to account
      </button>
      <h1 className="text-2xl font-bold mb-1">{collection}</h1>
      <p className="text-sm text-gray-500 font-mono mb-4">{did}</p>
      <p className="text-xs text-gray-400 mb-4">{records.length} records</p>

      {error && <p className="text-red-600 mb-4">{error.message}</p>}
      {isPending && records.length === 0 && <p className="text-gray-500">Loading...</p>}
      {records.length === 0 && !isPending && <p className="text-gray-400">No records found in this collection</p>}

      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-gray-200 text-left text-gray-500">
              <th className="p-2 font-medium w-10">#</th>
              <th className="p-2 font-medium">Rkey</th>
              <th className="p-2 font-medium">CID</th>
              <th className="p-2 font-medium">Value</th>
            </tr>
          </thead>
          <tbody>
            {records.map((r, i) => {
              const rkey = rkeyFromUri(r.uri);
              return (
                <tr key={r.uri} className="border-b border-gray-200">
                  <td className="p-2 text-xs text-gray-400 align-top">{i + 1}</td>
                  <td className="p-2 align-top">
                    <Link
                      to={`/accounts/${encodeURIComponent(did ?? '')}/collections/${encodeURIComponent(collection ?? '')}/${encodeURIComponent(rkey)}`}
                      className="font-mono text-xs text-blue-600 hover:text-blue-700"
                    >
                      {rkey}
                    </Link>
                  </td>
                  <td className="p-2 align-top">
                    <span className="font-mono text-xs text-gray-400 break-all">{r.cid}</span>
                  </td>
                  <td className="p-2">
                    <JsonTree value={r.value} />
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
