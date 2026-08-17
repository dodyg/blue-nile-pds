import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import {
  useConfirmEmail,
  useLogout,
  useProfile,
  useRequestEmailConfirmation,
  useRequestEmailUpdate,
  useResetPassword,
  useRequestPasswordReset,
  useSession,
  useUpdateEmail,
  useUpdateProfile,
  useUploadAvatar,
} from '../hooks/useAccount';
import type { BlobRef } from '../types/pds';
import { Card, CardHeader } from '../components/Card';
import { Input, Textarea } from '../components/Input';
import Button from '../components/Button';
import { XrpcError } from '../api/queryClient';

function errMessage(err: unknown): string | null {
  if (err instanceof XrpcError) return err.message;
  return 'Something went wrong';
}

const AVATAR_URL = (did: string, cid: string) =>
  `/xrpc/com.atproto.sync.getBlob?did=${encodeURIComponent(did)}&cid=${encodeURIComponent(cid)}`;

export default function Profile() {
  const hover = useLogout();
  const { data: session, isPending, isError } = useSession();

  const did = session?.did;
  const profile = useProfile(did);

  const [displayName, setDisplayName] = useState('');
  const [description, setDescription] = useState('');
  const [avatar, setAvatar] = useState<BlobRef | undefined>(undefined);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const updateProfile = useUpdateProfile();
  const uploadAvatar = useUploadAvatar();

  const requestEmailConfirmation = useRequestEmailConfirmation();
  const confirmEmail = useConfirmEmail();
  const requestEmailUpdate = useRequestEmailUpdate();
  const updateEmail = useUpdateEmail();
  const requestPasswordReset = useRequestPasswordReset();
  const resetPassword = useResetPassword();

  const [confirmToken, setConfirmToken] = useState('');
  const [confirmBusy, setConfirmBusy] = useState(false);
  const [confirmMsg, setConfirmMsg] = useState<string | null>(null);
  const [confirmErr, setConfirmErr] = useState<string | null>(null);
  const [tokenRequired, setTokenRequired] = useState(false);
  const [emailUpdate, setEmailUpdate] = useState('');
  const [emailUpdateToken, setEmailUpdateToken] = useState('');
  const [emailMsg, setEmailMsg] = useState<string | null>(null);
  const [emailErr, setEmailErr] = useState<string | null>(null);
  const [emailTokenRequested, setEmailTokenRequested] = useState(false);
  const [resetStep, setResetStep] = useState<'idle' | 'token' | 'done'>('idle');
  const [resetToken, setResetToken] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [resetMsg, setResetMsg] = useState<string | null>(null);
  const [resetErr, setResetErr] = useState<string | null>(null);

  useEffect(() => {
    if (profile.data) {
      setDisplayName(profile.data.record.displayName ?? '');
      setDescription(profile.data.record.description ?? '');
      setAvatar(profile.data.record.avatar);
    }
  }, [profile.data]);

  async function onAvatar(file: File) {
    setError(null);
    try {
      const uploaded = await uploadAvatar.mutateAsync({ blob: file, mimeType: file.type || 'image/png' });
      setAvatar(uploaded.blob);
    } catch (err) {
      setError(errMessage(err));
    }
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSaved(false);

    const record = {
      $type: 'app.bsky.actor.profile' as const,
      displayName: displayName.trim() || undefined,
      description: description.trim() || undefined,
      createdAt: profile.data?.record.createdAt ?? new Date().toISOString(),
      ...(avatar ? { avatar } : {}),
    };

    updateProfile.mutate(
      {
        did: did!,
        record,
        swapRecord: profile.data?.cid,
      },
      {
        onSuccess: () => setSaved(true),
        onError: (err) => setError(errMessage(err)),
      },
    );
  }

  function errMsg(err: unknown): string | null {
    return errMessage(err);
  }

  async function onSendConfirmCode() {
    setConfirmErr(null);
    setConfirmMsg(null);
    setConfirmBusy(true);
    try {
      await requestEmailConfirmation.mutateAsync();
      setConfirmMsg('Confirmation code sent. Check your email.');
    } catch (err) {
      setConfirmErr(errMsg(err));
    } finally {
      setConfirmBusy(false);
    }
  }

  async function onConfirmEmail(e: FormEvent) {
    e.preventDefault();
    setConfirmErr(null);
    setConfirmMsg(null);
    if (!confirmToken.trim()) {
      setConfirmErr('Enter the confirmation code.');
      return;
    }
    if (!session?.email) {
      setConfirmErr('No email on file to confirm.');
      return;
    }
    await confirmEmail.mutateAsync(
      { email: session.email, token: confirmToken.trim() },
      {
        onSuccess: () => setConfirmMsg('Email confirmed.'),
        onError: (err) => setConfirmErr(errMessage(err)),
      },
    );
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
      setEmailErr(errMsg(err));
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
    await updateEmail.mutateAsync(
      {
        email: emailUpdate.trim(),
        ...(tokenRequired ? { token: emailUpdateToken.trim() } : {}),
      },
      {
        onSuccess: () => {
          setEmailMsg('Email updated. It is not confirmed yet — send a confirmation code to verify it.');
          setEmailUpdate('');
          setEmailUpdateToken('');
          setTokenRequired(false);
          setEmailTokenRequested(false);
        },
        onError: (err) => setEmailErr(errMsg(err)),
      },
    );
  }

  async function onRequestPasswordReset() {
    setResetErr(null);
    setResetMsg(null);
    if (!session?.email) {
      setResetErr('No email on file to send a reset code to.');
      return;
    }
    await requestPasswordReset.mutateAsync(
      { email: session.email },
      {
        onSuccess: () => {
          setResetStep('token');
          setResetMsg('Reset code sent to your email.');
        },
        onError: (err) => setResetErr(errMessage(err)),
      },
    );
  }

  async function onResetPassword(e: FormEvent) {
    e.preventDefault();
    setResetErr(null);
    setResetMsg(null);
    if (!resetToken.trim()) {
      setResetErr('Enter the reset code.');
      return;
    }
    if (newPassword.length < 8) {
      setResetErr('Password must be at least 8 characters.');
      return;
    }
    if (newPassword !== confirmPassword) {
      setResetErr('Passwords do not match.');
      return;
    }
    await resetPassword.mutateAsync(
      { token: resetToken.trim(), password: newPassword },
      {
        onSuccess: () => {
          setResetStep('done');
          setResetMsg('Password updated. Sign out and sign in with your new password.');
        },
        onError: (err) => setResetErr(errMsg(err)),
      },
    );
  }

  if (isPending) {
    return (
      <div className="mx-auto max-w-md">
        <Card>
          <p className="px-4 py-6 text-center text-xs uppercase tracking-[0.16em] text-muted">Loading…</p>
        </Card>
      </div>
    );
  }

  if (isError || !session) {
    return (
      <div className="mx-auto max-w-md">
        <Card>
          <CardHeader title="Not signed in" />
          <div className="space-y-3 px-4 py-4">
            <p className="text-sm text-secondary">Please sign in to manage your profile.</p>
            <a
              href="/profile/login"
              className="inline-flex items-center justify-center gap-2 rounded-sm bg-board px-4 py-2 text-sm font-medium text-board-text shadow-chip transition-colors hover:bg-board-hover"
            >
              Sign in
            </a>
          </div>
        </Card>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-xl space-y-6">
      {session.active === false && session.status === 'suspended' && (
        <div className="rounded-sm border border-warning/30 bg-warning/10 px-4 py-3 text-sm text-warning-text dark:text-warning">
          Your account is pending approval by an administrator. Until it is approved, your profile is
          read-only and you cannot post.
        </div>
      )}

      <Card>
        <CardHeader title="Your profile" subtitle={`@${session.handle}`} />
        <div className="flex items-center gap-3 border-b border-subtle px-4 py-3">
          {avatar ? (
            <img src={AVATAR_URL(did!, avatar.ref.$link)} alt="Avatar" className="h-12 w-12 rounded-full object-cover" />
          ) : (
            <div className="flex h-12 w-12 items-center justify-center rounded-full bg-board text-lg font-bold text-board-text">
              {(displayName || session.handle)[0].toUpperCase()}
            </div>
          )}
          <div className="min-w-0 text-sm">
            <p className="font-semibold text-ink">{displayName || session.handle}</p>
            <p className="truncate font-mono text-xs text-secondary">{did}</p>
          </div>
        </div>

        <form className="space-y-4 px-4 py-4" onSubmit={onSubmit}>
          <label className="block">
            <span className="mb-1 block text-sm font-medium text-secondary">Display name</span>
            <Input
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              placeholder={session.handle}
              maxLength={64}
            />
          </label>

          <label className="block">
            <span className="mb-1 block text-sm font-medium text-secondary">Bio</span>
            <Textarea
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Tell people about yourself"
              rows={4}
              maxLength={256}
            />
          </label>

          <label className="block">
            <span className="mb-1 block text-sm font-medium text-secondary">Avatar</span>
            <input
              type="file"
              accept="image/*"
              onChange={(e) => {
                const file = e.target.files?.[0];
                if (file) void onAvatar(file);
              }}
              className="block w-full text-sm text-secondary file:mr-2 file:rounded-sm file:border-0 file:bg-board file:px-3 file:py-1.5 file:text-sm file:font-medium file:text-board-text file:shadow-chip hover:file:bg-board-hover"
            />
          </label>

          {error && (
            <p className="rounded-sm border border-danger/30 bg-danger/10 px-3 py-2 text-sm text-danger-deep dark:text-danger">
              {error}
            </p>
          )}
          {saved && (
            <p className="rounded-sm border border-success/30 bg-success/10 px-3 py-2 text-sm text-success-deep dark:text-success">
              Profile saved.
            </p>
          )}

          <div className="flex items-center gap-2">
            <Button type="submit" disabled={updateProfile.isPending || uploadAvatar.isPending}>
              {updateProfile.isPending ? 'Saving…' : 'Save profile'}
            </Button>
            <Button variant="ghost" onClick={hover}>
              Sign out
            </Button>
          </div>
        </form>
      </Card>

      <Card>
        <CardHeader
          title="Email"
          subtitle={
            session.emailConfirmed ? 'Email confirmed' : 'Email not confirmed'
          }
        />
        <div className="space-y-4 px-4 py-4">
          <p className="text-sm text-secondary">
            Current email:{' '}
            <span className="font-mono text-ink">{session.email ?? 'none'}</span>
          </p>

          {!session.emailConfirmed && session.email && (
            <div className="space-y-2">
              <div className="flex items-center gap-2">
                <span className="flex-1 rounded-sm border border-subtle bg-surface px-3 py-2 font-mono text-xs text-secondary">
                  Enter the code sent to your email
                </span>
                <Button
                  variant="secondary"
                  onClick={onSendConfirmCode}
                  disabled={confirmBusy}
                >
                  Send code
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
            {!emailTokenRequested && (
              <Button variant="secondary" onClick={onSendEmailUpdateCode}>
                {requestEmailUpdate.isPending ? 'Sending…' : 'Send verification code'}
              </Button>
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
        <CardHeader
          title="Password"
          subtitle="Reset your password using a code sent by email"
        />
        <div className="space-y-4 px-4 py-4">
          {resetStep === 'idle' && (
            <div className="flex items-center gap-2">
              <Button
                variant="secondary"
                onClick={onRequestPasswordReset}
                disabled={requestPasswordReset.isPending}
              >
                {requestPasswordReset.isPending ? 'Sending…' : 'Send reset code'}
              </Button>
              <span className="text-xs text-secondary">
                A code will be emailed to {session.email ?? 'your account'}
              </span>
            </div>
          )}
          {resetStep === 'token' && (
            <form className="space-y-2" onSubmit={onResetPassword}>
              <Input
                value={resetToken}
                onChange={(e) => setResetToken(e.target.value)}
                placeholder="Reset code"
                autoComplete="one-time-code"
              />
              <Input
                type="password"
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                placeholder="New password (at least 8 characters)"
                autoComplete="new-password"
              />
              <Input
                type="password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                placeholder="Confirm new password"
                autoComplete="new-password"
              />
              <div className="flex items-center gap-2">
                <Button
                  type="submit"
                  variant="primary"
                  disabled={resetPassword.isPending}
                >
                  {resetPassword.isPending ? 'Resetting…' : 'Reset password'}
                </Button>
                <Button
                  variant="ghost"
                  onClick={() => {
                    setResetStep('idle');
                    setResetToken('');
                    setNewPassword('');
                    setConfirmPassword('');
                  }}
                >
                  Cancel
                </Button>
              </div>
            </form>
          )}
          {resetMsg && <p className="text-xs text-success-deep">{resetMsg}</p>}
          {resetErr && <p className="text-xs text-danger">{resetErr}</p>}
        </div>
      </Card>
    </div>
  );
}