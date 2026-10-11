import { useParams, useNavigate } from 'react-router-dom';
import { usePublicListRecords } from '../hooks/usePublicRepo';
import PageHeader from '../components/PageHeader';
import Button from '../components/Button';
import FlipChip from '../components/FlipChip';
import { CollectionRecordsView } from '../components/RepoExplorer';

export default function PublicCollectionRecords() {
  const { did, collection } = useParams<{ did: string; collection: string }>();
  const navigate = useNavigate();

  const query = usePublicListRecords(did ?? '', collection ?? '');
  const recordCount = query.data?.pages.flatMap(p => p.records).length ?? 0;

  return (
    <div className="mx-auto max-w-3xl">
      <Button variant="ghost" size="sm" className="mb-3 -ml-2" onClick={() => navigate(`/profile/${encodeURIComponent(did ?? '')}`)}>
        ← Back to profile
      </Button>

      <PageHeader
        eyebrow="repo · collection"
        title={collection}
        description={did}
        actions={<FlipChip tone="accent">{recordCount} records</FlipChip>}
      />

      <CollectionRecordsView
        did={did ?? ''}
        query={query}
        recordLink={(rkey) => `/profile/${encodeURIComponent(did ?? '')}/collections/${encodeURIComponent(collection ?? '')}/${encodeURIComponent(rkey)}`}
      />
    </div>
  );
}
