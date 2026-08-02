import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useCreateInviteCode, useCreateInviteCodes } from '../hooks/useInvites';

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

  return (
    <div>
      <button
        onClick={() => navigate('/invites')}
        className="text-primary hover:text-primary-hover text-sm mb-4 font-medium focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
      >
        ← Back to invite codes
      </button>

      <h1 className="text-2xl font-bold mb-5">Create Invite Codes</h1>

      {error && <p className="text-danger mb-4">{error.message}</p>}

      {result ? (
        <div className="bg-surface border border-subtle shadow-card rounded-md p-5">
          <p className="text-success-deep font-semibold mb-4">
            {result.codes.length === 1
              ? 'Invite code created successfully'
              : `${result.codes.length} invite codes created`}
          </p>
          <div className="space-y-2 mb-5">
            {result.codes.map((code, i) => (
              <div key={i} className="flex items-center gap-2 font-mono text-sm bg-page border border-subtle rounded-md px-3 py-2">
                <span className="flex-1 break-all">{code}</span>
                <button
                  onClick={() => copyCode(code)}
                  className="text-xs px-2 py-1 rounded-md bg-surface border border-input hover:bg-hover shrink-0 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
                >
                  {copied === code ? 'Copied!' : 'Copy'}
                </button>
              </div>
            ))}
          </div>
          <div className="flex gap-3">
            <button
              onClick={handleCreateAnother}
              className="px-4 py-2 bg-primary text-surface rounded-md text-sm hover:bg-primary-hover focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
            >
              Create another batch
            </button>
            <button
              onClick={() => navigate('/invites')}
              className="px-4 py-2 bg-surface text-ghost border border-input rounded-md text-sm hover:bg-row-hover focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
            >
              View all invite codes
            </button>
          </div>
        </div>
      ) : (
        <form onSubmit={handleSubmit} className="bg-surface border border-subtle shadow-card rounded-md p-5 space-y-5 max-w-lg">
          <div className="flex gap-1 bg-hover rounded-md p-1 w-fit">
            <button
              type="button"
              onClick={() => setMode('single')}
              className={`px-4 py-1.5 rounded-sm text-sm font-medium transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring ${
                mode === 'single' ? 'bg-surface text-ink shadow-card' : 'text-muted hover:text-ghost'
              }`}
            >
              Single code
            </button>
            <button
              type="button"
              onClick={() => setMode('bulk')}
              className={`px-4 py-1.5 rounded-sm text-sm font-medium transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring ${
                mode === 'bulk' ? 'bg-surface text-ink shadow-card' : 'text-muted hover:text-ghost'
              }`}
            >
              Bulk codes
            </button>
          </div>

          <div>
            <label className="block text-sm text-secondary mb-1">Uses per code</label>
            <input
              type="number"
              min={1}
              value={useCount}
              onChange={e => setUseCount(Math.max(1, parseInt(e.target.value) || 1))}
              className="w-full px-3 py-2 rounded-md bg-surface border border-input text-ink focus:border-focus-ring focus:outline-none"
            />
          </div>

          {mode === 'bulk' && (
            <div>
              <label className="block text-sm text-secondary mb-1">Number of codes to generate</label>
              <input
                type="number"
                min={1}
                value={codeCount}
                onChange={e => setCodeCount(Math.max(1, parseInt(e.target.value) || 1))}
                className="w-full px-3 py-2 rounded-md bg-surface border border-input text-ink focus:border-focus-ring focus:outline-none"
              />
            </div>
          )}

          <div>
            <label className="block text-sm text-secondary mb-1">
              For account <span className="text-danger">*</span>
            </label>
            <input
              type="text"
              placeholder="did:plc:..."
              value={forAccount}
              onChange={e => setForAccount(e.target.value)}
              className="w-full px-3 py-2 rounded-md bg-surface border border-input text-ink focus:border-focus-ring focus:outline-none"
            />
          </div>

          <button
            type="submit"
            disabled={isPending}
            className="w-full px-4 py-2 bg-primary text-surface rounded-md text-sm font-medium hover:bg-primary-hover disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
          >
            {isPending ? 'Generating...' : mode === 'single' ? 'Generate code' : `Generate ${codeCount} codes`}
          </button>
        </form>
      )}
    </div>
  );
}
