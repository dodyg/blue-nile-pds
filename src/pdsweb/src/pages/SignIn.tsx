import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useCreateSession } from '../hooks/useAccount';
import { Card, CardHeader } from '../components/Card';
import { Input } from '../components/Input';
import Button from '../components/Button';
import { XrpcError } from '../api/queryClient';

function errMessage(err: unknown): string | null {
  if (err instanceof XrpcError) return err.message;
  return 'Something went wrong';
}

export default function SignIn() {
  const navigate = useNavigate();
  const [identifier, setIdentifier] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);

  const signIn = useCreateSession();

  function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    signIn.mutate(
      { identifier: identifier.trim(), password },
      {
        onSuccess: () => navigate('/profile', { replace: true }),
        onError: (err) => setError(errMessage(err)),
      },
    );
  }

  return (
    <div className="mx-auto max-w-md">
      <Card>
        <CardHeader title="Sign in" subtitle="Manage your public profile." />
        <form className="space-y-4 px-4 py-4" onSubmit={onSubmit}>
          <label className="block">
            <span className="mb-1 block text-sm font-medium text-secondary">Handle or email</span>
            <Input
              value={identifier}
              onChange={(e) => setIdentifier(e.target.value)}
              placeholder="alice.example or you@example.com"
              autoComplete="username"
              required
            />
          </label>
          <label className="block">
            <span className="mb-1 block text-sm font-medium text-secondary">Password</span>
            <Input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
              required
            />
          </label>
          {error && (
            <p className="rounded-sm border border-danger/30 bg-danger/10 px-3 py-2 text-sm text-danger-deep dark:text-danger">
              {error}
            </p>
          )}
          <Button type="submit" disabled={signIn.isPending} className="w-full">
            {signIn.isPending ? 'Signing in…' : 'Sign in'}
          </Button>
          <p className="text-center text-sm">
            <Link to="/profile/forgot-password" className="text-secondary underline transition-colors hover:text-ink">
              Forgot password?
            </Link>
          </p>
        </form>
      </Card>
    </div>
  );
}