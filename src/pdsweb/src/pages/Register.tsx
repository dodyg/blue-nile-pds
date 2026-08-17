import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useCreateAccount } from '../hooks/useAccount';
import { useDescribeServer, useHandleAvailability } from '../hooks/useServer';
import { Card, CardHeader } from '../components/Card';
import { Input } from '../components/Input';
import Button from '../components/Button';
import { XrpcError } from '../api/queryClient';

function errMessage(err: unknown): string | null {
  if (err instanceof XrpcError) return err.message;
  return 'Something went wrong';
}

export default function Register() {
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [handle, setHandle] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [inviteCode, setInviteCode] = useState('');
  const [formError, setFormError] = useState<string | null>(null);

  const describe = useDescribeServer();
  const availability = useHandleAvailability(handle.trim());

  const inviteRequired = describe.data?.inviteCodeRequired ?? false;
  const effectiveHandle = handle.trim().toLowerCase();
  const handleAvailable =
    availability.data?.result.$type === 'com.atproto.temp.checkHandleAvailability#resultAvailable';

  const createAccount = useCreateAccount();

  function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setFormError(null);

    if (!email.trim()) {
      setFormError('Email is required.');
      return;
    }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
      setFormError('Enter a valid email address.');
      return;
    }
    if (effectiveHandle.length < 3 || !effectiveHandle.includes('.')) {
      setFormError('Handle must be your username followed by a domain (e.g. alice.example).');
      return;
    }
    if (availability.isSuccess && !handleAvailable) {
      setFormError('That handle is not available.');
      return;
    }
    if (password.length < 8) {
      setFormError('Password must be at least 8 characters.');
      return;
    }
    if (confirmPassword !== password) {
      setFormError('Passwords do not match.');
      return;
    }
    if (inviteRequired && !inviteCode.trim()) {
      setFormError('This PDS requires an invite code.');
      return;
    }

    createAccount.mutate(
      {
        email: email.trim(),
        handle: effectiveHandle,
        password,
        ...(inviteRequired ? { inviteCode: inviteCode.trim() } : {}),
      },
      {
        onSuccess: () => navigate('/profile'),
        onError: (err) => setFormError(errMessage(err)),
      },
    );
  }

  return (
    <div className="mx-auto max-w-md">
      <Card>
        <CardHeader
          title="Create your account"
          subtitle={inviteRequired ? 'An invite code is required on this PDS.' : 'No invite code required on this PDS.'}
        />
        <form className="space-y-4 px-4 py-4" onSubmit={onSubmit}>
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

          <label className="block">
            <span className="mb-1 block text-sm font-medium text-secondary">Handle</span>
            <Input
              value={handle}
              onChange={(e) => setHandle(e.target.value)}
              placeholder="alice.example"
              autoComplete="username"
              required
            />
            {availability.isSuccess && (
              <p className={`mt-1 text-xs ${handleAvailable ? 'text-success-deep' : 'text-danger'}`}>
                {handleAvailable ? 'Handle is available.' : 'Handle is already taken.'}
              </p>
            )}
            {availability.isError && (
              <p className="mt-1 text-xs text-danger">That handle does not look right.</p>
            )}
          </label>

          <label className="block">
            <span className="mb-1 block text-sm font-medium text-secondary">Password</span>
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
            <span className="mb-1 block text-sm font-medium text-secondary">Confirm password</span>
            <Input
              type="password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              placeholder="Repeat your password"
              autoComplete="new-password"
              required
            />
          </label>

          {inviteRequired && (
            <label className="block">
              <span className="mb-1 block text-sm font-medium text-secondary">Invite code</span>
              <Input
                value={inviteCode}
                onChange={(e) => setInviteCode(e.target.value)}
                placeholder="Invite code"
                autoComplete="off"
                required
              />
            </label>
          )}

          {formError && (
            <p className="rounded-sm border border-danger/30 bg-danger/10 px-3 py-2 text-sm text-danger-deep dark:text-danger">
              {formError}
            </p>
          )}

          <Button type="submit" disabled={createAccount.isPending} className="w-full">
            {createAccount.isPending ? 'Creating account…' : 'Create account'}
          </Button>
        </form>
      </Card>
    </div>
  );
}