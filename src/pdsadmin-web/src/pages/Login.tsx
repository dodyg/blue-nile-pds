import { type FormEvent, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { setAdminPassword } from '../stores/auth';
import { validatePassword } from '../api/client';

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
        navigate('/', { replace: true });
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
    <div className="min-h-screen flex items-center justify-center bg-page">
      <form onSubmit={handleSubmit} className="bg-surface p-8 rounded-md shadow-card border border-subtle w-80">
        <h1 className="text-xl font-bold text-ink mb-6 text-center">PDS Admin</h1>
        <input
          type="password"
          placeholder="Admin password"
          value={password}
          onChange={e => setPassword(e.target.value)}
          className="w-full px-3 py-2 rounded-md bg-surface text-ink border border-input focus:border-focus-ring focus:outline-none mb-4"
          autoFocus
        />
        {error && <p className="text-danger text-sm mb-4">{error}</p>}
        <button
          type="submit"
          disabled={loading || !password}
          className="w-full py-2 rounded-md bg-primary text-surface font-medium hover:bg-primary-hover disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
        >
          {loading ? 'Verifying...' : 'Sign in'}
        </button>
      </form>
    </div>
  );
}
