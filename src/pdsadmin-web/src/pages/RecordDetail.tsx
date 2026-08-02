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
        className="text-primary hover:text-primary-hover text-sm mb-2 font-medium focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
      >
        ← Back to collection
      </button>
      <div className="flex items-center justify-between mb-1">
        <h1 className="text-2xl font-bold break-all">{rkey}</h1>
        <button
          onClick={() => setRaw(!raw)}
          className="px-3 py-1 text-xs font-medium rounded-md bg-surface border border-input hover:bg-row-hover focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
        >
          {raw ? 'Tree' : 'Raw'}
        </button>
      </div>
      <p className="text-sm text-secondary font-mono mb-1">{did} / {collection}</p>
      {data?.cid && <p className="text-xs text-muted font-mono mb-4">CID: {data.cid}</p>}

      {error && <p className="text-danger mb-4">{error.message}</p>}
      {isPending && <p className="text-secondary">Loading...</p>}

      {data && (
        raw ? (
          <div className="bg-surface border border-subtle rounded-md p-4 overflow-auto">
            <pre className="text-xs font-mono leading-relaxed whitespace-pre-wrap break-all">
              {JSON.stringify(data.value, null, 2)}
            </pre>
          </div>
        ) : (
          <div className="bg-surface border border-subtle rounded-md p-4">
            <JsonTree value={data.value} did={did} />
          </div>
        )
      )}
    </div>
  );
}
