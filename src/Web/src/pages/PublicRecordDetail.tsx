import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { usePublicGetRecord } from '../hooks/usePublicRepo';
import PageHeader from '../components/PageHeader';
import Button from '../components/Button';
import { RecordDetailView } from '../components/RepoExplorer';

export default function PublicRecordDetail() {
  const { did, collection, rkey } = useParams<{ did: string; collection: string; rkey: string }>();
  const navigate = useNavigate();
  const [raw, setRaw] = useState(false);

  const query = usePublicGetRecord(did ?? '', collection ?? '', rkey ?? '');

  return (
    <div className="mx-auto max-w-3xl">
      <Button variant="ghost" size="sm" className="mb-3 -ml-2" onClick={() => navigate(`/profile/${encodeURIComponent(did ?? '')}/collections/${encodeURIComponent(collection ?? '')}`)}>
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

      <RecordDetailView did={did} raw={raw} query={query} />
    </div>
  );
}
