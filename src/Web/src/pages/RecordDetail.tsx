import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useGetRecord } from '../hooks/useRepo';
import JsonTree from '../components/JsonTree';
import PageHeader from '../components/PageHeader';
import Button from '../components/Button';
import FlipChip from '../components/FlipChip';
import { Card } from '../components/Card';

export default function RecordDetail() {
  const { did, collection, rkey } = useParams<{ did: string; collection: string; rkey: string }>();
  const navigate = useNavigate();
  const [raw, setRaw] = useState(false);

  const { data, isPending, error } = useGetRecord(did ?? '', collection ?? '', rkey ?? '');

  return (
    <div>
      <Button variant="ghost" size="sm" className="mb-3 -ml-2" onClick={() => navigate(`/admin/accounts/${encodeURIComponent(did ?? '')}/collections/${encodeURIComponent(collection ?? '')}`)}>
        ← Back to collection
      </Button>

      <PageHeader
        eyebrow="repo · record"
        title={rkey}
        description={`${did} / ${collection}`}
        actions={
          <Button variant="secondary" size="sm" onClick={() => setRaw(!raw)}>
            {raw ? 'Tree' : 'Raw'}
          </Button>
        }
      />

      {data?.cid && (
        <div className="mb-4">
          <FlipChip tone="accent" title="Content identifier">
            CID: {data.cid}
          </FlipChip>
        </div>
      )}

      {error && <p className="mb-4 text-sm text-danger">{error.message}</p>}
      {isPending && <p className="text-sm text-secondary">Loading...</p>}

      {data && (
        raw ? (
          <Card className="overflow-auto p-4">
            <pre className="whitespace-pre-wrap break-all font-mono text-xs leading-relaxed">
              {JSON.stringify(data.value, null, 2)}
            </pre>
          </Card>
        ) : (
          <Card className="p-4">
            <JsonTree value={data.value} did={did} />
          </Card>
        )
      )}
    </div>
  );
}
