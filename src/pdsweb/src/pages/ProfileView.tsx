import { useParams } from 'react-router-dom';
import { useLookup } from '../hooks/useDirectory';
import AsyncState from '../components/AsyncState';
import { Card } from '../components/Card';

const AVATAR_URL = (did: string, cid: string) =>
  `/xrpc/com.atproto.sync.getBlob?did=${encodeURIComponent(did)}&cid=${encodeURIComponent(cid)}`;

export default function ProfileView() {
  const { did } = useParams<{ did: string }>();
  const lookup = useLookup(did ?? 'did:placeholder');

  return (
    <div className="mx-auto max-w-xl">
      <Card>
        <AsyncState loading={lookup.isPending} error={lookup.isError}>
          {lookup.data && (
            <div className="flex items-start gap-4 px-4 py-4">
              {lookup.data.avatarCid ? (
                <img
                  src={AVATAR_URL(lookup.data.did, lookup.data.avatarCid)}
                  alt=""
                  className="h-16 w-16 shrink-0 rounded-full object-cover"
                />
              ) : (
                <div className="flex h-16 w-16 shrink-0 items-center justify-center rounded-full bg-board text-xl font-bold text-board-text">
                  {(lookup.data.displayName || lookup.data.handle)[0].toUpperCase()}
                </div>
              )}
              <div className="min-w-0">
                <h1 className="text-lg font-bold text-ink">{lookup.data.displayName ?? lookup.data.handle}</h1>
                <p className="truncate font-mono text-sm text-secondary">@{lookup.data.handle}</p>
                {lookup.data.description && (
                  <p className="mt-2 whitespace-pre-wrap text-sm text-secondary">{lookup.data.description}</p>
                )}
              </div>
            </div>
          )}
          {lookup.isSuccess && !lookup.data && (
            <p className="px-4 py-6 text-center text-sm text-secondary">
              No account found for that did.
            </p>
          )}
        </AsyncState>
      </Card>
    </div>
  );
}