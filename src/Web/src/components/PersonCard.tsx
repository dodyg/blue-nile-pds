import { Link } from 'react-router-dom';
import type { Person } from '../types/pds';

const AVATAR_URL = (did: string, cid: string) =>
  `/xrpc/com.atproto.sync.getBlob?did=${encodeURIComponent(did)}&cid=${encodeURIComponent(cid)}`;

export default function PersonCard({ person }: { person: Person }) {
  const name = person.displayName ?? person.handle;

  return (
    <Link
      to={`/profile/${encodeURIComponent(person.did)}`}
      className="flex items-start gap-3 rounded-md border border-subtle bg-surface p-4 shadow-card transition-colors hover:border-accent"
    >
      {person.avatarCid ? (
        <img
          src={AVATAR_URL(person.did, person.avatarCid)}
          alt=""
          className="h-10 w-10 shrink-0 rounded-full object-cover"
        />
      ) : (
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-board text-sm font-bold text-board-text">
          {person.displayName ? name[0].toUpperCase() : '@'}
        </div>
      )}
      <div className="min-w-0">
        <p className="truncate text-sm font-semibold text-ink">{person.displayName}</p>
        <p className="truncate font-mono text-xs text-secondary">@{person.handle}</p>
        {person.description && (
          <p className="mt-1 line-clamp-2 text-xs text-muted">{person.description}</p>
        )}
      </div>
    </Link>
  );
}