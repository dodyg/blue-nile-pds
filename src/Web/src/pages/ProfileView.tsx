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
                <p className="truncate font-mono text-sm text-secondary">
                  <a
                    href={`https://bsky.app/profile/${encodeURIComponent(lookup.data.handle)}`}
                    target="_blank"
                    rel="noreferrer"
                    className="inline-flex items-center gap-1 text-primary underline decoration-subtle underline-offset-2 transition-colors hover:text-primary-hover hover:decoration-accent"
                  >
                    @{lookup.data.handle}
                    <svg className="h-3 w-3" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24" aria-hidden="true">
                      <path strokeLinecap="round" strokeLinejoin="round" d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6" />
                      <path strokeLinecap="round" strokeLinejoin="round" d="M15 3h6v6" />
                      <path strokeLinecap="round" strokeLinejoin="round" d="M10 14 21 3" />
                    </svg>
                  </a>
                </p>
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