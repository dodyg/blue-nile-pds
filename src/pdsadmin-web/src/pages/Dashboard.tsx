import { useDashboardStats } from '../hooks/useDashboard';

export default function Dashboard() {
  const { data: totalAccounts, isPending, error } = useDashboardStats();

  return (
    <div>
      <h1 className="text-2xl font-bold mb-6">Dashboard</h1>
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div className="bg-surface border border-subtle shadow-card rounded-md p-5">
          <div className="text-sm text-secondary mb-1">Total Accounts</div>
          <div className="text-3xl font-bold">
            {isPending ? '...' : totalAccounts}
          </div>
        </div>
      </div>
      {error && <p className="text-danger mt-4">{error.message}</p>}
    </div>
  );
}
