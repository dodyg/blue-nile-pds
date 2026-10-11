import { useParams, useNavigate } from 'react-router-dom';
import { useListRecords } from '../hooks/useRepo';
import PageHeader from '../components/PageHeader';
import Button from '../components/Button';
import FlipChip from '../components/FlipChip';
import { CollectionRecordsView } from '../components/RepoExplorer';

export default function CollectionRecords() {
  const { did, collection } = useParams<{ did: string; collection: string }>();
  const navigate = useNavigate();

  const query = useListRecords(did ?? '', collection ?? '');
  const recordCount = query.data?.pages.flatMap(p => p.records).length ?? 0;

  return (
    <div>
      <Button variant="ghost" size="sm" className="mb-3 -ml-2" onClick={() => navigate(`/admin/accounts/${encodeURIComponent(did ?? '')}`)}>
        ← Back to account
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
        recordLink={(rkey) => `/admin/accounts/${encodeURIComponent(did ?? '')}/collections/${encodeURIComponent(collection ?? '')}/${encodeURIComponent(rkey)}`}
      />
    </div>
  );
}
