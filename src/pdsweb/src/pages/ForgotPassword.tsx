import { useState } from 'react';
import type { FormEvent } from 'react';
import { useRequestPasswordReset, useResetPassword } from '../hooks/useAccount';
import { Card, CardHeader } from '../components/Card';
import { Input } from '../components/Input';
import Button from '../components/Button';
import { XrpcError } from '../api/queryClient';
import { Link } from 'react-router-dom';

function errMessage(err: unknown): string | null {
  if (err instanceof XrpcError) return err.message;
  return 'Something went wrong';
}

export default function ForgotPassword() {
  const [step, setStep] = useState<'email' | 'token' | 'done'>('email');
  const [email, setEmail] = useState('');
  const [token, setToken] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [msg, setMsg] = useState<string | null>(null);
  const [err, setErr] = useState<string | null>(null);

  const requestPasswordReset = useRequestPasswordReset();
  const resetPassword = useResetPassword();

  async function onRequest(e: FormEvent) {
    e.preventDefault();
    setErr(null);
    setMsg(null);
    if (!email.trim() || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
      setErr('Enter a valid email address.');
      return;
    }
    await requestPasswordReset.mutateAsync(
      { email: email.trim() },
      {
        onSuccess: () => {
          setStep('token');
          setMsg('If that email belongs to an account, a reset code was sent to it.');
        },
        onError: (e2) => setErr(errMessage(e2)),
      },
    );
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setErr(null);
    setMsg(null);
    if (!token.trim()) {
      setErr('Enter the reset code.');
      return;
    }
    if (password.length < 8) {
      setErr('Password must be at least 8 characters.');
      return;
    }
    if (password !== confirmPassword) {
      setErr('Passwords do not match.');
      return;
    }
    await resetPassword.mutateAsync(
      { token: token.trim(), password },
      {
        onSuccess: () => {
          setStep('done');
          setMsg('Password reset. Sign in with your new password.');
        },
        onError: (e2) => setErr(errMessage(e2)),
      },
    );
  }

  return (
    <div className="mx-auto max-w-md">
      <Card>
        <CardHeader title="Reset password" subtitle="Get a code by email, then set a new password." />
        <div className="space-y-4 px-4 py-4">
          {step === 'email' && (
            <form className="space-y-4" onSubmit={onRequest}>
              <label className="block">
                <span className="mb-1 block text-sm font-medium text-secondary">Email</span>
                <Input
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="you@example.com"
                  autoComplete="email"
                  required
                />
              </label>
              <Button type="submit" disabled={requestPasswordReset.isPending} className="w-full">
                {requestPasswordReset.isPending ? 'Sending…' : 'Send reset code'}
              </Button>
            </form>
          )}

          {step === 'token' && (
            <form className="space-y-4" onSubmit={onSubmit}>
              <label className="block">
                <span className="mb-1 block text-sm font-medium text-secondary">Code</span>
                <Input
                  value={token}
                  onChange={(e) => setToken(e.target.value)}
                  placeholder="Reset code from email"
                  autoComplete="one-time-code"
                  required
                />
              </label>
              <label className="block">
                <span className="mb-1 block text-sm font-medium text-secondary">New password</span>
                <Input
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="At least 8 characters"
                  autoComplete="new-password"
                  required
                />
              </label>
              <label className="block">
                <span className="mb-1 block text-sm font-medium text-secondary">Confirm new password</span>
                <Input
                  type="password"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  placeholder="Repeat your password"
                  autoComplete="new-password"
                  required
                />
              </label>
              <Button type="submit" disabled={resetPassword.isPending} className="w-full">
                {resetPassword.isPending ? 'Resetting…' : 'Reset password'}
              </Button>
            </form>
          )}

          {step === 'done' && (
            <div className="space-y-3">
              <p className="text-sm text-secondary">
                You can now sign in with your new password.
              </p>
              <Link
                to="/profile/login"
                className="inline-flex items-center justify-center gap-2 rounded-sm bg-board px-4 py-2 text-sm font-medium text-board-text shadow-chip transition-colors hover:bg-board-hover"
              >
                Go to sign in
              </Link>
            </div>
          )}

          {msg && <p className="text-xs text-success-deep">{msg}</p>}
          {err && <p className="text-xs text-danger">{err}</p>}
        </div>
      </Card>
    </div>
  );
}