import { useDashboardStats } from '../hooks/useDashboard';
import PageHeader from '../components/PageHeader';
import BoardStat from '../components/BoardStat';

export default function Dashboard() {
  const { data: totalAccounts, isPending, error } = useDashboardStats();

  return (
    <div>
      <PageHeader
        eyebrow="operations · overview"
        title="Dashboard"
        description="Live readout for this personal data server."
      />

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <BoardStat
          label="Total Accounts"
          value={isPending ? '…' : totalAccounts ?? 0}
          sub="registered on this PDS"
        />
      </div>

      {error && <p className="mt-4 text-sm text-danger">{error.message}</p>}

      <div className="mt-8 rounded-sm border border-subtle bg-surface px-4 py-3 font-mono text-[10px] uppercase tracking-[0.16em] text-muted shadow-card">
        All systems nominal · {new Date().toLocaleDateString()}
      </div>
    </div>
  );
}
