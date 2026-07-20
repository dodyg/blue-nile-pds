import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useGetRecord } from '../hooks/useRepo';
import JsonTree from '../components/JsonTree';

export default function RecordDetail() {
  const { did, collection, rkey } = useParams<{ did: string; collection: string; rkey: string }>();
  const navigate = useNavigate();
  const [raw, setRaw] = useState(false);

  const { data, isPending, error } = useGetRecord(did ?? '', collection ?? '', rkey ?? '');

  return (
    <div>
      <button
        onClick={() => navigate(`/accounts/${encodeURIComponent(did ?? '')}/collections/${encodeURIComponent(collection ?? '')}`)}
        className="text-blue-600 hover:text-blue-700 text-sm mb-2 font-medium"
      >
        ← Back to collection
      </button>
      <div className="flex items-center justify-between mb-1">
        <h1 className="text-2xl font-bold break-all">{rkey}</h1>
        <button
          onClick={() => setRaw(!raw)}
          className="px-3 py-1 text-xs font-medium rounded bg-white border border-gray-300 hover:bg-gray-50"
        >
          {raw ? 'Tree' : 'Raw'}
        </button>
      </div>
      <p className="text-sm text-gray-500 font-mono mb-1">{did} / {collection}</p>
      {data?.cid && <p className="text-xs text-gray-400 font-mono mb-4">CID: {data.cid}</p>}

      {error && <p className="text-red-600 mb-4">{error.message}</p>}
      {isPending && <p className="text-gray-500">Loading...</p>}

      {data && (
        raw ? (
          <div className="bg-white border border-gray-200 rounded-lg p-4 overflow-auto">
            <pre className="text-xs font-mono leading-relaxed whitespace-pre-wrap break-all">
              {JSON.stringify(data.value, null, 2)}
            </pre>
          </div>
        ) : (
          <div className="bg-white border border-gray-200 rounded-lg p-4">
            <JsonTree value={data.value} did={did} />
          </div>
        )
      )}
    </div>
  );
}
