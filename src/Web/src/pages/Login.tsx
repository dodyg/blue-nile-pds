import { type FormEvent, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { setAdminPassword } from '../stores/auth';
import { validatePassword } from '../api/adminClient';
import Button from '../components/Button';
import { Input } from '../components/Input';

export default function Login() {
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const navigate = useNavigate();

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      const ok = await validatePassword(password);
      if (ok) {
        setAdminPassword(password);
        navigate('/admin', { replace: true });
      } else {
        setError('Invalid admin password');
      }
    } catch {
      setError('Connection failed');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-page px-4">
      <form
        onSubmit={handleSubmit}
        className="w-full max-w-sm rounded-md border border-subtle bg-surface p-8 shadow-card"
      >
        <div className="rounded-sm border border-black/15 bg-board px-4 py-3 text-center shadow-chip">
          <p className="font-display text-base font-bold tracking-[0.18em] text-board-text uppercase">PDS Admin</p>
          <p className="mt-1 font-mono text-[9px] tracking-[0.14em] text-board-text-dim uppercase">atproto · departure board</p>
        </div>

        <p className="mt-6 mb-1.5 font-mono text-[10px] font-semibold uppercase tracking-[0.16em] text-secondary">
          Admin password
        </p>
        <Input
          type="password"
          placeholder="Admin password"
          value={password}
          onChange={e => setPassword(e.target.value)}
          autoFocus
        />

        {error && (
          <p className="mt-3 rounded-sm border border-danger/30 bg-danger/10 px-3 py-2 text-sm text-danger-deep dark:text-danger">
            {error}
          </p>
        )}

        <Button
          type="submit"
          variant="primary"
          disabled={loading || !password}
          className="mt-5 w-full"
        >
          {loading ? 'Verifying...' : 'Sign in'}
        </Button>
      </form>
    </div>
  );
}
