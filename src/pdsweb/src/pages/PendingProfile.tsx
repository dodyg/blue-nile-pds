import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { usePendingProfile, usePendingUpdateProfile, usePendingRequestEmailConfirmation, usePendingConfirmEmail, usePendingLogout } from '../hooks/usePending';
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

  const [confirmToken, setConfirmToken] = useState('');
  const [confirmMsg, setConfirmMsg] = useState<string | null>(null);
  const [confirmErr, setConfirmErr] = useState<string | null>(null);

  useEffect(() => {
    if (profile.data) {
      setDisplayName((profile.data as { displayName?: string }).displayName ?? '');
      setDescription((profile.data as { description?: string }).description ?? '');
      setLocation((profile.data as { location?: string }).location ?? '');
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

  const data = profile.data as unknown as { email: string; handle: string; status: string; emailConfirmed?: boolean; createdAt: string; location?: string; accountType?: string; displayName?: string };
  const emailConfirmed = !!data?.emailConfirmed;

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

  return (
    <div className="mx-auto max-w-xl space-y-6">
      <div className="rounded-sm border border-warning/30 bg-warning/10 px-4 py-3 text-sm text-warning-text dark:text-warning">
        Your registration is <strong>{data?.status ?? 'pending'}</strong>. An administrator will review it and you will receive an email once approved.
        {emailConfirmed ? null : ' Please confirm your email below.'}
      </div>

      <Card>
        <CardHeader title="Pending registration" subtitle={data ? `@${data.handle} · ${data.email}` : undefined} actions={<Badge tone={data?.status === 'Approved' ? 'success' : 'warning'}>{data?.status ?? 'pending'}</Badge>} />
        <div className="px-4 py-3 text-sm text-secondary">
          <p>Handle: <span className="font-mono text-ink">{data?.handle}</span></p>
          <p>Email: <span className="font-mono text-ink">{data?.email}</span> {emailConfirmed ? <Badge tone="success">confirmed</Badge> : <Badge tone="warning">not confirmed</Badge>}</p>
          <p>Requested: {data?.createdAt ? new Date(data.createdAt).toLocaleString() : '—'}</p>
        </div>
      </Card>

      {!emailConfirmed && (
        <Card>
          <CardHeader title="Confirm email" />
          <div className="space-y-3 px-4 py-4">
            <div className="flex items-center gap-2">
              <Button variant="secondary" onClick={onSendConfirmCode} disabled={requestEmailConfirmation.isPending}>
                {requestEmailConfirmation.isPending ? 'Sending…' : 'Send confirmation code'}
              </Button>
            </div>
            <form className="flex items-center gap-2" onSubmit={onConfirmEmail}>
              <Input value={confirmToken} onChange={(e) => setConfirmToken(e.target.value)} placeholder="Confirmation code" autoComplete="one-time-code" />
              <Button type="submit" variant="primary" disabled={confirmEmail.isPending}>Confirm</Button>
            </form>
            {confirmMsg && <p className="text-xs text-success-deep">{confirmMsg}</p>}
            {confirmErr && <p className="text-xs text-danger">{confirmErr}</p>}
          </div>
        </Card>
      )}

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
