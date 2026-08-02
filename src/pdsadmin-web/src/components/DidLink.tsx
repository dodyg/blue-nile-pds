import { Link } from 'react-router-dom';
import { useAccountInfo } from '../hooks/useAccounts';

interface Props {
  did: string;
}

export default function DidLink({ did }: Props) {
  const { data, isPending } = useAccountInfo(did);

  return (
    <Link
      to={`/accounts/${encodeURIComponent(did)}`}
      className="font-mono text-xs text-primary underline decoration-subtle underline-offset-2 transition-colors hover:text-primary-hover hover:decoration-accent"
    >
      {isPending ? did : data?.handle ?? did}
    </Link>
  );
}
