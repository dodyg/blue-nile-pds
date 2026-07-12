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
    <div className="min-h-screen flex items-center justify-center bg-gray-50">
      <form onSubmit={handleSubmit} className="bg-white p-8 rounded-lg shadow-md border border-gray-200 w-80">
        <h1 className="text-xl font-bold text-gray-900 mb-6 text-center">PDS Admin</h1>
        <input
          type="password"
          placeholder="Admin password"
          value={password}
          onChange={e => setPassword(e.target.value)}
          className="w-full px-3 py-2 rounded bg-gray-50 text-gray-900 border border-gray-300 focus:border-blue-500 focus:outline-none mb-4"
          autoFocus
        />
        {error && <p className="text-red-600 text-sm mb-4">{error}</p>}
        <button
          type="submit"
          disabled={loading || !password}
          className="w-full py-2 rounded bg-blue-600 text-white font-medium hover:bg-blue-700 disabled:opacity-50"
        >
          {loading ? 'Verifying...' : 'Sign in'}
        </button>
      </form>
    </div>
  );
}
