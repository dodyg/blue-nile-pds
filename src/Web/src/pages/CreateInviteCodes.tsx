import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useCreateInviteCode, useCreateInviteCodes } from '../hooks/useInvites';
import PageHeader from '../components/PageHeader';
import Button from '../components/Button';
import { Input, Textarea } from '../components/Input';
import { Card } from '../components/Card';
import Badge from '../components/Badge';
import AccountPicker, { type PickedAccount } from '../components/AccountPicker';

type Mode = 'single' | 'bulk';
type InputMode = 'search' | 'manual';

function parseManualDids(raw: string): string[] {
  return raw.split('\n').map(s => s.trim()).filter(Boolean);
}

export default function CreateInviteCodes() {
  const navigate = useNavigate();
  const [mode, setMode] = useState<Mode>('single');
  const [inputMode, setInputMode] = useState<InputMode>('search');
  const [useCount, setUseCount] = useState(1);
  const [selected, setSelected] = useState<PickedAccount[]>([]);
  const [manualDid, setManualDid] = useState('');
  const [manualDids, setManualDids] = useState('');
  const [codeCount, setCodeCount] = useState(5);
  const [result, setResult] = useState<{ codes: string[]; forAccount: string } | null>(null);
  const [copied, setCopied] = useState<string | null>(null);

  const createSingle = useCreateInviteCode();
  const createBulk = useCreateInviteCodes();

  const isPending = createSingle.isPending || createBulk.isPending;
  const error = createSingle.error || createBulk.error;

  function resetErrors() {
    createSingle.reset();
    createBulk.reset();
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setResult(null);

    if (mode === 'single') {
      const forAccount = inputMode === 'search'
        ? selected[0]?.did.trim()
        : manualDid.trim();
      if (!forAccount) return;
      const label = inputMode === 'search' && selected[0]
        ? `${selected[0].handle} (${forAccount})`
        : forAccount;
      createSingle.mutate(
        { useCount, forAccount },
        {
          onSuccess: (data) => setResult({ codes: [data.code], forAccount: label }),
        },
      );
    } else {
      const forAccounts = inputMode === 'search'
        ? selected.map(s => s.did)
        : parseManualDids(manualDids);
      if (forAccounts.length === 0) return;
      const label = inputMode === 'search'
        ? selected.map(s => s.handle).join(', ')
        : forAccounts.join(', ');
      createBulk.mutate(
        { useCount, codeCount, forAccounts },
        {
          onSuccess: (data) => {
            const allCodes = data.codes.flatMap(c => c.codes);
            setResult({ codes: allCodes, forAccount: label });
          },
        },
      );
    }
  }

  async function copyCode(code: string) {
    try {
      await navigator.clipboard.writeText(code);
      setCopied(code);
      setTimeout(() => setCopied(null), 2000);
    } catch {
      // clipboards may not be available
    }
  }

  function handleCreateAnother() {
    setResult(null);
    setSelected([]);
    setManualDid('');
    setManualDids('');
  }

  function switchInputMode(next: InputMode) {
    setInputMode(next);
    setResult(null);
    resetErrors();
  }

  function switchMode(next: Mode) {
    setMode(next);
    setResult(null);
    resetErrors();
  }

  const modeTab = (tab: Mode, label: string) => (
    <button
      type="button"
      onClick={() => switchMode(tab)}
      className={`rounded-sm px-4 py-1.5 font-mono text-xs font-semibold uppercase tracking-[0.12em] transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring ${
        mode === tab ? 'bg-board text-board-text shadow-chip' : 'text-muted hover:text-ghost'
      }`}
    >
      {label}
    </button>
  );

  const inputTab = (tab: InputMode, label: string) => (
    <button
      type="button"
      onClick={() => switchInputMode(tab)}
      className={`rounded-sm px-3 py-1 font-mono text-[10px] font-semibold uppercase tracking-[0.12em] transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring ${
        inputMode === tab ? 'bg-board text-board-text shadow-chip' : 'text-muted hover:text-ghost'
      }`}
    >
      {label}
    </button>
  );

  const manualDidsParsed = parseManualDids(manualDids);
  const canSubmit = inputMode === 'search'
    ? mode === 'single' ? selected.length === 1 : selected.length >= 1
    : mode === 'single' ? manualDid.trim().length > 0 : manualDidsParsed.length > 0;
  const invitesOffCount = selected.filter(s => s.invitesDisabled).length;

  return (
    <div>
      <Button variant="ghost" size="sm" className="mb-3 -ml-2" onClick={() => navigate('/admin/invites')}>
        ← Back to invite codes
      </Button>

      <PageHeader
        eyebrow="invites · issuance"
        title="Create Invite Codes"
        description="Issue one or more invite codes for an account."
      />

      {error && <p className="mb-4 text-sm text-danger">{error.message}</p>}

      {result ? (
        <Card className="max-w-lg p-5">
          <p className="mb-4 font-display text-sm font-semibold tracking-[0.08em] text-success-deep dark:text-success">
            {result.codes.length === 1
              ? 'Invite code created successfully'
              : `${result.codes.length} invite codes created`}
          </p>
          <p className="mb-4 truncate text-xs text-secondary">For: {result.forAccount}</p>
          <div className="mb-5 space-y-2">
            {result.codes.map((code, i) => (
              <div key={i} className="flex items-center gap-2 rounded-sm border border-subtle bg-page px-3 py-2 font-mono text-xs">
                <span className="flex-1 break-all text-ink">{code}</span>
                <Button variant="secondary" size="sm" onClick={() => copyCode(code)}>
                  {copied === code ? 'Copied!' : 'Copy'}
                </Button>
              </div>
            ))}
          </div>
          <div className="flex gap-3">
            <Button variant="primary" onClick={handleCreateAnother}>Create another batch</Button>
            <Button variant="secondary" onClick={() => navigate('/admin/invites')}>View all invite codes</Button>
          </div>
        </Card>
      ) : (
        <form onSubmit={handleSubmit} className="max-w-lg space-y-5 rounded-md border border-subtle bg-surface p-5 shadow-card">
          <div className="flex w-fit gap-1 rounded-sm border border-subtle bg-hover p-1">
            {modeTab('single', 'Single code')}
            {modeTab('bulk', 'Bulk codes')}
          </div>

          <div>
            <label className="mb-1.5 block font-mono text-[10px] font-semibold uppercase tracking-[0.16em] text-secondary">Uses per code</label>
            <Input
              type="number"
              min={1}
              value={useCount}
              onChange={e => setUseCount(Math.max(1, parseInt(e.target.value) || 1))}
            />
          </div>

          {mode === 'bulk' && (
            <div>
              <label className="mb-1.5 block font-mono text-[10px] font-semibold uppercase tracking-[0.16em] text-secondary">Number of codes to generate</label>
              <Input
                type="number"
                min={1}
                value={codeCount}
                onChange={e => setCodeCount(Math.max(1, parseInt(e.target.value) || 1))}
              />
            </div>
          )}

          <div>
            <div className="mb-1.5 flex items-center justify-between">
              <label className="block font-mono text-[10px] font-semibold uppercase tracking-[0.16em] text-secondary">
                For account{mode === 'bulk' && inputMode === 'search' ? 's' : ''} <span className="text-danger">*</span>
              </label>
              <div className="flex w-fit gap-1 rounded-sm border border-subtle bg-hover p-0.5">
                {inputTab('search', 'Search')}
                {inputTab('manual', 'Paste DID')}
              </div>
            </div>

            {inputMode === 'search' ? (
              <>
                <AccountPicker
                  value={selected}
                  onChange={(next) => { setSelected(next); resetErrors(); }}
                  multi={mode === 'bulk'}
                  disabled={isPending}
                />
                {invitesOffCount > 0 && (
                  <p className="mt-1.5 flex items-center gap-2 text-xs text-secondary">
                    <Badge tone="warning">invites off</Badge>
                    {invitesOffCount === 1 ? 'This account has' : `${invitesOffCount} accounts have`} invites disabled — the server may reject it.
                  </p>
                )}
              </>
            ) : mode === 'single' ? (
              <Input
                type="text"
                placeholder="did:plc:..."
                value={manualDid}
                onChange={e => setManualDid(e.target.value)}
              />
            ) : (
              <>
                <Textarea
                  rows={4}
                  placeholder={'did:plc:...\ndid:plc:...'}
                  value={manualDids}
                  onChange={e => setManualDids(e.target.value)}
                />
                <p className="mt-1 text-xs text-secondary">One DID per line{manualDidsParsed.length > 0 ? ` — ${manualDidsParsed.length} detected` : ''}.</p>
              </>
            )}
          </div>

          <Button type="submit" variant="primary" disabled={isPending || !canSubmit} className="w-full">
            {isPending ? 'Generating...' : mode === 'single' ? 'Generate code' : `Generate ${codeCount} codes`}
          </Button>
        </form>
      )}
    </div>
  );
}
