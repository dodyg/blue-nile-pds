import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  usePendingProfile,
  usePendingUpdateProfile,
  usePendingRequestEmailConfirmation,
  usePendingConfirmEmail,
  usePendingRequestEmailUpdate,
  usePendingUpdateEmail,
  usePendingLogout,
} from '../hooks/usePending';
import { Card, CardHeader } from '../components/Card';
import { Input, Textarea } from '../components/Input';
import Button from '../components/Button';
import Badge from '../components/Badge';
import { XrpcError } from '../api/queryClient';
import { getPendingAccessJwt } from '../stores/pendingAuth';

function errMessage(err: unknown): string | null {
  if (err instanceof XrpcError) return err.message || err.error || 'Something went wrong';
  return 'Something went wrong';
}

function initials(name: string): string {
  const trimmed = name.trim();
  if (!trimmed) return '?';
  return trimmed[0].toUpperCase();
}

export default function PendingProfile() {
  const navigate = useNavigate();
  const profile = usePendingProfile();
  const pendingJwt = getPendingAccessJwt();
  const logout = usePendingLogout();

  const [displayName, setDisplayName] = useState('');
  const [description, setDescription] = useState('');
  const [location, setLocation] = useState('');
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const updateProfile = usePendingUpdateProfile();
  const requestEmailConfirmation = usePendingRequestEmailConfirmation();
  const confirmEmail = usePendingConfirmEmail();
  const requestEmailUpdate = usePendingRequestEmailUpdate();
  const updateEmail = usePendingUpdateEmail();

  const [confirmToken, setConfirmToken] = useState('');
  const [confirmMsg, setConfirmMsg] = useState<string | null>(null);
  const [confirmErr, setConfirmErr] = useState<string | null>(null);

  const [emailUpdate, setEmailUpdate] = useState('');
  const [emailUpdateToken, setEmailUpdateToken] = useState('');
  const [emailMsg, setEmailMsg] = useState<string | null>(null);
  const [emailErr, setEmailErr] = useState<string | null>(null);
  const [emailTokenRequested, setEmailTokenRequested] = useState(false);
  const [tokenRequired, setTokenRequired] = useState(false);

  useEffect(() => {
    if (profile.data) {
      const d = profile.data as unknown as { displayName?: string; description?: string; location?: string };
      setDisplayName(d.displayName ?? '');
      setDescription(d.description ?? '');
      setLocation(d.location ?? '');
    }
  }, [profile.data]);

  if (!pendingJwt) {
    return (
      <div className="mx-auto max-w-md">
        <Card>
          <CardHeader title="Not signed in" />
          <div className="space-y-3 px-4 py-4">
            <p className="text-sm text-secondary">Please sign in with your pending account.</p>
            <a href="/profile/login" className="inline-flex items-center justify-center gap-2 rounded-sm bg-board px-4 py-2 text-sm font-medium text-board-text shadow-chip hover:bg-board-hover">
              Sign in
            </a>
          </div>
        </Card>
      </div>
    );
  }

  if (profile.isPending) {
    return (
      <div className="mx-auto max-w-md">
        <Card><p className="px-4 py-6 text-center text-xs uppercase tracking-[0.16em] text-muted">Loading…</p></Card>
      </div>
    );
  }

  if (profile.isError) {
    const msg = errMessage(profile.error);
    return (
      <div className="mx-auto max-w-xl space-y-6">
        <Card>
          <CardHeader title="Pending profile" />
          <div className="px-4 py-4 space-y-3">
            <p className="text-sm text-danger">{msg}</p>
            <Button variant="ghost" onClick={() => { logout(); navigate('/register'); }}>Sign out</Button>
          </div>
        </Card>
      </div>
    );
  }

  const data = profile.data as unknown as {
    email: string;
    handle: string;
    status: string;
    emailConfirmed?: boolean;
    emailConfirmedAt?: string;
    createdAt: string;
    location?: string;
    accountType?: string;
    displayName?: string;
    description?: string;
  };
  const emailConfirmed = !!(data?.emailConfirmed || data?.emailConfirmedAt);
  const statusLower = (data?.status ?? 'pending').toLowerCase();
  const isApproved = statusLower === 'approved';

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSaved(false);
    updateProfile.mutate(
      { displayName: displayName.trim() || undefined, description: description.trim() || undefined, location: location.trim() || undefined },
      { onSuccess: () => setSaved(true), onError: (err) => setError(errMessage(err)) },
    );
  }

  async function onSendConfirmCode() {
    setConfirmErr(null); setConfirmMsg(null);
    try { await requestEmailConfirmation.mutateAsync(); setConfirmMsg('Confirmation code sent. Check your email.'); }
    catch (err) { setConfirmErr(errMessage(err)); }
  }

  async function onConfirmEmail(e: FormEvent) {
    e.preventDefault();
    setConfirmErr(null); setConfirmMsg(null);
    if (!confirmToken.trim()) { setConfirmErr('Enter the confirmation code.'); return; }
    try { await confirmEmail.mutateAsync({ token: confirmToken.trim() }); setConfirmMsg('Email confirmed.'); setConfirmToken(''); }
    catch (err) { setConfirmErr(errMessage(err)); }
  }

  async function onSendEmailUpdateCode() {
    setEmailErr(null);
    setEmailMsg(null);
    setEmailTokenRequested(true);
    try {
      const res = await requestEmailUpdate.mutateAsync();
      setTokenRequired(res.tokenRequired);
      setEmailMsg(
        res.tokenRequired
          ? 'Verification code sent to your current email.'
          : 'No code needed — you can update your email directly.',
      );
    } catch (err) {
      setEmailTokenRequested(false);
      setEmailErr(errMessage(err));
    }
  }

  async function onUpdateEmail(e: FormEvent) {
    e.preventDefault();
    setEmailErr(null);
    setEmailMsg(null);
    if (!emailUpdate.trim() || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(emailUpdate.trim())) {
      setEmailErr('Enter a valid email address.');
      return;
    }
    if (tokenRequired && !emailUpdateToken.trim()) {
      setEmailErr('Enter the verification code.');
      return;
    }
    try {
      await updateEmail.mutateAsync({
        email: emailUpdate.trim(),
        ...(tokenRequired ? { token: emailUpdateToken.trim() } : {}),
      });
      setEmailMsg('Email updated. It is not confirmed yet — send a confirmation code to verify it.');
      setEmailUpdate('');
      setEmailUpdateToken('');
      setTokenRequired(false);
      setEmailTokenRequested(false);
    } catch (err) {
      setEmailErr(errMessage(err));
    }
  }

  const avatarLabel = initials(displayName || data?.handle || '?');
  const requestedAt = data?.createdAt ? new Date(data.createdAt) : null;
  const accountTypeLabel = data?.accountType && data.accountType !== 'individual' ? data.accountType : null;

  return (
    <div className="mx-auto max-w-xl space-y-6">
      <div className="rounded-sm border border-warning/30 bg-warning/10 px-4 py-3 text-sm text-warning-text dark:text-warning">
        Your registration is <strong>{data?.status ?? 'pending'}</strong>. An administrator will review it and you will receive an email once approved.
        {emailConfirmed ? null : ' Please confirm your email below.'}
      </div>

      {/* Identity card — enhanced */}
      <Card>
        <CardHeader
          title={displayName || data?.handle || 'Pending registration'}
          subtitle={data ? `@${data.handle}` : undefined}
          actions={<Badge tone={isApproved ? 'success' : 'warning'}>{data?.status ?? 'pending'}</Badge>}
        />
        <div className="flex items-center gap-3 border-b border-subtle px-4 py-4">
          <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-board text-lg font-bold text-board-text">
            {avatarLabel}
          </div>
          <div className="min-w-0 flex-1">
            <p className="truncate text-sm font-semibold text-ink">{displayName || data?.handle}</p>
            <p className="truncate font-mono text-xs text-secondary">{data?.email}</p>
            <div className="mt-1 flex flex-wrap items-center gap-1.5">
              {emailConfirmed ? <Badge tone="success">email confirmed</Badge> : <Badge tone="warning">email not confirmed</Badge>}
              {accountTypeLabel && <Badge tone="neutral">{accountTypeLabel}</Badge>}
            </div>
          </div>
        </div>
        <div className="grid grid-cols-1 gap-x-6 gap-y-3 px-4 py-4 sm:grid-cols-2">
          <div className="space-y-0.5">
            <p className="text-xs font-medium uppercase tracking-[0.08em] text-muted">Handle</p>
            <p className="font-mono text-sm text-ink">{data?.handle}</p>
          </div>
          <div className="space-y-0.5">
            <p className="text-xs font-medium uppercase tracking-[0.08em] text-muted">Email</p>
            <p className="flex flex-wrap items-center gap-1.5 font-mono text-sm text-ink">
              <span className="break-all">{data?.email}</span>
              {emailConfirmed ? <Badge tone="success">confirmed</Badge> : <Badge tone="warning">unconfirmed</Badge>}
            </p>
          </div>
          {data?.location && (
            <div className="space-y-0.5">
              <p className="text-xs font-medium uppercase tracking-[0.08em] text-muted">Location</p>
              <p className="text-sm text-ink">{data.location}</p>
            </div>
          )}
          <div className="space-y-0.5">
            <p className="text-xs font-medium uppercase tracking-[0.08em] text-muted">Requested</p>
            <p className="text-sm text-ink">{requestedAt ? requestedAt.toLocaleString() : '—'}</p>
          </div>
          {accountTypeLabel && (
            <div className="space-y-0.5">
              <p className="text-xs font-medium uppercase tracking-[0.08em] text-muted">Account type</p>
              <p className="text-sm capitalize text-ink">{accountTypeLabel}</p>
            </div>
          )}
          <div className="space-y-0.5">
            <p className="text-xs font-medium uppercase tracking-[0.08em] text-muted">Status</p>
            <p className="text-sm capitalize text-ink">{data?.status ?? 'pending'}</p>
          </div>
        </div>
      </Card>

      {/* Email card — confirm + change */}
      <Card>
        <CardHeader
          title="Email"
          subtitle={emailConfirmed ? 'Email confirmed' : 'Email not confirmed'}
        />
        <div className="space-y-4 px-4 py-4">
          <p className="text-sm text-secondary">
            Current email: <span className="break-all font-mono text-ink">{data?.email ?? '—'}</span>
          </p>

          {!emailConfirmed && (
            <div className="space-y-2">
              <div className="flex items-center gap-2">
                <span className="flex-1 rounded-sm border border-subtle bg-surface px-3 py-2 font-mono text-xs text-secondary">
                  Enter the code sent to your email
                </span>
                <Button
                  variant="secondary"
                  onClick={onSendConfirmCode}
                  disabled={requestEmailConfirmation.isPending}
                >
                  {requestEmailConfirmation.isPending ? 'Sending…' : 'Send code'}
                </Button>
              </div>
              <form className="flex items-center gap-2" onSubmit={onConfirmEmail}>
                <Input
                  value={confirmToken}
                  onChange={(e) => setConfirmToken(e.target.value)}
                  placeholder="Confirmation code"
                  autoComplete="one-time-code"
                />
                <Button
                  type="submit"
                  variant="primary"
                  disabled={confirmEmail.isPending}
                >
                  Confirm
                </Button>
              </form>
              {confirmMsg && <p className="text-xs text-success-deep">{confirmMsg}</p>}
              {confirmErr && <p className="text-xs text-danger">{confirmErr}</p>}
            </div>
          )}

          <form className="space-y-2 border-t border-subtle pt-4" onSubmit={onUpdateEmail}>
            <span className="mb-1 block text-sm font-medium text-secondary">
              Change email
            </span>
            {emailConfirmed && !emailTokenRequested && (
              <Button variant="secondary" onClick={onSendEmailUpdateCode} disabled={requestEmailUpdate.isPending}>
                {requestEmailUpdate.isPending ? 'Sending…' : 'Send verification code'}
              </Button>
            )}
            {!emailConfirmed && (
              <p className="text-xs text-muted">
                Your email is not confirmed. You can change it directly.
              </p>
            )}
            <Input
              type="email"
              value={emailUpdate}
              onChange={(e) => setEmailUpdate(e.target.value)}
              placeholder="new@example.com"
              autoComplete="email"
            />
            {tokenRequired && (
              <Input
                value={emailUpdateToken}
                onChange={(e) => setEmailUpdateToken(e.target.value)}
                placeholder="Verification code"
                autoComplete="one-time-code"
              />
            )}
            <div className="flex items-center gap-2">
              <Button
                type="submit"
                variant="primary"
                disabled={updateEmail.isPending}
              >
                {updateEmail.isPending ? 'Updating…' : 'Update email'}
              </Button>
            </div>
            {emailMsg && <p className="text-xs text-success-deep">{emailMsg}</p>}
            {emailErr && <p className="text-xs text-danger">{emailErr}</p>}
          </form>
        </div>
      </Card>

      <Card>
        <CardHeader title="Edit profile" subtitle="Update your pending profile information" />
        <form className="space-y-4 px-4 py-4" onSubmit={onSubmit}>
          <label className="block">
            <span className="mb-1 block text-sm font-medium text-secondary">Display name</span>
            <Input value={displayName} onChange={(e) => setDisplayName(e.target.value)} placeholder="Your name" maxLength={64} />
          </label>
          <label className="block">
            <span className="mb-1 block text-sm font-medium text-secondary">Bio</span>
            <Textarea value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Tell us about yourself" rows={3} maxLength={256} />
          </label>
          <label className="block">
            <span className="mb-1 block text-sm font-medium text-secondary">Location</span>
            <Input value={location} onChange={(e) => setLocation(e.target.value)} placeholder="City, Country" maxLength={200} />
          </label>
          {error && <p className="rounded-sm border border-danger/30 bg-danger/10 px-3 py-2 text-sm text-danger">{error}</p>}
          {saved && <p className="rounded-sm border border-success/30 bg-success/10 px-3 py-2 text-sm text-success-deep">Profile saved.</p>}
          <div className="flex items-center gap-2">
            <Button type="submit" disabled={updateProfile.isPending}>{updateProfile.isPending ? 'Saving…' : 'Save'}</Button>
            <Button variant="ghost" onClick={() => { logout(); navigate('/register'); }}>Sign out</Button>
          </div>
        </form>
      </Card>
    </div>
  );
}
