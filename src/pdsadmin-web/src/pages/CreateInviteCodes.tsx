import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useCreateInviteCode, useCreateInviteCodes } from '../hooks/useInvites';
import PageHeader from '../components/PageHeader';
import Button from '../components/Button';
import { Input } from '../components/Input';
import { Card } from '../components/Card';

type Mode = 'single' | 'bulk';

export default function CreateInviteCodes() {
  const navigate = useNavigate();
  const [mode, setMode] = useState<Mode>('single');
  const [useCount, setUseCount] = useState(1);
  const [forAccount, setForAccount] = useState('');
  const [codeCount, setCodeCount] = useState(5);
  const [result, setResult] = useState<{ codes: string[]; forAccount: string } | null>(null);
  const [copied, setCopied] = useState<string | null>(null);

  const createSingle = useCreateInviteCode();
  const createBulk = useCreateInviteCodes();

  const isPending = createSingle.isPending || createBulk.isPending;
  const error = createSingle.error || createBulk.error;

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const trimmedFor = forAccount.trim();
    if (!trimmedFor) return;
    setResult(null);

    if (mode === 'single') {
      createSingle.mutate(
        { useCount, forAccount: trimmedFor },
        {
          onSuccess: (data) => setResult({ codes: [data.code], forAccount: trimmedFor }),
        },
      );
    } else {
      createBulk.mutate(
        { useCount, codeCount, forAccounts: [trimmedFor] },
        {
          onSuccess: (data) => {
            const allCodes = data.codes.flatMap(c => c.codes);
            setResult({ codes: allCodes, forAccount: trimmedFor });
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
  }

  const modeTab = (tab: Mode, label: string) => (
    <button
      type="button"
      onClick={() => setMode(tab)}
      className={`rounded-sm px-4 py-1.5 font-mono text-xs font-semibold uppercase tracking-[0.12em] transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring ${
        mode === tab ? 'bg-board text-board-text shadow-chip' : 'text-muted hover:text-ghost'
      }`}
    >
      {label}
    </button>
  );

  return (
    <div>
      <Button variant="ghost" size="sm" className="mb-3 -ml-2" onClick={() => navigate('/invites')}>
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
            <Button variant="secondary" onClick={() => navigate('/invites')}>View all invite codes</Button>
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
            <label className="mb-1.5 block font-mono text-[10px] font-semibold uppercase tracking-[0.16em] text-secondary">
              For account <span className="text-danger">*</span>
            </label>
            <Input
              type="text"
              placeholder="did:plc:..."
              value={forAccount}
              onChange={e => setForAccount(e.target.value)}
            />
          </div>

          <Button type="submit" variant="primary" disabled={isPending} className="w-full">
            {isPending ? 'Generating...' : mode === 'single' ? 'Generate code' : `Generate ${codeCount} codes`}
          </Button>
        </form>
      )}
    </div>
  );
}
