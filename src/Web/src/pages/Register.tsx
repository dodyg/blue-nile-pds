import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import TurnstileWidget from '../components/TurnstileWidget';
import { useCreateAccount, useSetAccountProfile } from '../hooks/useAccount';
import { useDescribeServer, useHandleAvailability } from '../hooks/useServer';
import { usePendingConfig, usePendingRegister } from '../hooks/usePending';
import { Card, CardHeader } from '../components/Card';
import { Input } from '../components/Input';
import Button from '../components/Button';
import { XrpcError } from '../api/queryClient';

function errMessage(err: unknown): string | null {
  if (err instanceof XrpcError) return err.message;
  return 'Something went wrong';
}

const ACCOUNT_TYPES = [
  { value: 'individual', label: 'Individual' },
  { value: 'organization', label: 'Organization' },
  { value: 'business', label: 'Business' },
];

export default function Register() {
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [handlePrefix, setHandlePrefix] = useState('');
  const [handleDomain, setHandleDomain] = useState('');
  const [customHandle, setCustomHandle] = useState('');
  const [useCustomDomain, setUseCustomDomain] = useState(false);
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [inviteCode, setInviteCode] = useState('');
  const [location, setLocation] = useState('');
  const [accountType, setAccountType] = useState('individual');
  const [formError, setFormError] = useState<string | null>(null);

  const describe = useDescribeServer();

  const CUSTOM_DOMAIN_VALUE = '__custom';
  const domains = (describe.data?.availableUserDomains ?? [])
    .filter((d) => d.trim() !== '')
    .map((d) => (d.startsWith('.') ? d : `.${d}`));
  const activeDomain = handleDomain || domains[0] || '';

  const effectiveHandle = useCustomDomain
    ? customHandle.trim().toLowerCase()
    : `${handlePrefix.trim().toLowerCase()}${activeDomain}`;

  const availabilityHandle =
    useCustomDomain || handlePrefix.trim() ? effectiveHandle : '';
  const availability = useHandleAvailability(availabilityHandle);
  const pendingConfig = usePendingConfig();
  const approvalRequired = pendingConfig.data?.approvalRequired ?? false;
  const turnstileRequired = approvalRequired
    && (pendingConfig.data?.turnstileRequired ?? false)
    && !!pendingConfig.data?.turnstileSiteKey;
  const [turnstileToken, setTurnstileToken] = useState('');
  const [turnstileKey, setTurnstileKey] = useState(0);

  const inviteRequired = describe.data?.inviteCodeRequired ?? false;
  const handleAvailable =
    availability.data?.result.$type === 'com.atproto.temp.checkHandleAvailability#resultAvailable';

  const createAccount = useCreateAccount();
  const setAccountProfile = useSetAccountProfile();
  const pendingRegister = usePendingRegister();

  async function onSubmit(e: React.FormEvent) {
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
    if (useCustomDomain) {
      if (effectiveHandle.length < 3 || !effectiveHandle.includes('.')) {
        setFormError('Handle must be your username followed by a domain (e.g. alice.example).');
        return;
      }
    } else {
      const prefix = handlePrefix.trim().toLowerCase();
      if (!prefix) {
        setFormError('Enter a handle prefix.');
        return;
      }
      if (!/^[a-z0-9-]+$/.test(prefix)) {
        setFormError('Handle prefix may only contain letters, numbers, and hyphens.');
        return;
      }
      if (!activeDomain) {
        setFormError('No handle domain is available. Use a custom domain instead.');
        return;
      }
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

    try {
      if (approvalRequired) {
        if (turnstileRequired && !turnstileToken) {
          setFormError('Please complete the captcha.');
          return;
        }
        await pendingRegister.mutateAsync({
          email: email.trim(),
          handle: effectiveHandle,
          password,
          ...(inviteRequired ? { inviteCode: inviteCode.trim() } : {}),
          ...(location.trim() ? { location: location.trim() } : {}),
          ...(accountType !== 'individual' ? { accountType } : {}),
          ...(turnstileRequired ? { verificationCode: turnstileToken } : {}),
        });
        navigate('/pending/profile');
        return;
      }

      await createAccount.mutateAsync({
        email: email.trim(),
        handle: effectiveHandle,
        password,
        ...(inviteRequired ? { inviteCode: inviteCode.trim() } : {}),
      });

      if (location.trim() || accountType !== 'individual') {
        setAccountProfile.mutate(
          {
            ...(location.trim() ? { location: location.trim() } : {}),
            ...(accountType !== 'individual' ? { accountType } : {}),
          },
          { onError: () => undefined },
        );
      }

      navigate('/profile');
    } catch (err) {
      setFormError(errMessage(err));
      if (turnstileRequired) {
        setTurnstileToken('');
        setTurnstileKey((k) => k + 1);
      }
    }
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

          <div className="block">
            <span className="mb-1 block text-sm font-medium text-secondary">Handle</span>
            {domains.length > 0 && !useCustomDomain ? (
              <>
                <div className="flex">
                  <Input
                    value={handlePrefix}
                    onChange={(e) => {
                      const raw = e.target.value.toLowerCase();
                      const domain = activeDomain.toLowerCase();
                      setHandlePrefix(
                        domain && raw.endsWith(domain) ? raw.slice(0, -domain.length) : raw,
                      );
                    }}
                    placeholder="alice"
                    autoComplete="username"
                    required
                    className="rounded-r-none"
                  />
                  <span className="inline-flex shrink-0 items-center rounded-r-sm border border-l-0 border-subtle bg-hover px-3 py-2 font-mono text-sm text-secondary">
                    {activeDomain || '…'}
                  </span>
                </div>
                {domains.length > 1 && (
                  <select
                    value={activeDomain}
                    onChange={(e) => {
                      const value = e.target.value;
                      if (value === CUSTOM_DOMAIN_VALUE) {
                        setUseCustomDomain(true);
                        return;
                      }
                      setHandleDomain(value);
                      setHandlePrefix((prev) => {
                        const lowered = prev.toLowerCase();
                        const next = value.toLowerCase();
                        return next && lowered.endsWith(next) ? lowered.slice(0, -next.length) : prev;
                      });
                    }}
                    className="mt-2 block w-full rounded-sm border border-subtle bg-surface px-3 py-2 text-sm text-ink focus:outline-2 focus:outline-offset-2 focus:outline-focus-ring"
                    aria-label="Handle domain"
                  >
                    {domains.map((d) => (
                      <option key={d} value={d}>
                        {d}
                      </option>
                    ))}
                    <option value={CUSTOM_DOMAIN_VALUE}>Custom…</option>
                  </select>
                )}
                {domains.length <= 1 && (
                  <button
                    type="button"
                    onClick={() => setUseCustomDomain(true)}
                    className="mt-1 text-xs text-secondary underline hover:text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
                  >
                    Use a custom domain instead
                  </button>
                )}
              </>
            ) : (
              <>
                <Input
                  value={customHandle}
                  onChange={(e) => setCustomHandle(e.target.value)}
                  placeholder="alice.example"
                  autoComplete="username"
                  required
                />
                {domains.length > 0 && (
                  <button
                    type="button"
                    onClick={() => {
                      setUseCustomDomain(false);
                      setCustomHandle('');
                    }}
                    className="mt-1 text-xs text-secondary underline hover:text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
                  >
                    Back to {domains[0]} handles
                  </button>
                )}
              </>
            )}
            {availability.isSuccess && (
              <p className={`mt-1 text-xs ${handleAvailable ? 'text-success-deep' : 'text-danger'}`}>
                {handleAvailable ? 'Handle is available.' : 'Handle is already taken.'}
              </p>
            )}
            {availability.isError && (
              <p className="mt-1 text-xs text-danger">That handle does not look right.</p>
            )}
          </div>

          <label className="block">
            <span className="mb-1 block text-sm font-medium text-secondary">Location</span>
            <Input
              value={location}
              onChange={(e) => setLocation(e.target.value)}
              placeholder="City, Country"
              maxLength={200}
            />
          </label>

          <label className="block">
            <span className="mb-1 block text-sm font-medium text-secondary">Account type</span>
            <select
              value={accountType}
              onChange={(e) => setAccountType(e.target.value)}
              className="block w-full rounded-sm border border-subtle bg-surface px-3 py-2 text-sm text-ink focus:outline-2 focus:outline-offset-2 focus:outline-focus-ring"
            >
              {ACCOUNT_TYPES.map((t) => (
                <option key={t.value} value={t.value}>
                  {t.label}
                </option>
              ))}
            </select>
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

          {turnstileRequired && pendingConfig.data?.turnstileSiteKey && (
            <TurnstileWidget
              siteKey={pendingConfig.data.turnstileSiteKey}
              resetKey={turnstileKey}
              onVerify={(token: string) => setTurnstileToken(token)}
              onExpire={() => setTurnstileToken('')}
              onError={() => setTurnstileToken('')}
              onUnsupported={() => setFormError('Captcha is not supported by your browser.')}
            />
          )}

          {formError && (
            <p className="rounded-sm border border-danger/30 bg-danger/10 px-3 py-2 text-sm text-danger-deep dark:text-danger">
              {formError}
            </p>
          )}

          <Button type="submit" disabled={createAccount.isPending || pendingRegister.isPending} className="w-full">
            {createAccount.isPending || pendingRegister.isPending ? 'Creating account…' : 'Create account'}
          </Button>
          {approvalRequired && <p className="text-center text-xs text-muted">Your registration will be reviewed by an administrator.</p>}
        </form>
      </Card>
    </div>
  );
}