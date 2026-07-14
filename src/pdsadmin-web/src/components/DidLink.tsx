import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { xrpcGet } from '../api/client';

const handleCache = new Map<string, string | null>();

interface Props {
  did: string;
}

export default function DidLink({ did }: Props) {
  const [handle, setHandle] = useState<string | null>(handleCache.get(did) ?? null);
  const [loading, setLoading] = useState(!handleCache.has(did));

  useEffect(() => {
    if (handleCache.has(did)) return;
    let cancelled = false;
    xrpcGet<{ handle: string }>('com.atproto.admin.getAccountInfo', { did })
      .then(res => {
        if (!cancelled) {
          handleCache.set(did, res.handle);
          setHandle(res.handle);
          setLoading(false);
        }
      })
      .catch(() => {
        if (!cancelled) {
          handleCache.set(did, null);
          setLoading(false);
        }
      });
    return () => { cancelled = true; };
  }, [did]);

  return (
    <Link to={`/accounts/${encodeURIComponent(did)}`} className="text-blue-600 hover:text-blue-700">
      {loading ? did : handle ? `${handle}` : did}
    </Link>
  );
}
