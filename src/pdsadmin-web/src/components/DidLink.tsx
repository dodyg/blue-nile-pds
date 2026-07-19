import { Link } from 'react-router-dom';
import { useAccountInfo } from '../hooks/useAccounts';

interface Props {
  did: string;
}

export default function DidLink({ did }: Props) {
  const { data, isPending } = useAccountInfo(did);

  return (
    <Link to={`/accounts/${encodeURIComponent(did)}`} className="text-blue-600 hover:text-blue-700">
      {isPending ? did : data?.handle ?? did}
    </Link>
  );
}
